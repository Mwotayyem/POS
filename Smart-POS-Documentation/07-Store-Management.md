# 07 — Store Management (إدارة الشركات والفروع)

> إدارة **التسلسل الهرمي للمُلكية** في المنصّة: المستأجر (`Tenant` = الشركة/الحساب) في القمّة، وتحته فرع واحد أو أكثر (`Store`). هذا الملف هو المرجع التشغيلي لكل ما يخصّ **إنشاء المستأجرين (Onboarding)**، **إدارة الفروع**، **التفعيل/التعطيل**، و**حدود الاشتراك (Subscription Limits)** — كل ذلك **دون تعديل أي سطر كود** عند استقبال عميل جديد.

> يلتزم هذا الملف بالكامل بالمرجع الحاكم [04-Database-Design.md](04-Database-Design.md) (الأعمدة المشتركة، أنواع البيانات، الفهرسة، Soft Delete)، ويكمّله ملف العزل [24-MultiTenant.md](24-MultiTenant.md).

---

## 1) الهدف من الصفحة (Purpose)

توفير واجهة إدارية (Back-office) يستطيع من خلالها:

- **Platform Admin** (مشغّل المنصّة): إنشاء مستأجر جديد، تفعيله/تعطيله، تعديل خطّة اشتراكه وحدوده، ومراقبة استهلاكه.
- **Tenant Owner** (مالك الشركة): إدارة فروع شركته (إضافة/تعديل/تعطيل فرع)، ضبط الفرع الافتراضي، ومراجعة حدود اشتراكه المتبقية.

المبدأ الحاكم: **إضافة عميل جديد = صفّ في جدول `Tenants` + أول صفّ في `Stores`** — لا `if` جديد في الكود، ولا نشر جديد. القطاع (`BusinessType`) والخصوصيات تُفعَّل عبر إعدادات و Feature Flags لا عبر فروع كود.

### التسلسل الهرمي (Hierarchy)

```
Tenant (الشركة / الحساب التجاري)
   │  1
   │
   │  N
   └── Store (فرع / متجر)
          │  1
          │  N
          ├── Warehouses (مستودعات الفرع)
          ├── Users (مستخدمو الفرع)         ← عبر UserStores
          ├── PosShifts (شفتات الكاشير)
          └── Stock / Sales / Purchases ...  (كل بيانات الأعمال StoreId)
```

> `TenantId` إجباري في كل جدول أعمال. `StoreId` يكون `NULL` للكيانات المشتركة على مستوى الشركة (كتالوج مركزي مثلاً) و`NOT NULL` للكيانات الخاصة بفرع (رصيد مخزون فرع، شفت كاشير).

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية (Permission Key) | الوصف | يملكها |
|---------------------------|-------|--------|
| `Platform.Tenants.View` | عرض قائمة كل المستأجرين | Platform Admin فقط |
| `Platform.Tenants.Create` | إنشاء مستأجر جديد (Onboarding) | Platform Admin فقط |
| `Platform.Tenants.ManageSubscription` | تعديل الخطّة والحدود | Platform Admin فقط |
| `Platform.Tenants.Suspend` | تعطيل/تعليق مستأجر | Platform Admin فقط |
| `Stores.View` | عرض فروع الشركة | Tenant Owner, Store Manager |
| `Stores.Create` | إنشاء فرع جديد | Tenant Owner |
| `Stores.Edit` | تعديل بيانات فرع | Tenant Owner |
| `Stores.Deactivate` | تعطيل فرع | Tenant Owner |

**قواعد حاكمة على الصلاحيات:**

1. صلاحيات `Platform.*` معزولة تماماً عن المستأجرين — تُمنح فقط لمستخدمي المنصّة (لا ينتمون لأي `TenantId`، أو ينتمون لمستأجر إداري خاص).
2. `Tenant Owner` **لا يرى** أي مستأجر آخر — كل استعلاماته مفلترة تلقائياً بـ `TenantId` عبر Global Query Filter.
3. إنشاء فرع خاضع لـ **حدّ الاشتراك** `MaxStores` — تُرفض العملية عند بلوغ السقف (انظر §7).

---

## 3) تصميم الصفحة (Page Layout)

**شاشة Platform — Tenants List:**

```
┌────────────────────────────────────────────────────────────────────┐
│  المستأجرون (Tenants)                          [+ مستأجر جديد]       │
├────────────────────────────────────────────────────────────────────┤
│  🔍 بحث   | الحالة: [الكل ▾] | الخطّة: [الكل ▾]                       │
├──────┬───────────────┬──────────┬──────────┬─────────┬──────────────┤
│ #    │ اسم الشركة     │ Subdomain│ الخطّة    │ الفروع  │ الحالة        │
├──────┼───────────────┼──────────┼──────────┼─────────┼──────────────┤
│ 1001 │ متجر النور     │ alnoor   │ Pro      │ 3 / 5   │ 🟢 Active     │
│ 1002 │ صيدلية الحياة  │ hayat    │ Basic    │ 1 / 1   │ 🟡 Trial      │
│ 1003 │ سوبر ماركت X   │ superx   │ Enterprise│ 12 / ∞ │ 🔴 Suspended  │
└──────┴───────────────┴──────────┴──────────┴─────────┴──────────────┘
```

**شاشة Tenant Owner — Stores List:**

```
┌────────────────────────────────────────────────────────────────────┐
│  فروعي (Stores)               حدّ الاشتراك: 3 / 5      [+ فرع جديد]  │
├──────┬───────────────┬───────────┬───────────┬────────┬─────────────┤
│ #    │ اسم الفرع      │ الرمز(Code)│ المدينة    │ افتراضي│ الحالة       │
├──────┼───────────────┼───────────┼───────────┼────────┼─────────────┤
│ 501  │ الفرع الرئيسي  │ MAIN      │ عمّان      │ ★      │ 🟢 Active    │
│ 502  │ فرع الزرقاء     │ ZRQ       │ الزرقاء    │        │ 🟢 Active    │
│ 503  │ فرع إربد        │ IRB       │ إربد       │        │ ⚪ Inactive   │
└──────┴───────────────┴───────────┴───────────┴────────┴─────────────┘
```

---

## 4) جميع الأزرار (Buttons)

| الزر | الشاشة | الإجراء | الصلاحية | تأكيد؟ |
|------|--------|---------|----------|--------|
| **+ مستأجر جديد** | Tenants | يفتح معالج Onboarding (4 خطوات) | `Platform.Tenants.Create` | — |
| **تفعيل / تعطيل** | Tenants | يبدّل حالة المستأجر (`Active`⇄`Suspended`) | `Platform.Tenants.Suspend` | ✅ |
| **تعديل الخطّة** | Tenants | يفتح لوحة الحدود والخطّة | `Platform.Tenants.ManageSubscription` | — |
| **الدخول كـ (Impersonate)** | Tenants | يفتح جلسة قراءة/دعم داخل المستأجر | `Platform.Tenants.Impersonate` | ✅ + سبب |
| **+ فرع جديد** | Stores | يفتح نموذج إنشاء فرع | `Stores.Create` | — |
| **تعديل** | Stores | تعديل بيانات الفرع | `Stores.Edit` | — |
| **تعيين افتراضي** | Stores | يجعل الفرع الافتراضي للشركة | `Stores.Edit` | — |
| **تعطيل الفرع** | Stores | Soft-disable للفرع | `Stores.Deactivate` | ✅ |

---

## 5) جميع الحقول (Fields)

### 5.1 حقول المستأجر (Tenant Onboarding)

| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|--------|---------|
| اسم الشركة (`Name`) | نص | ✅ | الاسم التجاري المعروض |
| الرمز الفريد (`Subdomain`) | نص (a-z0-9-) | ✅ | يُستخدم كـ `tenant.smartpos.app` — فريد عالمياً |
| البريد الإداري (`OwnerEmail`) | بريد | ✅ | يُنشئ أول مستخدم `Tenant Owner` |
| رقم الهاتف (`Phone`) | هاتف | ➖ | — |
| البلد/العملة (`CountryId`/`BaseCurrency`) | قائمة | ✅ | يضبط العملة والضريبة الافتراضية |
| القطاع (`BusinessType`) | قائمة | ✅ | Supermarket / Pharmacy / ... يفعّل Feature Flags |
| الخطّة (`PlanCode`) | قائمة | ✅ | Trial / Basic / Pro / Enterprise |

### 5.2 حقول الفرع (Store)

| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|--------|---------|
| اسم الفرع (`Name`) | نص | ✅ | — |
| الرمز (`Code`) | نص | ✅ | فريد داخل المستأجر (يظهر بأرقام الفواتير) |
| المدينة/العنوان (`City`/`Address`) | نص | ➖ | — |
| الهاتف (`Phone`) | هاتف | ➖ | يظهر بترويسة الفاتورة |
| المنطقة الزمنية (`TimeZone`) | قائمة | ✅ | لتحويل `DATETIME2` UTC للعرض |
| افتراضي (`IsDefault`) | تبديل | ➖ | فرع واحد فقط افتراضي لكل مستأجر |

---

## 6) التحقق (Validation)

| القاعدة | الرسالة عند الفشل |
|---------|-------------------|
| `Subdomain` فريد عالمياً + نمط `^[a-z0-9](-?[a-z0-9])*$` بطول 3–40 | "الرمز مستخدَم أو غير صالح" |
| `Subdomain` ليس ضمن قائمة محجوزة (`www`, `api`, `admin`, `app`) | "هذا الرمز محجوز للنظام" |
| `OwnerEmail` صيغة بريد صحيحة وغير مسجّل كـ Owner لمستأجر آخر | "البريد مستخدَم بالفعل" |
| `Store.Code` فريد داخل `TenantId` (Unique filtered index) | "رمز الفرع مكرّر داخل شركتك" |
| عدد الفروع الفعّالة < `MaxStores` | "بلغت الحدّ الأقصى للفروع في خطّتك" |
| لا يمكن تعطيل الفرع الافتراضي إن كان الوحيد الفعّال | "لا يمكن تعطيل الفرع الوحيد الفعّال" |

> التحقق يُطبَّق عبر **FluentValidation** في طبقة التطبيق، ويُدعَّم بقيود قاعدة البيانات (Unique Indexes, Check Constraints) كخطّ دفاع ثانٍ.

---

## 7) قواعد العمل (Business Rules)

1. **Onboarding ذرّي:** إنشاء مستأجر جديد يُنفَّذ في **معاملة واحدة** تُنشئ: صفّ `Tenants` + صفّ `Stores` (الفرع الرئيسي) + مستخدم `Tenant Owner` + صفّ `TenantSettings` + تسلسلات الترقيم الافتراضية (`Sequences`). إن فشلت أي خطوة، يُلغى كل شيء (Rollback).
2. **لا تعديل كود:** القطاع يُخزَّن كقيمة (`BusinessType`) وتُفعَّل خصوصياته عبر Feature Flags — لا فرع كود لكل قطاع.
3. **حدود الاشتراك (Subscription Limits):** تُخزَّن في `Tenants` (أو `TenantSubscriptions`) وتُفحَص قبل كل عملية إنشاء:

   | الحدّ | العمود | يُفحَص عند |
   |------|--------|-----------|
   | أقصى عدد فروع | `MaxStores` | إنشاء فرع |
   | أقصى عدد مستخدمين | `MaxUsers` | إنشاء مستخدم |
   | أقصى عدد منتجات | `MaxProducts` | إنشاء منتج |
   | تاريخ انتهاء الاشتراك | `SubscriptionEndsUtc` | تسجيل الدخول |

4. **التعطيل ناعم (Soft):** تعطيل مستأجر يضبط `IsActive = 0` — لا يُحذف. عند التعطيل تُرفض جلسات الدخول الجديدة، وتُبطَل Refresh Tokens تدريجياً، وتبقى البيانات كاملة للاسترجاع أو التصفية المحاسبية.
5. **التدرّج بالحالات (Status Machine):** `Trial → Active → PastDue → Suspended → Cancelled`. الانتقال يحرّكه إمّا Platform Admin يدوياً أو **Background Job** لمراقبة الاشتراكات (Hangfire) عند انتهاء المدّة.
6. **الفرع الافتراضي:** يُستخدم عند تسجيل دخول مستخدم غير مرتبط بفرع محدّد، وكقيمة `StoreId` افتراضية في POS.

---

## 8) جداول قاعدة البيانات (Database Tables)

### 8.1 جدول `Tenants`

> **ملاحظة تصميمية:** `Tenants` هو جدول القمّة — لا يحمل `TenantId` (هو نفسه المستأجر) ولا `StoreId`. يحمل بقية الأعمدة المشتركة (التدقيق + Soft Delete + ConcurrencyStamp) وفق المرجع الحاكم.

```sql
CREATE TABLE [dbo].[Tenants]
(
    [Id]                  BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Tenants_PublicId DEFAULT (NEWID()),
    [Name]                NVARCHAR(200)    NOT NULL,
    [Subdomain]           VARCHAR(40)      NOT NULL,   -- alnoor -> alnoor.smartpos.app
    [CustomDomain]        VARCHAR(253)     NULL,        -- pos.alnoor.com (لاحقاً)
    [OwnerEmail]          NVARCHAR(256)    NOT NULL,
    [Phone]               VARCHAR(32)      NULL,
    [CountryId]           INT              NULL,        -- مرجع Reference Data
    [BaseCurrency]        CHAR(3)          NOT NULL CONSTRAINT DF_Tenants_Currency DEFAULT ('USD'),
    [BusinessType]        VARCHAR(30)      NOT NULL,    -- Supermarket / Pharmacy / ...
    [PlanCode]            VARCHAR(20)      NOT NULL CONSTRAINT DF_Tenants_Plan DEFAULT ('TRIAL'),
    [Status]              VARCHAR(20)      NOT NULL CONSTRAINT DF_Tenants_Status DEFAULT ('TRIAL'),
                          -- TRIAL / ACTIVE / PASTDUE / SUSPENDED / CANCELLED
    [IsActive]            BIT              NOT NULL CONSTRAINT DF_Tenants_IsActive DEFAULT (1),
    -- ===== حدود الاشتراك (Subscription Limits) =====
    [MaxStores]           INT              NOT NULL CONSTRAINT DF_Tenants_MaxStores  DEFAULT (1),
    [MaxUsers]            INT              NOT NULL CONSTRAINT DF_Tenants_MaxUsers   DEFAULT (3),
    [MaxProducts]         INT              NOT NULL CONSTRAINT DF_Tenants_MaxProds   DEFAULT (500),
    [TrialEndsUtc]        DATETIME2(3)     NULL,
    [SubscriptionEndsUtc] DATETIME2(3)     NULL,
    -- ===== الأعمدة المشتركة (بدون TenantId/StoreId لأنه جدول القمّة) =====
    [CreatedDate]         DATETIME2(3)     NOT NULL CONSTRAINT DF_Tenants_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]           BIGINT           NULL,
    [ModifiedDate]        DATETIME2(3)     NULL,
    [ModifiedBy]          BIGINT           NULL,
    [DeletedDate]         DATETIME2(3)     NULL,
    [DeletedBy]           BIGINT           NULL,
    [IsDeleted]           BIT              NOT NULL CONSTRAINT DF_Tenants_IsDeleted DEFAULT (0),
    [ConcurrencyStamp]    ROWVERSION       NOT NULL,

    CONSTRAINT [PK_Tenants] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [CK_Tenants_Status] CHECK ([Status] IN ('TRIAL','ACTIVE','PASTDUE','SUSPENDED','CANCELLED')),
    CONSTRAINT [CK_Tenants_MaxStores] CHECK ([MaxStores] > 0)
);
GO

-- الرمز الفريد عالمياً (subdomain) — أساس توجيه الطلب للمستأجر
CREATE UNIQUE NONCLUSTERED INDEX [UX_Tenants_Subdomain]
    ON [dbo].[Tenants] ([Subdomain]) WHERE [IsDeleted] = 0;
GO

-- الدومين المخصّص فريد عالمياً عند وجوده (custom domain mapping)
CREATE UNIQUE NONCLUSTERED INDEX [UX_Tenants_CustomDomain]
    ON [dbo].[Tenants] ([CustomDomain])
    WHERE [CustomDomain] IS NOT NULL AND [IsDeleted] = 0;
GO

CREATE NONCLUSTERED INDEX [IX_Tenants_Status]
    ON [dbo].[Tenants] ([Status], [SubscriptionEndsUtc]) WHERE [IsDeleted] = 0;
GO
```

### 8.2 جدول `Stores`

```sql
CREATE TABLE [dbo].[Stores]
(
    [Code]             VARCHAR(20)   NOT NULL,   -- MAIN / ZRQ ... فريد داخل المستأجر
    [Name]             NVARCHAR(200) NOT NULL,
    [City]             NVARCHAR(120) NULL,
    [Address]          NVARCHAR(400) NULL,
    [Phone]            VARCHAR(32)   NULL,
    [TimeZone]         VARCHAR(64)   NOT NULL CONSTRAINT DF_Stores_TZ DEFAULT ('UTC'),
    [IsDefault]        BIT           NOT NULL CONSTRAINT DF_Stores_IsDefault DEFAULT (0),
    [IsActive]         BIT           NOT NULL CONSTRAINT DF_Stores_IsActive  DEFAULT (1),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT        IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT        NOT NULL,
    [StoreId]          BIGINT        NULL,        -- دائماً NULL هنا (الجدول نفسه هو الفرع)
    [CreatedDate]      DATETIME2(3)  NOT NULL CONSTRAINT DF_Stores_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT        NULL,
    [ModifiedDate]     DATETIME2(3)  NULL,
    [ModifiedBy]       BIGINT        NULL,
    [DeletedDate]      DATETIME2(3)  NULL,
    [DeletedBy]        BIGINT        NULL,
    [IsDeleted]        BIT           NOT NULL CONSTRAINT DF_Stores_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION    NOT NULL,

    CONSTRAINT [PK_Stores] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Stores_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO

-- تفرّد رمز الفرع داخل المستأجر
CREATE UNIQUE NONCLUSTERED INDEX [UX_Stores_Tenant_Code]
    ON [dbo].[Stores] ([TenantId], [Code]) WHERE [IsDeleted] = 0;
GO

-- فرع افتراضي واحد فقط لكل مستأجر
CREATE UNIQUE NONCLUSTERED INDEX [UX_Stores_Tenant_Default]
    ON [dbo].[Stores] ([TenantId])
    WHERE [IsDefault] = 1 AND [IsDeleted] = 0;
GO

-- فهرس العزل القياسي
CREATE NONCLUSTERED INDEX [IX_Stores_Tenant_Store]
    ON [dbo].[Stores] ([TenantId], [StoreId]) WHERE [IsDeleted] = 0;
GO
```

---

## 9) الـ API

| Method | Endpoint | الوصف | الصلاحية |
|--------|----------|-------|----------|
| `GET`  | `/api/platform/tenants` | قائمة المستأجرين (Paged) | `Platform.Tenants.View` |
| `POST` | `/api/platform/tenants` | Onboarding مستأجر جديد | `Platform.Tenants.Create` |
| `PATCH`| `/api/platform/tenants/{id}/status` | تفعيل/تعطيل/تعليق | `Platform.Tenants.Suspend` |
| `PUT`  | `/api/platform/tenants/{id}/subscription` | تعديل الخطّة والحدود | `Platform.Tenants.ManageSubscription` |
| `GET`  | `/api/stores` | فروع الشركة الحالية | `Stores.View` |
| `POST` | `/api/stores` | إنشاء فرع | `Stores.Create` |
| `PUT`  | `/api/stores/{id}` | تعديل فرع | `Stores.Edit` |
| `POST` | `/api/stores/{id}/set-default` | تعيين افتراضي | `Stores.Edit` |
| `PATCH`| `/api/stores/{id}/deactivate` | تعطيل فرع | `Stores.Deactivate` |

**مثال — إنشاء مستأجر (Request):**

```json
POST /api/platform/tenants
{
  "name": "متجر النور",
  "subdomain": "alnoor",
  "ownerEmail": "owner@alnoor.com",
  "businessType": "Supermarket",
  "baseCurrency": "JOD",
  "planCode": "PRO",
  "mainStore": { "name": "الفرع الرئيسي", "code": "MAIN", "city": "عمّان", "timeZone": "Asia/Amman" }
}
```

**Response (201):** يُعيد `publicId` للمستأجر ورابط الدخول `https://alnoor.smartpos.app` ورمز تفعيل حساب الـ Owner.

---

## 10) مخطط التدفّق (Flow Chart) — Onboarding مستأجر

```
[Platform Admin] → POST /tenants
        │
        ▼
[Validate] subdomain فريد؟ ─── لا ──► 409 Conflict
        │ نعم
        ▼
┌──────────── BEGIN TRANSACTION ────────────┐
│ 1. INSERT Tenants (Status=TRIAL, limits)  │
│ 2. INSERT Stores  (المتجر الرئيسي, Default)│
│ 3. INSERT User    (Tenant Owner)          │
│ 4. INSERT TenantSettings (JSON افتراضي)    │
│ 5. INSERT Sequences (INV-, PUR-, ...)     │
└──────────── COMMIT / ROLLBACK ────────────┘
        │
        ▼
[Send] بريد تفعيل للـ Owner  +  Audit Log
        │
        ▼
201 Created → { publicId, loginUrl }
```

---

## 11) ماذا يحدث عند: الحذف / التعديل / التعطيل

| الحدث | السلوك |
|-------|--------|
| **حذف مستأجر** | لا حذف فعلي أبداً — `Status=CANCELLED` + `IsDeleted=1` بعد فترة سماح. تبقى البيانات للأرشفة والامتثال المحاسبي. |
| **تعطيل مستأجر** | `IsActive=0`, `Status=SUSPENDED` — تُرفض جلسات الدخول، تبقى البيانات، لا نسخ احتياطية/تقارير مجدولة. |
| **إعادة تفعيل** | `Status=ACTIVE`, `IsActive=1` — يُعاد فتح الدخول فوراً. |
| **تعديل الحدود** | تحديث `MaxStores/MaxUsers/MaxProducts` يأخذ مفعوله فوراً على عمليات الإنشاء القادمة، ولا يحذف ما تجاوز الحدّ سابقاً (يمنع الزيادة فقط). |
| **تعطيل فرع** | `Store.IsActive=0` — تُخفى نقاط بيعه، لا تُقفل بياناته التاريخية. لا يمكن تعطيل آخر فرع فعّال أو الفرع الافتراضي الوحيد. |
| **تغيير الفرع الافتراضي** | يُطفأ `IsDefault` عن السابق ويُشعَل على الجديد في معاملة واحدة (يحرسها الـ Unique Filtered Index). |

---

## 12) سجل التدقيق (Audit Log)

كل عملية على `Tenants`/`Stores` تُسجَّل في `AuditLogs` بالحقول: `Actor (UserId)`, `Action (TENANT_CREATED/SUSPENDED/...)`, `EntityType`, `EntityId`, `OldValues`, `NewValues (JSON)`, `IpAddress`, `Timestamp (UTC)`.

- عمليات **Platform Admin** (خصوصاً `Suspend` و`Impersonate`) تُسجَّل إجبارياً مع **سبب نصّي** — تُراجَع دورياً.
- تغييرات الحدود تُسجَّل مع القيمة القديمة والجديدة لكل حدّ.

---

## 13) الأخطاء المحتملة (Possible Errors)

| الكود | السبب | الرسالة |
|------|-------|---------|
| `409 SUBDOMAIN_TAKEN` | الرمز مستخدَم | "الرمز مستخدَم، اختر رمزاً آخر" |
| `422 SUBDOMAIN_RESERVED` | رمز محجوز للنظام | "هذا الرمز محجوز" |
| `403 STORE_LIMIT_REACHED` | تجاوز `MaxStores` | "بلغت الحدّ الأقصى للفروع في خطّتك" |
| `409 STORE_CODE_DUPLICATE` | رمز فرع مكرّر داخل المستأجر | "رمز الفرع مكرّر" |
| `422 CANNOT_DISABLE_LAST_STORE` | تعطيل آخر فرع فعّال | "لا يمكن تعطيل الفرع الوحيد الفعّال" |
| `409 CONCURRENCY_CONFLICT` | تعديل متزامن (ROWVERSION) | "عُدّل السجل من جلسة أخرى، أعد التحميل" |

---

## 14) الأداء (Performance)

- توجيه الطلب للمستأجر يعتمد على `UX_Tenants_Subdomain` (بحث O(log n) على فهرس فريد) — يُخزَّن مؤقتاً (Cache) خريطة `Subdomain → TenantId` في الذاكرة/Redis لتفادي ضربة DB لكل طلب.
- قوائم الفروع صغيرة (عشرات لكل مستأجر) — تُحمَّل مع Cache على مستوى المستأجر وتُبطَل عند أي تعديل.
- فحوص حدود الاشتراك (عدّ الفروع/المستخدمين) تُخدَم من عدّادات مُخزَّنة أو `COUNT` مفهرس بـ `WHERE IsDeleted=0` لتجنّب المسح الكامل.

---

## 15) الأمان (Security)

1. **عزل صارم:** `Tenant Owner` لا يصل مطلقاً لبيانات مستأجر آخر — كل استعلام مفلتر بـ `TenantId` عبر Global Query Filter، مع RLS كطبقة دفاع ثانية (انظر [24-MultiTenant.md](24-MultiTenant.md)).
2. **فصل صلاحيات المنصّة:** `Platform.*` غير قابلة للمنح لأي مستخدم مستأجر — تُدار بحساب إداري منفصل.
3. **PublicId بدل Id:** المعرّفات المكشوفة خارجياً (روابط، API عامة) تستخدم `UNIQUEIDENTIFIER` لمنع IDOR وتخمين تسلسل المستأجرين.
4. **Impersonation مُدقَّق:** الدخول للدعم يتطلّب سبباً، يُسجَّل بالكامل، ومحدود زمنياً (قراءة أساساً).
5. **حجب الرموز المحجوزة:** قائمة `Subdomain` محجوزة تمنع اختطاف مسارات النظام (`admin`, `api`, `www`).

---

_يلتزم هذا الملف بالمرجع الحاكم [04-Database-Design.md](04-Database-Design.md). أي انحراف مبرَّر صراحةً أعلاه (مثل غياب `TenantId` من جدول `Tenants` نفسه)._
