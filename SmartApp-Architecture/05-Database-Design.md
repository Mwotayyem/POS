# 05 — Database Design (المرجع الحاكم للتصميم)

> هذا الملف هو **المرجع الحاكم** لكل جدول في SmartApp. أي جدول يُوثَّق في [06-Tables-Definitions.md](06-Tables-Definitions.md) **يجب** أن يلتزم بالمعايير هنا. الهدف: تصميم علائقي مُطبَّع (Normalized)، آمن، متعدّد المستأجرين، جاهز للإنتاج على **SQL Server 2022+**.

---

## 1) المبادئ الحاكمة (Design Principles)

| المبدأ | القرار | السبب |
|--------|--------|-------|
| Normalization | 3NF كحدّ أدنى؛ de-normalization مقصود ومبرَّر فقط في جداول التقارير | تقليل التكرار وضمان التكامل |
| نوع الأموال | `DECIMAL(18,4)` | تجنّب أخطاء الفاصلة العائمة في `FLOAT` |
| المفاتيح الأساسية | `BIGINT IDENTITY(1,1)` — Clustered | أداء أعلى وحجم أقل من GUID |
| المعرّفات العامّة | عمود إضافي `PublicId UNIQUEIDENTIFIER` عند كشف المعرّف خارجياً | منع IDOR (عدم كشف التسلسل) |
| التواريخ | `DATETIME2(3)` بتوقيت **UTC** حصراً | التوحيد عبر المناطق الزمنية |
| الحذف | **Soft Delete** إجباري لكل جداول الأعمال | الحفاظ على السجل المحاسبي والتدقيق |
| العزل | `TenantId` في كل جدول أعمال + **Global Query Filter** | منع تسرّب البيانات بين المستأجرين |
| التزامن | `ConcurrencyStamp ROWVERSION` | Optimistic Concurrency |
| الحذف المتتالي | **ممنوع** (`ON DELETE NO ACTION`) | لأن كل الحذف soft |
| Append-Only | `StockMovements`, `AuditLogs` — إدراج فقط | نزاهة السجل المالي/التدقيقي |

> **قرار العزل المعتمد لـ SmartApp:** `Tenant` هو حدّ العزل **الأساسي والوحيد** الآن. **لا `StoreId`** في أي جدول نواة. يُضاف `StoreId` مستقبلاً **فقط** للكيانات التي تتطلّب عمليات على مستوى فرع (انظر §7).

---

## 2) الأعمدة المشتركة الإجبارية (Mandatory Base Columns)

**كل جدول أعمال (Business Table)** يرث الأعمدة التالية، عبر `BaseEntity` في الكود و EF Core:

```sql
-- كتلة الأعمدة المشتركة — تُطبَّق على كل جدول أعمال
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,   -- المستأجر (الشركة) — حدّ العزل
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_{T}_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,       -- UserId
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_{T}_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
```

> `{T}` = اسم الجدول، يُستبدل في كل قيد لتفادي تعارض أسماء القيود.

### تمثيل `BaseEntity` في الكود (Domain)

```csharp
// SmartApp.Domain/Common/BaseEntity.cs
public abstract class BaseEntity : ITenantOwned, IAuditable, ISoftDeletable
{
    public long   Id { get; set; }
    public long   TenantId { get; set; }          // يُختَم خادم-جانبي — لا من العميل

    public DateTime  CreatedDate { get; set; }
    public long?     CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public long?     ModifiedBy { get; set; }

    public DateTime? DeletedDate { get; set; }
    public long?     DeletedBy { get; set; }
    public bool      IsDeleted { get; set; }

    public byte[]  ConcurrencyStamp { get; set; } = default!;   // ROWVERSION
}
```

### قواعد التطبيق

1. **`TenantId` NOT NULL** في كل جدول أعمال — لا استثناء.
2. **`TenantId` يُختَم خادم-جانبياً** عند الإضافة عبر `SaveChanges` — لا يُقبل من العميل.
3. جداول النظام المرجعية العالمية (مثل `Countries`, `Currencies`) **لا** تحمل `TenantId` وتُعامَل كـ Reference Data (مشتركة للقراءة).
4. حساب مالك النظام (`System Owner`) وجدول `Tenants` نفسه ليسا "جداول أعمال مملوكة لمستأجر" — لهما معاملة خاصّة (انظر §6).

---

## 3) قالب الجدول القياسي (Standard Table Template)

```sql
CREATE TABLE [dbo].[{TableName}]
(
    -- ===== الأعمدة الخاصّة بالجدول =====
    -- (تُضاف هنا)

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_{TableName}_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_{TableName}_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_{TableName}] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_{TableName}_Tenant] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION
);
GO

-- فهرس العزل الإلزامي: كل استعلام يبدأ بـ TenantId
CREATE NONCLUSTERED INDEX [IX_{TableName}_Tenant]
    ON [dbo].[{TableName}] ([TenantId])
    WHERE [IsDeleted] = 0;   -- Filtered Index يستبعد المحذوف
GO
```

> **الفرق عن التوثيق القديم:** لا عمود `StoreId` ولا `FK_..._Store`. فهرس العزل على `(TenantId)` فقط بدل `(TenantId, StoreId)`.

---

## 4) استراتيجية المفاتيح (Keys Strategy)

| النوع | القرار |
|-------|--------|
| Primary Key | `BIGINT IDENTITY` — Clustered |
| Foreign Key | مُعرَّف صراحةً مع `ON DELETE NO ACTION` (لأن الحذف soft) |
| Composite Uniqueness | عبر `UNIQUE` filtered index يتضمّن `TenantId` (مثل باركود فريد داخل المستأجر فقط) |
| Natural Keys | لا تُستخدم كـ PK، بل كـ Unique Constraint |
| Public IDs | `PublicId UNIQUEIDENTIFIER` عند الحاجة لكشف المعرّف في الـ API (منع IDOR) |

**مثال — تفرّد الباركود على مستوى المستأجر:**

```sql
CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductBarcodes_Tenant_Barcode]
    ON [dbo].[ProductBarcodes] ([TenantId], [Barcode])
    WHERE [IsDeleted] = 0;
```

---

## 5) العزل على مستوى قاعدة البيانات (DB-Level Isolation)

**النموذج المعتمد:** *Single Database, Shared Schema, Row-Level Isolation via `TenantId`*.

**الطبقة المعتمدة الآن — EF Core Global Query Filter (تلقائي):**

```csharp
modelBuilder.Entity<TEntity>()
    .HasQueryFilter(e => e.TenantId == _tenantProvider.CurrentTenantId && !e.IsDeleted);
```

**طبقة تقوية مستقبلية اختيارية — SQL Server RLS:** الأعمدة والتصميم جاهزة لتفعيل Row-Level Security كـ defense-in-depth عند الحاجة (تفصيل التصميم الجاهز في [11-Security-Architecture.md](11-Security-Architecture.md) §"RLS المستقبلي"). **لا تُفعَّل في الإصدار الأول** حفاظاً على بساطة النشر والـ migrations.

---

## 6) معاملة خاصّة: جداول التحكّم بالمستأجرين

بعض الجداول ليست "مملوكة لمستأجر" بل تُدير المستأجرين أنفسهم:

| الجدول | المعاملة |
|--------|----------|
| `Tenants` | **لا** يحمل `TenantId` (هو تعريف المستأجر نفسه). يُدار من مالك النظام. لا يخضع للفلتر العالمي |
| `Users` | يحمل `TenantId` (كل مستخدم ينتمي لمستأجر)، **عدا** حساب مالك النظام (`TenantId` قد يكون `NULL` أو مستأجر-نظام خاص) |
| `Permissions` | مرجعي — قائمة الصلاحيات الثابتة (لا `TenantId`) |
| `AuditLogs` | يحمل `TenantId` لكنه **Append-Only** ولا يخضع لـ Soft Delete |

> تفصيل إدارة مالك النظام والفصل بين "سياق المستأجر" و"سياق الإدارة الفوقية" في [09-Multi-Tenant.md](09-Multi-Tenant.md).

---

## 7) قابلية إضافة الفروع مستقبلاً (`StoreId` Future-Proofing)

القرار: **لا `StoreId` الآن**. لكن التصميم يسمح بإضافته لاحقاً دون إعادة كتابة:

| الجانب | كيف صُمّم للمستقبل |
|--------|--------------------|
| الكيانات المرشّحة | فقط `Stock`, `SalesInvoice`, `PurchaseInvoice`, حركات المخزون — التي تتطلّب تشغيلاً على مستوى فرع |
| آلية الإضافة | عمود `StoreId BIGINT NULL` يُضاف عبر migration للكيانات المعنية فقط (لا كل الجداول) |
| العزل | يبقى `TenantId` الحدّ الأمني؛ `StoreId` **تقسيم تشغيلي داخل المستأجر** لا حدّ أمني |
| الفهرسة | يُوسَّع فهرس العزل إلى `(TenantId, StoreId)` للكيانات المعنية عند الإضافة |

> **مهمّ:** لا نضيف `StoreId` استباقياً لكل جدول (كما فعل التوثيق القديم) — نضيفه انتقائياً عند الحاجة الفعلية. هذا يبقي النواة أبسط.

---

## 8) التعاملات (Transactions)

- كل عملية تُعدّل أكثر من جدول (فاتورة + بنودها + حركة مخزون + رصيد شريك) تُغلَّف في **Transaction واحد** عبر `Unit of Work` (والـ `TransactionBehavior` في MediatR للأوامر).
- عزل المعاملات الافتراضي: **`READ COMMITTED SNAPSHOT` (RCSI)** — يُفعَّل على قاعدة البيانات لتقليل الأقفال.
- المخزون يُحدَّث ضمن **نفس معاملة الفاتورة** (Atomicity).

---

## 9) متى نستخدم JSON

`NVARCHAR(MAX)` مع `ISJSON()` check constraint **يُسمح به فقط** لـ:

- الإعدادات المرنة (`TenantSettings`, receipt templates).
- الحقول المخصّصة (Custom Fields) للمنتجات.
- Snapshot تاريخي غير قابل للاستعلام العلائقي.

**ممنوع منعاً باتاً** تخزين الفواتير أو بنودها أو الحركات المحاسبية أو المخزون كـ JSON.

```sql
[Settings] NVARCHAR(MAX) NULL
    CONSTRAINT CK_{Table}_Settings_Json CHECK ([Settings] IS NULL OR ISJSON([Settings]) = 1),
```

---

## 10) ترقيم المستندات (Document Numbering)

أرقام الفواتير **لا** تعتمد على `Id` (سرّي)، بل على تسلسل مستقلّ **لكل مستأجر/نوع مستند**:

```sql
CREATE TABLE [dbo].[Sequences]
(
    [Id]         BIGINT IDENTITY(1,1) NOT NULL,
    [TenantId]   BIGINT       NOT NULL,
    [DocType]    VARCHAR(30)  NOT NULL,   -- 'SALES_INVOICE', 'PURCHASE_INVOICE', ...
    [Prefix]     VARCHAR(10)  NULL,        -- 'INV-', 'PUR-'
    [NextValue]  BIGINT       NOT NULL CONSTRAINT DF_Sequences_NextValue DEFAULT (1),
    [Padding]    TINYINT      NOT NULL CONSTRAINT DF_Sequences_Padding DEFAULT (6),
    CONSTRAINT [PK_Sequences] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UX_Sequences] UNIQUE ([TenantId], [DocType])
);
```

> يُقرأ ويُزاد **ذرّياً** داخل معاملة الفاتورة عبر `UPDATE ... OUTPUT` لتفادي التعارض. (بدون `StoreId` في مفتاح التفرّد الآن — يُضاف عند دعم الفروع).

---

## 11) ما أُزيل صراحةً من التصميم (No SaaS)

**لا يوجد في قاعدة بيانات SmartApp أيٌّ من الجداول التالية** (كانت في نموذج SaaS القديم):

`Subscriptions` · `SubscriptionPlans` · `Invoices` (فواتير المنصّة) · `Payments` (مدفوعات المنصّة) · `BillingCycles` · `PlanFeatures` · أي جدول ترخيص/فوترة/دفع.

**البديل:** حقل `Status` على `Tenants` (تفصيل في [06-Tables-Definitions.md](06-Tables-Definitions.md) §Tenancy).

---

_يلتزم [06-Tables-Definitions.md](06-Tables-Definitions.md) بهذا المرجع. أي انحراف يجب تبريره صراحةً._
