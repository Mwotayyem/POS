# 23 — Settings (إعدادات الشركة والمتجر)

> شاشة **الإعدادات** هي قلب مبدأ *"Configuration over Code"*: كل اختلاف بين العملاء والقطاعات يُحلّ بالإعداد لا بفرع كود ولا بإعادة نشر. تغطّي: الضرائب، العملات، قوالب الفواتير/الإيصالات، البريد، SMS، WhatsApp، الطابعة، النسخ الاحتياطي/الاستعادة، السمة (Theme)، والشعار (Logo).

> يلتزم بالمرجع الحاكم [04-Database-Design.md](04-Database-Design.md) — خصوصاً **قاعدة JSON**: يُسمح بـ `NVARCHAR(MAX)` مع `ISJSON()` check للإعدادات المرنة فقط، ويُمنع منعاً باتاً تخزين البيانات المحاسبية كـ JSON. مرتبط بـ [24-MultiTenant.md](24-MultiTenant.md) (التخصيص) و[28-Backup-And-Restore.md](28-Backup-And-Restore.md).

---

## 1) الهدف من الصفحة (Purpose)

تمكين **Tenant Owner** و**Store Manager** من ضبط سلوك النظام ومظهره لشركتهم/فرعهم دون أي تدخّل هندسي:

- **إعدادات على مستوى الشركة (Tenant-level):** العملة الأساسية، الضرائب، البريد، الرسائل، السمة، الشعار — تُطبَّق على كل الفروع افتراضياً.
- **إعدادات على مستوى الفرع (Store-level):** قالب الإيصال، الطابعة، ترويسة الفاتورة، خيارات محلية — **تُورَّث** من الشركة ويمكن تجاوزها (Override) لكل فرع.

مبدأ التوريث: `StoreSettings` يتجاوز `TenantSettings` عند التعارض؛ غياب مفتاح على مستوى الفرع = استخدام قيمة الشركة.

---

## 1.1) فصل مسؤوليات جداول الإعدادات (Settings Ownership) — مهم

> **تنبيه معماري:** لتفادي أي تداخل، هذه هي الحدود الصارمة لمسؤولية كل جدول إعدادات. **لا يوجد جدول عام باسم `Settings`** — الاسم `Settings.*` يظهر فقط كـ **مجموعة صلاحيات** (Permission group)، لا كجدول.

| الجدول | النطاق | يملكه | يحتوي | لا يحتوي |
|--------|--------|-------|-------|----------|
| **`TenantSettings`** | الشركة كاملة (كل الفروع) | Tenant Owner | العملة الأساسية، البريد/SMS/WhatsApp، السمة، الشعار، سياسات عامة | أي شيء خاص بفرع واحد |
| **`StoreSettings`** | فرع واحد فقط | Store Manager | قالب الإيصال، الطابعة، ترويسة الفاتورة، خيارات الفرع المحلية | إعدادات على مستوى الشركة |
| **`TaxRates`** | الشركة | Accountant | نسب الضريبة (بيانات علائقية، **لا JSON**) | إعدادات غير ضريبية |
| **`Currencies`** | الشركة | Accountant | العملات وأسعار الصرف (علائقي) | — |
| **`PlatformSettings`** *(إن وُجد)* | المنصّة كلها | Super Admin | إعدادات النظام العامة فوق كل المستأجرين | أي بيانات مستأجر |

**قواعد الحسم عند الالتباس:**
1. هل الإعداد يخصّ **فرعاً واحداً**؟ → `StoreSettings`. وإلا → `TenantSettings`.
2. هل هو **بيان محاسبي** (ضريبة/عملة)؟ → جدول علائقي مخصّص (`TaxRates`/`Currencies`)، **لا** JSON في Settings.
3. هل يخصّ **المنصّة فوق كل المستأجرين**؟ → طبقة Platform، لا جداول المستأجر.
4. القيمة الفعّالة = دمج `TenantSettings` (أساس) ← `StoreSettings` (تجاوز) — انظر §8.

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية | الوصف | يملكها |
|----------|-------|--------|
| `Settings.Tenant.View` | عرض إعدادات الشركة | Tenant Owner, Accountant |
| `Settings.Tenant.Edit` | تعديل إعدادات الشركة | Tenant Owner |
| `Settings.Store.View` | عرض إعدادات الفرع | Store Manager |
| `Settings.Store.Edit` | تعديل إعدادات الفرع | Store Manager |
| `Settings.Taxes.Manage` | إدارة الضرائب | Tenant Owner, Accountant |
| `Settings.Currencies.Manage` | إدارة العملات وأسعار الصرف | Tenant Owner, Accountant |
| `Settings.Backup.Run` | تشغيل نسخة احتياطية يدوية | Tenant Owner |
| `Settings.Restore.Run` | استعادة نسخة | Platform Admin + موافقة Owner |

> **الاستعادة (Restore)** مقيّدة بأعلى مستوى: عملية خطرة تتطلّب موافقة مزدوجة وتُسجَّل بالكامل (انظر §11 و[28-Backup-And-Restore.md](28-Backup-And-Restore.md)).

---

## 3) تصميم الصفحة (Page Layout)

تبويبات جانبية (Tabbed):

```
┌───────────────────────┬──────────────────────────────────────────────┐
│  الإعدادات             │   [ عام | الضرائب | العملات | القوالب |        │
│                       │     البريد | SMS/WhatsApp | الطابعة |          │
│  ▸ عام (General)       │     السمة والشعار | النسخ الاحتياطي ]          │
│  ▸ الضرائب (Taxes)     ├──────────────────────────────────────────────┤
│  ▸ العملات            │                                              │
│  ▸ القوالب            │   [ محتوى التبويب المختار ]                    │
│  ▸ البريد الإلكتروني  │                                              │
│  ▸ SMS / WhatsApp     │   🏢 مستوى: [الشركة ▾] / [الفرع: MAIN ▾]      │
│  ▸ الطابعة            │                                              │
│  ▸ السمة والشعار      │                    [ حفظ ]  [ استعادة الافتراضي ]│
│  ▸ النسخ الاحتياطي    │                                              │
└───────────────────────┴──────────────────────────────────────────────┘
```

مبدّل **المستوى (Scope Switcher)** أعلى المحتوى يحدّد هل نحرّر إعداد الشركة أم إعداد فرع محدّد.

---

## 4) جميع الأزرار (Buttons)

| الزر | الإجراء | الصلاحية | تأكيد؟ |
|------|---------|----------|--------|
| **حفظ** | يحفظ JSON الإعداد للمستوى المختار | `Settings.*.Edit` | — |
| **استعادة الافتراضي** | يعيد التبويب لقيم النظام الافتراضية | `Settings.*.Edit` | ✅ |
| **إرسال بريد اختبار** | يجرّب إعداد SMTP فعلياً | `Settings.Tenant.Edit` | — |
| **إرسال SMS/WhatsApp اختبار** | يجرّب مزوّد الرسائل | `Settings.Tenant.Edit` | — |
| **معاينة الإيصال** | يعرض قالب الطباعة ببيانات وهمية | `Settings.Store.View` | — |
| **+ ضريبة / + عملة** | يضيف صفاً في `TaxRates`/`Currencies` | `Settings.Taxes/Currencies.Manage` | — |
| **رفع الشعار / الخلفية** | يرفع صورة (تُخزَّن كأصل، والمسار في JSON) | `Settings.Tenant.Edit` | — |
| **نسخة احتياطية الآن** | يشغّل Backup فوري | `Settings.Backup.Run` | ✅ |
| **استعادة** | يستعيد من نسخة | `Settings.Restore.Run` | ✅✅ |

---

## 5) جميع الحقول (Fields)

### 5.1 عام (General)

| الحقل | النوع | ملاحظة |
|-------|------|--------|
| اسم النظام المعروض | نص | يظهر في العنوان (تخصيص العلامة) |
| العملة الأساسية | قائمة | من `Currencies` |
| المنطقة الزمنية الافتراضية | قائمة | لعرض تواريخ UTC |
| تنسيق التاريخ/الأرقام | قائمة | Locale |
| اللغة الافتراضية | قائمة | ar / en (RTL أصلي) |

### 5.2 البريد (Email / SMTP)

| الحقل | النوع | ملاحظة |
|-------|------|--------|
| `SmtpHost` / `SmtpPort` | نص/رقم | — |
| `UseSsl` | تبديل | — |
| `Username` | نص | — |
| `Password` | سرّي | **يُشفَّر** ولا يُعاد بالـ API |
| `FromName` / `FromEmail` | نص | ترويسة الرسائل |

### 5.3 SMS / WhatsApp

| الحقل | النوع | ملاحظة |
|-------|------|--------|
| `Provider` | قائمة | Twilio / UnifonicWA / Meta Cloud API |
| `ApiKey` / `Sender` | سرّي/نص | **مُشفَّر** |
| قوالب الرسائل | JSON | نصوص متغيّرة `{{customer}}`, `{{invoice}}` |

### 5.4 الطابعة والقالب

| الحقل | النوع | ملاحظة |
|-------|------|--------|
| نوع الطابعة | قائمة | Thermal 58mm / 80mm / A4 |
| قالب الإيصال | JSON/HTML | ترويسة، شعار، حقول، تذييل |
| فتح الدرج النقدي | تبديل | ESC/POS |

---

## 6) التحقق (Validation)

| القاعدة | الرسالة |
|---------|---------|
| كل حقل JSON يمرّ فحص `ISJSON()=1` على مستوى DB | "بنية الإعداد غير صالحة" |
| `SmtpPort` بين 1–65535 | "منفذ SMTP غير صالح" |
| نسبة الضريبة (`Rate`) بين 0 و 100 | "نسبة الضريبة يجب أن تكون 0–100" |
| رمز العملة `CHAR(3)` ISO-4217 صالح | "رمز عملة غير صالح" |
| الشعار: صيغة PNG/JPG/SVG وحجم ≤ 2MB | "صيغة أو حجم الشعار غير مقبول" |
| ضريبة/عملة افتراضية واحدة فقط | "يوجد افتراضي بالفعل" |

> الأسرار (كلمات مرور SMTP، مفاتيح API) تُشفَّر بـ **Data Protection API / Key Vault** قبل التخزين، ولا تُرجَع أبداً في استجابات القراءة (تُقنَّع كـ `••••`).

---

## 7) قواعد العمل (Business Rules)

1. **التوريث والتجاوز:** القراءة الفعّالة لإعداد = **دمج (Merge)** `TenantSettings` (الأساس) فوقه `StoreSettings` (التجاوز). يُنفَّذ الدمج في طبقة التطبيق ويُخزَّن مؤقتاً لكل `(TenantId, StoreId)`.
2. **لا إعادة نشر:** كل تغيير إعداد يأخذ مفعوله **فوراً** بإبطال الـ Cache — لا حاجة لإعادة تشغيل التطبيق ولا نشر.
3. **الضرائب مطبَّعة علائقياً:** نسب الضريبة تُخزَّن في جدول `TaxRates` (لا JSON) لأنها تدخل في حسابات الفواتير — والمرجع الحاكم يمنع تخزين البيانات المحاسبية كـ JSON. قالب الإيصال (عرضي، غير محاسبي) يُسمح بتخزينه JSON.
4. **أسعار الصرف تاريخية:** تغيير سعر صرف لا يعدّل الفواتير القديمة — الفاتورة تحفظ سعر الصرف لحظة إصدارها (Snapshot).
5. **قوالب النظام الافتراضية:** لكل تبويب قيم Default مُهيّأة عند Onboarding — "استعادة الافتراضي" يعيدها دون حذف الصف (يستبدل الـ JSON).
6. **النسخ الاحتياطي مجدول:** إعداد `Backup` يضبط جدولة (Cron) لوظيفة Hangfire؛ فشلها يولّد إشعار `Backup Failure` (انظر [22-Notifications.md](22-Notifications.md)).

---

## 8) جداول قاعدة البيانات (Database Tables)

### 8.1 `TenantSettings` — إعدادات الشركة (JSON مرن)

```sql
CREATE TABLE [dbo].[TenantSettings]
(
    [Category]         VARCHAR(40)    NOT NULL,   -- GENERAL / EMAIL / SMS / THEME / BACKUP / TEMPLATE ...
    [SettingsJson]     NVARCHAR(MAX)  NOT NULL,   -- محتوى مرن لكل فئة

    -- ===== الأعمدة المشتركة (StoreId = NULL دائماً هنا) =====
    [Id]               BIGINT        IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT        NOT NULL,
    [StoreId]          BIGINT        NULL,
    [CreatedDate]      DATETIME2(3)  NOT NULL CONSTRAINT DF_TenantSettings_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT        NULL,
    [ModifiedDate]     DATETIME2(3)  NULL,
    [ModifiedBy]       BIGINT        NULL,
    [DeletedDate]      DATETIME2(3)  NULL,
    [DeletedBy]        BIGINT        NULL,
    [IsDeleted]        BIT           NOT NULL CONSTRAINT DF_TenantSettings_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION    NOT NULL,

    CONSTRAINT [PK_TenantSettings] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_TenantSettings_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [CK_TenantSettings_Json]
        CHECK ([SettingsJson] IS NULL OR ISJSON([SettingsJson]) = 1)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_TenantSettings_Tenant_Category]
    ON [dbo].[TenantSettings] ([TenantId], [Category]) WHERE [IsDeleted] = 0;
GO
```

### 8.2 `StoreSettings` — تجاوزات على مستوى الفرع (JSON مرن)

```sql
CREATE TABLE [dbo].[StoreSettings]
(
    [Category]         VARCHAR(40)    NOT NULL,   -- TEMPLATE / PRINTER / GENERAL ...
    [SettingsJson]     NVARCHAR(MAX)  NOT NULL,   -- يتجاوز نظيره في TenantSettings

    -- ===== الأعمدة المشتركة (StoreId = NOT NULL منطقياً) =====
    [Id]               BIGINT        IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT        NOT NULL,
    [StoreId]          BIGINT        NOT NULL,     -- خاص بفرع
    [CreatedDate]      DATETIME2(3)  NOT NULL CONSTRAINT DF_StoreSettings_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT        NULL,
    [ModifiedDate]     DATETIME2(3)  NULL,
    [ModifiedBy]       BIGINT        NULL,
    [DeletedDate]      DATETIME2(3)  NULL,
    [DeletedBy]        BIGINT        NULL,
    [IsDeleted]        BIT           NOT NULL CONSTRAINT DF_StoreSettings_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION    NOT NULL,

    CONSTRAINT [PK_StoreSettings] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_StoreSettings_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_StoreSettings_Store]  FOREIGN KEY ([StoreId])  REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [CK_StoreSettings_Json]
        CHECK ([SettingsJson] IS NULL OR ISJSON([SettingsJson]) = 1)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_StoreSettings_Store_Category]
    ON [dbo].[StoreSettings] ([TenantId], [StoreId], [Category]) WHERE [IsDeleted] = 0;
GO
```

### 8.3 `TaxRates` — نسب الضرائب (علائقي — محاسبي)

```sql
CREATE TABLE [dbo].[TaxRates]
(
    [Code]             VARCHAR(20)   NOT NULL,   -- VAT16 / EXEMPT / ZERO
    [Name]             NVARCHAR(100) NOT NULL,   -- ضريبة القيمة المضافة
    [Rate]             DECIMAL(9,4)  NOT NULL,   -- 16.0000 (نسبة مئوية)
    [IsInclusive]      BIT           NOT NULL CONSTRAINT DF_TaxRates_Incl DEFAULT (0),
    [IsDefault]        BIT           NOT NULL CONSTRAINT DF_TaxRates_Def  DEFAULT (0),
    [IsActive]         BIT           NOT NULL CONSTRAINT DF_TaxRates_Act  DEFAULT (1),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT        IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT        NOT NULL,
    [StoreId]          BIGINT        NULL,        -- NULL = على مستوى الشركة
    [CreatedDate]      DATETIME2(3)  NOT NULL CONSTRAINT DF_TaxRates_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT        NULL,
    [ModifiedDate]     DATETIME2(3)  NULL,
    [ModifiedBy]       BIGINT        NULL,
    [DeletedDate]      DATETIME2(3)  NULL,
    [DeletedBy]        BIGINT        NULL,
    [IsDeleted]        BIT           NOT NULL CONSTRAINT DF_TaxRates_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION    NOT NULL,

    CONSTRAINT [PK_TaxRates] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_TaxRates_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_TaxRates_Store]  FOREIGN KEY ([StoreId])  REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [CK_TaxRates_Rate]   CHECK ([Rate] >= 0 AND [Rate] <= 100)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_TaxRates_Tenant_Code]
    ON [dbo].[TaxRates] ([TenantId], [Code]) WHERE [IsDeleted] = 0;
GO
-- ضريبة افتراضية واحدة لكل مستأجر
CREATE UNIQUE NONCLUSTERED INDEX [UX_TaxRates_Tenant_Default]
    ON [dbo].[TaxRates] ([TenantId])
    WHERE [IsDefault] = 1 AND [IsDeleted] = 0;
GO
```

### 8.4 `Currencies` — العملات وأسعار الصرف

```sql
CREATE TABLE [dbo].[Currencies]
(
    [IsoCode]          CHAR(3)       NOT NULL,   -- USD / JOD / EUR (ISO-4217)
    [Name]             NVARCHAR(80)  NOT NULL,
    [Symbol]           NVARCHAR(8)   NOT NULL,   -- $ / د.أ
    [DecimalPlaces]    TINYINT       NOT NULL CONSTRAINT DF_Currencies_Dec DEFAULT (2),
    [ExchangeRate]     DECIMAL(18,6) NOT NULL,   -- مقابل العملة الأساسية
    [IsBase]           BIT           NOT NULL CONSTRAINT DF_Currencies_Base DEFAULT (0),
    [IsActive]         BIT           NOT NULL CONSTRAINT DF_Currencies_Act  DEFAULT (1),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT        IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT        NOT NULL,
    [StoreId]          BIGINT        NULL,
    [CreatedDate]      DATETIME2(3)  NOT NULL CONSTRAINT DF_Currencies_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT        NULL,
    [ModifiedDate]     DATETIME2(3)  NULL,
    [ModifiedBy]       BIGINT        NULL,
    [DeletedDate]      DATETIME2(3)  NULL,
    [DeletedBy]        BIGINT        NULL,
    [IsDeleted]        BIT           NOT NULL CONSTRAINT DF_Currencies_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION    NOT NULL,

    CONSTRAINT [PK_Currencies] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Currencies_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [CK_Currencies_Rate]   CHECK ([ExchangeRate] > 0)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Currencies_Tenant_Iso]
    ON [dbo].[Currencies] ([TenantId], [IsoCode]) WHERE [IsDeleted] = 0;
GO
-- عملة أساسية واحدة لكل مستأجر
CREATE UNIQUE NONCLUSTERED INDEX [UX_Currencies_Tenant_Base]
    ON [dbo].[Currencies] ([TenantId])
    WHERE [IsBase] = 1 AND [IsDeleted] = 0;
GO
```

**مثال محتوى `SettingsJson` لفئة `THEME`:**

```json
{
  "primaryColor": "#0F766E",
  "sidebarStyle": "dark",
  "logoUrl": "/assets/tenant/1001/logo.png",
  "loginBackgroundUrl": "/assets/tenant/1001/bg.jpg",
  "systemName": "متجر النور - نقاط البيع"
}
```

---

## 9) الـ API

| Method | Endpoint | الوصف | الصلاحية |
|--------|----------|-------|----------|
| `GET`  | `/api/settings/{category}?storeId=` | قراءة الإعداد الفعّال (مدموج) | `Settings.*.View` |
| `PUT`  | `/api/settings/{category}` | حفظ إعداد شركة/فرع | `Settings.*.Edit` |
| `POST` | `/api/settings/email/test` | إرسال بريد اختبار | `Settings.Tenant.Edit` |
| `GET`  | `/api/tax-rates` / `POST` / `PUT` | إدارة الضرائب | `Settings.Taxes.Manage` |
| `GET`  | `/api/currencies` / `POST` / `PUT` | إدارة العملات | `Settings.Currencies.Manage` |
| `POST` | `/api/settings/logo` | رفع الشعار | `Settings.Tenant.Edit` |
| `POST` | `/api/backups/run` | نسخة احتياطية فورية | `Settings.Backup.Run` |
| `POST` | `/api/backups/{id}/restore` | استعادة | `Settings.Restore.Run` |

الأسرار في الاستجابة تُقنَّع (`"password": "••••"`) ولا تُرسَل نصّاً.

---

## 10) مخطط التدفّق (Flow Chart) — قراءة إعداد فعّال

```
[Request] GET /settings/TEMPLATE?storeId=502
        │
        ▼
[Cache?] key = (TenantId, 502, TEMPLATE) ── HIT ──► return
        │ MISS
        ▼
[Load] TenantSettings(TEMPLATE)   ← الأساس
[Load] StoreSettings(502, TEMPLATE) ← التجاوز
        │
        ▼
[Merge] Store JSON فوق Tenant JSON (deep merge)
        │
        ▼
[Cache set] + [Return effective settings]
```

---

## 11) ماذا يحدث عند: الحذف / التعديل / الاستعادة

| الحدث | السلوك |
|-------|--------|
| **تعديل إعداد** | تحديث الصف (نفس `Category`) + **إبطال Cache** فوراً → مفعول لحظي بلا نشر. |
| **حذف تجاوز فرع** | Soft delete لصف `StoreSettings` → يعود الفرع لوراثة قيمة الشركة. |
| **تغيير سعر صرف** | يؤثر على العمليات الجديدة فقط؛ الفواتير القديمة تحتفظ بـ Snapshot سعرها. |
| **تعطيل ضريبة** | `IsActive=0` — لا تظهر للاختيار الجديد، والفواتير القديمة تبقى بضريبتها. |
| **رفع شعار/خلفية جديد** | يُخزَّن كأصل جديد ويُحدَّث المسار في JSON؛ القديم يُؤرشف. |
| **استعادة (Restore)** | عملية حسّاسة: موافقة مزدوجة + وضع صيانة + سجل تدقيق كامل (تفاصيل [28-Backup-And-Restore.md](28-Backup-And-Restore.md)). |

---

## 12) سجل التدقيق (Audit Log)

- كل حفظ إعداد يُسجَّل في `AuditLogs` بـ `OldValues`/`NewValues` (JSON) — مع **تقنيع الأسرار** قبل التسجيل.
- تشغيل النسخ الاحتياطي/الاستعادة يُسجَّل مع من ومتى ونتيجة العملية.
- تغيير الضرائب والعملات (بيانات محاسبية حسّاسة) يُدقَّق إجبارياً.

---

## 13) الأخطاء المحتملة (Possible Errors)

| الكود | السبب | الرسالة |
|------|-------|---------|
| `422 INVALID_JSON` | فشل `ISJSON` | "بنية الإعداد غير صالحة" |
| `422 SMTP_TEST_FAILED` | فشل اتصال SMTP | "تعذّر الاتصال بخادم البريد" |
| `409 DEFAULT_TAX_EXISTS` | ضريبة افتراضية موجودة | "يوجد ضريبة افتراضية بالفعل" |
| `413 LOGO_TOO_LARGE` | الشعار > 2MB | "حجم الشعار كبير جداً" |
| `403 RESTORE_NOT_APPROVED` | استعادة بلا موافقة مزدوجة | "الاستعادة تتطلّب موافقة إضافية" |
| `409 CONCURRENCY_CONFLICT` | تعديل متزامن | "عُدّل الإعداد من جلسة أخرى" |

---

## 14) الأداء (Performance)

- الإعدادات تُقرأ كثيراً وتُكتَب نادراً → **Cache-first** (Redis/in-memory) بمفتاح `(TenantId, StoreId, Category)`، يُبطَل فقط عند الحفظ.
- الدمج (Merge) يُحسَب مرّة ويُخزَّن — لا إعادة حساب لكل طلب.
- جداول `TaxRates`/`Currencies` صغيرة ومفهرسة بـ `(TenantId, ...)` — تُحمَّل بالكامل للمستأجر وتُخزَّن مؤقتاً.
- رفع الأصول (Logo/Background) يذهب لتخزين ملفات/Blob لا لقاعدة البيانات — يُحفظ المسار فقط.

---

## 15) الأمان (Security)

1. **تشفير الأسرار:** كلمات مرور SMTP ومفاتيح API تُشفَّر (DPAPI/Key Vault) ولا تُرجَع أبداً بنصّ صريح.
2. **عزل الإعدادات:** كل قراءة/كتابة مفلترة بـ `TenantId` — مستأجر لا يرى إعدادات آخر (Global Query Filter + RLS).
3. **تحقّق رفع الملفات:** فحص نوع MIME الفعلي وحجم الشعار/الخلفية لمنع رفع محتوى خبيث.
4. **تقييد الاستعادة:** أخطر عملية — موافقة مزدوجة، تدقيق كامل، وصلاحية على مستوى المنصّة.
5. **حماية الحقن في القوالب:** قوالب الإيصال/HTML تُعرَض بعد Sanitization لمنع XSS في الطباعة/المعاينة.

---

_يلتزم بالمرجع الحاكم [04-Database-Design.md](04-Database-Design.md): JSON مسموح للإعدادات المرنة فقط (مع `ISJSON`)، والبيانات المحاسبية (الضرائب/العملات) علائقية._
