# 31 — Subscription & Billing (دورة الاشتراك والفوترة)

> **الطبقة الأهم في أي SaaS.** هذا النظام هو ما تستخدمه **أنت (مالك المنصّة / Super Admin)** لإدارة عملائك المشتركين (المستأجرين) — لا ما يستخدمه المتجر. يحكم: من دفع؟ من انتهى اشتراكه؟ من يجب تعليقه؟ من يُجدَّد؟
>
> **مثال محوري:** محمد يدفع 50 ديناراً شهرياً → يُجدَّد اشتراكه → إذا لم يدفع تبدأ **فترة السماح (Grace)** → ثم **التعليق (Suspend)** → عند الدفع **يُعاد التفعيل (Renew)** — مع إشعارات في كل مرحلة.
>
> **النطاق (مقصود بسيط):** جدولان فقط (`Subscriptions` + `SubscriptionPayments`)، بلا بوابات دفع إلكترونية ولا فوترة معقّدة. هذا يكفي تماماً لإدارة الاشتراكات. التوسّع (Plans/Invoices/بوابات) مؤجّل لنسخة لاحقة — انظر [30-Future-Roadmap.md](30-Future-Roadmap.md).

---

## 1) الهدف من الصفحة (Purpose)

إدارة **العلاقة المالية** بين المنصّة وكل مستأجر (Tenant):

- ربط كل مستأجر بـ **اشتراك** له تاريخ بداية ونهاية.
- تتبّع **حالة الاشتراك** (نشط، فترة سماح، معلّق، ملغى).
- تسجيل **الدفعات** وربطها بالاشتراك.
- تطبيق **فترة السماح** ثم **التعليق التلقائي** عند عدم الدفع.
- **إعادة التفعيل** عند السداد، مع إشعارات في كل مرحلة.

> **لماذا هذا محوري:** بدونه لا توجد إيرادات مُدارة — المنصّة تعطي خدمة بلا ضابط. لا يقل أهمية عن نظام المبيعات نفسه.

---

## 2) الموقع المعماري (Architectural Placement)

نظام **Platform-Level** يقع **فوق** طبقة المستأجرين:

```
┌─────────────────────────────────────────────────────────┐
│  PLATFORM LAYER  (يديره Super Admin — أنت)              │
│  ┌───────────────────────────────────────────────────┐  │
│  │  Subscriptions · SubscriptionPayments  ← هذا الملف │  │
│  └───────────────────────────────────────────────────┘  │
│                        │ يتحكّم بوصول                     │
│                        ▼                                 │
│  ┌───────────────────────────────────────────────────┐  │
│  │  TENANT LAYER  (المتاجر — بقية التوثيق 01→30)      │  │
│  │  Tenant A · Tenant B · Tenant C ...                │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

**فرق جوهري:** `TenantId` في هذين الجدولين هو **مفتاح خارجي يشير للمستأجر المُدار** (العميل)، لا مفتاح عزل. يملكهما Super Admin ولا يخضعان لـ Global Query Filter للمستأجر (استثناء مبرَّر — البند 2 في [04-Database-Design.md](04-Database-Design.md)، مثل جدول `Tenants` نفسه).

---

## 3) دورة حياة الاشتراك (Subscription Lifecycle) — قلب النظام

### الحالات (`Status`)

| الحالة | الوصف | الوصول للنظام |
|--------|-------|:-------------:|
| `Active` | مدفوع وساري (`EndDate` في المستقبل) | ✅ كامل |
| `Grace` | انتهى `EndDate` ولم يُدفع — ضمن مهلة `GraceDays` | ✅ كامل (مع تحذير) |
| `Suspended` | انقضت فترة السماح دون دفع | ❌ محظور (شاشة تجديد فقط) |
| `Cancelled` | أُلغي (بطلب المستأجر أو المنصّة) | ❌ محظور |

### مخطط الانتقالات (State Machine)

```
   ┌────────────────────────► ┌──────────┐  انتهى EndDate ولم يُدفع
   │       دفع / تجديد        │  Active  │──────────────┐
   │                          └────┬─────┘              ▼
   │                               │              ┌──────────┐
   │  دفع خلال المهلة              │              │  Grace   │ (خلال GraceDays)
   └───────────────────────────────┤              │ (سماح)   │
   │                                │              └────┬─────┘
   │                                │                   │ انقضت GraceDays دون دفع
   │                                │                   ▼
   │      دفع (Reactivate)          │              ┌──────────┐
   └────────────────────────────────┴──────────────│Suspended │ ❌ الوصول محظور
                                                    │ (معلّق)  │
                                                    └────┬─────┘
                                                         │ إلغاء
                                                         ▼
                                                  ┌──────────┐
                                                  │Cancelled │
                                                  └──────────┘
```

### الجدول الزمني الافتراضي (Timeline)

```
StartDate     EndDate        EndDate + GraceDays        + N يوم
   │             │                   │                     │
   ▼             ▼                   ▼                     ▼
 Active   انتهاء الاشتراك         Suspend               Cancel
          → Grace (سماح)         (معلّق)              (إلغاء نهائي)
```

`GraceDays` قابل للإعداد لكل اشتراك. `NextPaymentDate` = `EndDate` (تاريخ الاستحقاق القادم).

---

## 4) صلاحيات الدخول (Access Permissions)

| الدور | الصلاحية |
|-------|----------|
| **Super Admin** (مالك المنصّة) | `Platform.Subscriptions.*` — إدارة كاملة |
| **Company Owner** (المستأجر) | `Subscription.ViewOwn` — عرض اشتراكه ودفعاته فقط |
| باقي الأدوار | ❌ لا وصول |

يعتمد نموذج `Resource.Action` من [06-Roles-And-Permissions.md](06-Roles-And-Permissions.md).

---

## 5) قواعد العمل (Business Rules)

1. **كل مستأجر له اشتراك واحد حيّ كحدّ أقصى** (غير الملغى) في أي لحظة.
2. **فترة السماح إلزامية** قبل التعليق — لا يُعلَّق مستأجر فور انتهاء `EndDate`.
3. **التعليق لا يحذف البيانات** — يمنع الوصول فقط (Reactivate يعيد كل شيء كما كان).
4. **تسجيل دفعة يُمدّد الاشتراك:** يحدّث `EndDate` و`LastPaymentDate` و`NextPaymentDate`، ويعيد الحالة إلى `Active`.
5. **حالة الاشتراك تُفحص عند كل طلب** عبر Middleware — المعلّق يُوجَّه لشاشة التجديد ([24-MultiTenant.md](24-MultiTenant.md)).
6. **`PlanName` نصّي** (Basic/Pro/Enterprise) — لا حاجة لجدول Plans منفصل في هذه النسخة.

---

## 6) جداول قاعدة البيانات (Database Tables)

> تلتزم بالأعمدة المشتركة ومعايير [04-Database-Design.md](04-Database-Design.md): `DECIMAL(18,4)` للأموال، `DATETIME2(3)` UTC، Soft Delete، `ConcurrencyStamp ROWVERSION`. **استثناء مبرَّر:** لا Global Query Filter بـ TenantId (جداول Platform يملكها Super Admin).

### 6.1 — Subscriptions (الاشتراكات)

```sql
CREATE TABLE [dbo].[Subscriptions]
(
    [SubscribedTenantId] BIGINT       NOT NULL,   -- المستأجر المُشترِك (FK → Tenants)
    [PlanName]           NVARCHAR(100) NOT NULL,   -- 'Basic' | 'Pro' | 'Enterprise' (نصّي)
    [StartDate]          DATETIME2(3) NOT NULL,
    [EndDate]            DATETIME2(3) NOT NULL,     -- تاريخ انتهاء الفترة الحالية
    [GraceDays]          INT          NOT NULL CONSTRAINT DF_Subs_GraceDays DEFAULT (7),
    [Status]             VARCHAR(20)  NOT NULL CONSTRAINT DF_Subs_Status DEFAULT ('Active'),
                                                   -- Active | Grace | Suspended | Cancelled
    [Notes]              NVARCHAR(500) NULL,
    [LastPaymentDate]    DATETIME2(3) NULL,         -- آخر دفعة مسجَّلة
    [NextPaymentDate]    DATETIME2(3) NULL,         -- الاستحقاق القادم (= EndDate عادةً)

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NULL,           -- طبقة المنصّة (لا عزل مستأجر)
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_Subs_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_Subs_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_Subscriptions] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Subs_Tenant] FOREIGN KEY ([SubscribedTenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [CK_Subs_Status] CHECK ([Status] IN ('Active','Grace','Suspended','Cancelled')),
    CONSTRAINT [CK_Subs_Dates]  CHECK ([EndDate] >= [StartDate]),
    CONSTRAINT [CK_Subs_Grace]  CHECK ([GraceDays] >= 0)
);
GO

-- مستأجر واحد له اشتراك حيّ واحد فقط (غير الملغى)
CREATE UNIQUE NONCLUSTERED INDEX [UX_Subscriptions_ActivePerTenant]
    ON [dbo].[Subscriptions] ([SubscribedTenantId])
    WHERE [IsDeleted] = 0 AND [Status] IN ('Active','Grace','Suspended');
GO

-- فهرس للـ Background Job: إيجاد الاشتراكات المستحقّة للمعالجة
CREATE NONCLUSTERED INDEX [IX_Subscriptions_DueForProcessing]
    ON [dbo].[Subscriptions] ([Status], [EndDate])
    WHERE [IsDeleted] = 0;
GO
```

### 6.2 — SubscriptionPayments (دفعات الاشتراك)

```sql
CREATE TABLE [dbo].[SubscriptionPayments]
(
    [SubscriptionId]   BIGINT       NOT NULL,
    [Amount]           DECIMAL(18,4) NOT NULL,
    [PaidDate]         DATETIME2(3) NOT NULL,
    [PaymentMethod]    VARCHAR(30)  NOT NULL,      -- 'Cash' | 'BankTransfer' | 'Card' | 'Cheque'
    [ReferenceNumber]  NVARCHAR(100) NULL,          -- رقم التحويل/الشيك/المرجع
    [Notes]            NVARCHAR(500) NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_SubPay_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_SubPay_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_SubscriptionPayments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SubPay_Sub] FOREIGN KEY ([SubscriptionId]) REFERENCES [dbo].[Subscriptions]([Id]),
    CONSTRAINT [CK_SubPay_Amount] CHECK ([Amount] > 0)
);
GO

CREATE NONCLUSTERED INDEX [IX_SubPay_Subscription]
    ON [dbo].[SubscriptionPayments] ([SubscriptionId], [PaidDate] DESC)
    WHERE [IsDeleted] = 0;
GO
```

---

## 7) الوظائف المجدولة (Background Jobs) — المحرّك الأوتوماتيكي

تُشغَّل عبر **Hangfire** ([29-Performance.md](29-Performance.md))، كلها بتوقيت UTC:

| الوظيفة | التكرار | المنطق |
|---------|---------|--------|
| **`GraceJob`** | يومياً | لكل اشتراك `Active` تجاوز `EndDate`: انقله إلى `Grace` (يبقى الوصول متاحاً)، وأرسل إشعار انتهاء. |
| **`SuspendJob`** | يومياً | لكل اشتراك `Grace` تجاوز `EndDate + GraceDays`: انقله إلى `Suspended`، احظر الوصول، أرسل إشعار تعليق. |
| **`ReminderJob`** | يومياً | إشعارات ما قبل الانتهاء (7/3/1 أيام) وتذكيرات أثناء `Grace`. |

كل وظيفة **idempotent** (تُعيد نفس النتيجة لو أُعيد تشغيلها) وتعمل داخل معاملة لكل اشتراك.

**تسجيل دفعة (يدوياً من Super Admin) يعيد التفعيل:**
```
INSERT SubscriptionPayments (...)
UPDATE Subscriptions SET
    Status = 'Active',
    LastPaymentDate = @PaidDate,
    EndDate = DATEADD(MONTH, 1, EndDate),   -- تمديد فترة (شهر مثلاً)
    NextPaymentDate = DATEADD(MONTH, 1, EndDate)
WHERE Id = @SubscriptionId;
-- كل ذلك داخل معاملة واحدة
```

---

## 8) الـ API

| Method | Endpoint | الوصف | الصلاحية |
|--------|----------|-------|----------|
| `GET` | `/api/v1/platform/subscriptions` | كل الاشتراكات (فلترة/بحث/ترقيم) | Super Admin |
| `GET` | `/api/v1/platform/subscriptions/{id}` | تفاصيل اشتراك + دفعاته | Super Admin |
| `POST` | `/api/v1/platform/subscriptions` | إنشاء اشتراك لمستأجر | Super Admin |
| `POST` | `/api/v1/platform/subscriptions/{id}/payments` | تسجيل دفعة (يمدّد ويعيد التفعيل) | Super Admin |
| `POST` | `/api/v1/platform/subscriptions/{id}/suspend` | تعليق يدوي | Super Admin |
| `POST` | `/api/v1/platform/subscriptions/{id}/cancel` | إلغاء | Super Admin |
| `GET` | `/api/v1/subscription/me` | اشتراكي الحالي وحالته | Company Owner |
| `GET` | `/api/v1/subscription/me/payments` | سجل دفعاتي | Company Owner |

**مثال — تسجيل دفعة (Response):**

```json
{
  "success": true,
  "data": {
    "subscriptionId": 4102,
    "status": "Active",
    "lastPaymentDate": "2026-07-14T09:12:00Z",
    "endDate": "2026-08-14T00:00:00Z",
    "nextPaymentDate": "2026-08-14T00:00:00Z"
  }
}
```

يتبع envelope الموحّد من [25-API-Design.md](25-API-Design.md).

---

## 9) الإشعارات (Notifications)

عبر نظام [22-Notifications.md](22-Notifications.md)، تُرسَل للمستأجر (Company Owner):

| الحدث | القناة | التوقيت |
|-------|--------|---------|
| قرب انتهاء الاشتراك | Email + in-app | قبل 7 / 3 / 1 يوم |
| دخول فترة السماح (Grace) | Email + in-app | فور انتهاء `EndDate` |
| تذكير أثناء السماح | Email + in-app | يومياً حتى نهاية Grace |
| التعليق (Suspend) | Email + in-app | فور التعليق |
| إعادة التفعيل بعد الدفع | Email + in-app | فور تسجيل الدفعة |

---

## 10) ماذا يحدث عند... (State Impact)

| الحدث | الأثر |
|-------|-------|
| **انتهاء `EndDate`** | `Active → Grace`، إشعار، **الوصول يبقى متاحاً** (مع تحذير). |
| **انقضاء فترة السماح** | `Grace → Suspended`، حظر الوصول، توجيه لشاشة التجديد. **البيانات محفوظة.** |
| **تسجيل دفعة** | تمديد `EndDate`، تحديث `LastPaymentDate`/`NextPaymentDate`، الحالة → `Active`. |
| **الدفع بعد التعليق (Reactivate)** | `Suspended → Active`، **استعادة الوصول الكامل فوراً** كما كان (لا فقدان بيانات). |
| **الإلغاء** | `→ Cancelled`، حظر الوصول. |
| **حذف مستأجر** | soft delete + جدولة حذف نهائي بعد retention ([28-Backup-And-Restore.md](28-Backup-And-Restore.md)). |

---

## 11) سجل التدقيق (Audit Log)

كل انتقال حالة وكل دفعة تُسجَّل في `AuditLogs` ([27-Security.md](27-Security.md)):
`SubscriptionCreated`, `PaymentRecorded`, `EnteredGrace`, `Suspended`, `Reactivated`, `Cancelled` — مع المستخدم/الوقت/القيم قبل وبعد.

---

## 12) الأخطاء المحتملة (Possible Errors)

| الخطأ | السبب | المعالجة |
|-------|-------|----------|
| `SUB_ALREADY_ACTIVE` | إنشاء اشتراك ثانٍ لمستأجر له اشتراك حيّ | رفض (يفرضه الـ Unique Index) |
| `SUB_SUSPENDED_ACCESS` | مستأجر معلّق يحاول استخدام النظام | توجيه لشاشة التجديد (Middleware) |
| `INVALID_PAYMENT_AMOUNT` | مبلغ ≤ 0 | رفض (يفرضه `CK_SubPay_Amount`) |
| `SUB_CANCELLED` | تسجيل دفعة لاشتراك ملغى | رفض (يتطلّب إنشاء اشتراك جديد) |

---

## 13) الأداء (Performance)

- الوظائف المجدولة تعالج على دفعات عبر فهرس `IX_Subscriptions_DueForProcessing` — لا تفحص كل السجلات.
- **فحص الحالة عند كل طلب** يُخزَّن مؤقتاً (cache) لدقائق لتفادي استعلام DB على كل request ([29-Performance.md](29-Performance.md)).

---

## 14) الأمان (Security)

- جداول Platform **معزولة عن استعلامات المستأجرين** — لا يصل مستأجر لبيانات اشتراك مستأجر آخر (فحص `SubscribedTenantId == currentTenant` لمسارات `/me`).
- عمليات Super Admin محميّة بصلاحية `Platform.Subscriptions.*` + تدقيق كامل.
- منع IDOR: مسارات `/subscription/me/*` تشتقّ المستأجر من الـ JWT لا من معطى العميل.
- المبالغ والحالات تُتحقَّق خادمياً — لا يُوثَق بأي قيمة من الواجهة.

---

## 15) العلاقة ببقية النظام (Integration Points)

| يرتبط بـ | الغرض |
|----------|-------|
| [07-Store-Management.md](07-Store-Management.md) / `Tenants` | كل اشتراك يخصّ مستأجراً |
| [22-Notifications.md](22-Notifications.md) | إشعارات دورة الحياة |
| [24-MultiTenant.md](24-MultiTenant.md) | Middleware فحص الحالة يوجّه المعلّق |
| [25-API-Design.md](25-API-Design.md) | envelope موحّد |
| [29-Performance.md](29-Performance.md) | Hangfire jobs + caching |

---

_ملاحظة نطاق: هذا نموذج **مبسّط مقصود** (جدولان، `PlanName` نصّي، بلا فواتير/بوابات دفع). كافٍ تماماً لإدارة الاشتراكات. التوسّع لاحقاً (جدول Plans، فواتير رسمية، بوابات Stripe/HyperPay، proration، dunning آلي) — انظر [30-Future-Roadmap.md](30-Future-Roadmap.md)._
