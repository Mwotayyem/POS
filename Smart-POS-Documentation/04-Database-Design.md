# 04 — Database Design (التصميم العام والمعايير الموحّدة)

> هذا الملف هو **المرجع الحاكم** لكل جدول في النظام. أي جدول يُوثَّق في أي ملف آخر **يجب** أن يلتزم بالمعايير هنا. الهدف: تصميم علائقي مُطبَّع (Normalized)، آمن، متعدّد المستأجرين، وجاهز للإنتاج على **SQL Server 2022**.

---

## 1) المبادئ الحاكمة (Design Principles)

| المبدأ | القرار | السبب |
|--------|--------|-------|
| Normalization | 3NF كحدّ أدنى، مع de-normalization مقصود ومبرَّر فقط في جداول التقارير | تقليل التكرار وضمان التكامل |
| Data Types للأموال | `DECIMAL(18,4)` | تجنّب أخطاء الفاصلة العائمة في `FLOAT` |
| المفاتيح الأساسية | `BIGINT IDENTITY(1,1)` افتراضياً | أداء أعلى وحجم أقل من GUID كـ clustered key |
| مفاتيح التبادل الخارجي (Public IDs) | عمود إضافي `PublicId UNIQUEIDENTIFIER` عند الحاجة لكشف المعرّف خارجياً | عدم كشف تسلسل السجلات (IDOR) |
| التواريخ | `DATETIME2(3)` بتوقيت **UTC** حصراً | التوحيد عبر المناطق الزمنية |
| الحذف | **Soft Delete** إجباري لكل جداول الأعمال | الحفاظ على السجل المحاسبي والتدقيق |
| العزل | `TenantId` في كل جدول أعمال + **Global Query Filter** | منع تسرّب البيانات بين المستأجرين |
| التزامن | `ConcurrencyStamp ROWVERSION` | Optimistic Concurrency |

---

## 2) الأعمدة المشتركة الإجبارية (Mandatory Base Columns)

**كل جدول أعمال (Business Table)** يرث الأعمدة التالية. تُطبَّق عملياً عبر Base Entity في الكود (`BaseEntity`) و EF Core.

```sql
-- كتلة الأعمدة المشتركة — تُنسَخ في كل جدول أعمال
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,   -- المستأجر (الشركة) — العزل
    [StoreId]          BIGINT           NULL,       -- الفرع/المتجر (NULL = على مستوى الشركة)
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

### قواعد التطبيق

1. **`TenantId` NOT NULL** في كل جدول أعمال — لا استثناء.
2. **`StoreId`** يكون `NULL` للكيانات المشتركة على مستوى الشركة (مثل قائمة المنتجات المركزية)، و`NOT NULL` للكيانات الخاصة بفرع (مثل رصيد مخزون فرع).
3. جداول النظام العامة (مثل `Countries`, `Currencies` المرجعية العالمية) **لا** تحمل `TenantId` وتُعامَل كـ Reference Data.

---

## 3) قالب الجدول القياسي (Standard Table Template)

```sql
CREATE TABLE [dbo].[{TableName}]
(
    -- ===== الأعمدة الخاصة بالجدول =====
    -- (تُضاف هنا)

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_{TableName}_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_{TableName}_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_{TableName}] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_{TableName}_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_{TableName}_Store]  FOREIGN KEY ([StoreId])  REFERENCES [dbo].[Stores]([Id])
);
GO

-- فهرس العزل الإلزامي: كل استعلام يبدأ بـ TenantId
CREATE NONCLUSTERED INDEX [IX_{TableName}_Tenant_Store]
    ON [dbo].[{TableName}] ([TenantId], [StoreId])
    WHERE [IsDeleted] = 0;   -- Filtered Index يستبعد المحذوف
GO
```

---

## 4) استراتيجية المفاتيح (Keys Strategy)

| النوع | القرار |
|-------|--------|
| Primary Key | `BIGINT IDENTITY` — clustered |
| Foreign Key | دائماً مُعرَّف صراحةً مع `ON DELETE NO ACTION` (لأن الحذف soft) |
| Composite Uniqueness | عبر `UNIQUE` filtered index يتضمّن `TenantId` (مثل: باركود فريد داخل المستأجر فقط) |
| Natural Keys | لا تُستخدم كـ PK، بل كـ Unique Constraint |

**مثال — تفرّد الباركود على مستوى المستأجر:**

```sql
CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductBarcodes_Tenant_Barcode]
    ON [dbo].[ProductBarcodes] ([TenantId], [Barcode])
    WHERE [IsDeleted] = 0;
```

---

## 5) العزل متعدّد المستأجرين على مستوى DB

**النموذج المعتمد مبدئياً:** *Single Database, Shared Schema, Row-Level Isolation via `TenantId`* (مفصّل في [03-Database-Strategy.md](03-Database-Strategy.md) و [24-MultiTenant.md](24-MultiTenant.md)).

- **EF Core Global Query Filter** يُضاف تلقائياً:
  ```csharp
  modelBuilder.Entity<TEntity>()
      .HasQueryFilter(e => e.TenantId == _tenantProvider.CurrentTenantId && !e.IsDeleted);
  ```
- طبقة إضافية اختيارية: **SQL Server Row-Level Security (RLS)** كـ defense-in-depth (مفصّل في [27-Security.md](27-Security.md)).

---

## 6) الفهرسة (Indexing Standards)

| القاعدة | التفصيل |
|---------|---------|
| كل FK | يُفهرس (SQL Server لا يفهرس FK تلقائياً) |
| فهرس العزل | `(TenantId, StoreId)` على كل جدول أعمال |
| الفهارس المُرشَّحة | `WHERE IsDeleted = 0` لاستبعاد المحذوف soft |
| أعمدة البحث | باركود، اسم منتج، رقم فاتورة — فهارس مخصّصة |
| Covering Indexes | لاستعلامات التقارير الثقيلة عبر `INCLUDE` |

التفاصيل الكاملة في [Database/Indexes.md](Database/Indexes.md).

---

## 7) التعاملات (Transactions)

- كل عملية تُعدّل أكثر من جدول (فاتورة + بنودها + حركة مخزون) تُغلَّف في **Transaction واحد** عبر `Unit of Work`.
- عزل المعاملات الافتراضي: `READ COMMITTED SNAPSHOT` (تفعيل RCSI على قاعدة البيانات لتقليل الأقفال).
- المخزون يُحدَّث ضمن نفس معاملة الفاتورة (Atomicity) — انظر [13-Inventory.md](13-Inventory.md).

---

## 8) متى نستخدم JSON

`NVARCHAR(MAX)` مع `ISJSON()` check constraint **يُسمح به فقط** لـ:

- الإعدادات المرنة (Tenant/Store settings, receipt templates).
- الحقول المخصّصة (Custom Fields) للمنتجات.
- Snapshot للبيانات التاريخية غير القابلة للاستعلام العلائقي.

**ممنوع منعاً باتاً** تخزين الفواتير أو بنودها أو الحركات المحاسبية أو المخزون كـ JSON.

```sql
[Settings] NVARCHAR(MAX) NULL
    CONSTRAINT CK_{Table}_Settings_Json CHECK ([Settings] IS NULL OR ISJSON([Settings]) = 1),
```

---

## 9) قائمة الجداول الأساسية (Master Table List)

> القائمة الكاملة مع الأعمدة في [Database/Tables.md](Database/Tables.md). ملخّص المجموعات:

| المجموعة | الجداول |
|----------|---------|
| Tenancy | `Tenants`, `Stores`, `TenantSettings`, `StoreSettings` |
| Identity | `Users`, `Roles`, `Permissions`, `RolePermissions`, `UserRoles`, `RefreshTokens` |
| Catalog | `Products`, `ProductBarcodes`, `ProductUnits`, `ProductVariants`, `Categories`, `Brands`, `Units` |
| Partners | `Suppliers`, `Customers`, `SupplierPayments`, `CustomerPayments` |
| Inventory | `Warehouses`, `Stock`, `StockMovements`, `StockAdjustments`, `StockTransfers` |
| Purchasing | `PurchaseOrders`, `PurchaseInvoices`, `PurchaseInvoiceItems`, `PurchaseReturns` |
| Sales | `SalesInvoices`, `SalesInvoiceItems`, `SalesReturns`, `SalesReturnItems`, `Payments` |
| POS | `PosShifts`, `PosSessions`, `CashDrawerMovements`, `SuspendedInvoices` |
| Promotions | `Offers`, `OfferRules`, `OfferProducts`, `OfferUsage` |
| Ops | `Notifications`, `AuditLogs`, `Settings`, `Sequences` |

---

## 10) توليد المعرّفات التسلسلية للفواتير (Document Numbering)

أرقام الفواتير **لا** تعتمد على `Id` (سرّي)، بل على تسلسل مستقل **لكل مستأجر/فرع/نوع مستند**:

```sql
CREATE TABLE [dbo].[Sequences]
(
    [Id]         BIGINT IDENTITY(1,1) NOT NULL,
    [TenantId]   BIGINT       NOT NULL,
    [StoreId]    BIGINT       NULL,
    [DocType]    VARCHAR(30)  NOT NULL,   -- 'SALES_INVOICE', 'PURCHASE_INVOICE', ...
    [Prefix]     VARCHAR(10)  NULL,        -- 'INV-', 'PUR-'
    [NextValue]  BIGINT       NOT NULL CONSTRAINT DF_Sequences_NextValue DEFAULT (1),
    [Padding]    TINYINT      NOT NULL CONSTRAINT DF_Sequences_Padding DEFAULT (6),
    CONSTRAINT [PK_Sequences] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UX_Sequences] UNIQUE ([TenantId], [StoreId], [DocType])
);
```

يُقرأ ويُزاد ذرّياً داخل معاملة الفاتورة عبر `UPDATE ... OUTPUT` لتفادي التعارض.

---

_يلتزم كل ملف جداول لاحق بهذا المرجع. أي انحراف يجب تبريره صراحةً في ملف الجدول._
