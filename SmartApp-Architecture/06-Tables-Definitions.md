# 06 — Tables & Definitions (تعريفات الجداول)

> `CREATE TABLE` فعلي لكل جداول **النواة (Core)** فقط. يلتزم بالمرجع الحاكم [05-Database-Design.md](05-Database-Design.md).
>
> **قواعد هذا الملف (حاكمة):**
> - **لا وراثة عمياء** لجداول Smart ERP POS القديمة — فقط الجداول المعتمدة لوحدات SmartApp الأساسية.
> - **فصل صارم:** جداول Core (الإصدار الأول) بـ SQL كامل · جداول Extensions (POS، إلخ) بالاسم فقط كـ Roadmap.
> - **صفر جداول** SaaS / Subscription / Billing / Payment.
> - **لا `StoreId`** في أي جدول نواة.
>
> لتقليل التكرار: كتلة الأعمدة المشتركة تُختصر بـ `-- <<BASE COLUMNS>>` وتعني الأعمدة العشرة من [05-Database-Design.md §2](05-Database-Design.md). الجداول المرجعية/الفوقية التي لا تحملها موضَّحة صراحةً.

---

## القسم أ — جداول النواة (Core Tables — الإصدار الأول) ✅

قائمة جداول النواة مجمّعة حسب الوحدة:

| الوحدة | الجداول |
|--------|---------|
| **Tenancy** | `Tenants`, `TenantSettings` |
| **Identity** | `Users`, `Roles`, `UserRoles`, `Permissions`, `RolePermissions`, `RefreshTokens` |
| **Catalog** | `Categories`, `Units`, `Products`, `ProductUnits`, `ProductBarcodes` |
| **Inventory** | `Stock`, `StockMovements`(Append-Only), `StockAdjustments`, `StockAdjustmentItems` |
| **Partners** | `Customers`, `Suppliers`, `CustomerPayments`, `SupplierPayments` |
| **Sales** | `SalesInvoices`, `SalesInvoiceItems`, `SalesReturns`, `SalesReturnItems` |
| **Purchases** | `PurchaseInvoices`, `PurchaseInvoiceItems`, `PurchaseReturns`, `PurchaseReturnItems` |
| **Ops/Settings** | `AuditLogs`(Append-Only), `Sequences` |

**إجمالي جداول النواة: 30 جدولاً.**

---

## 1) Tenancy

### 1.1 `Tenants` — المستأجرون (لا يحمل `TenantId`؛ يُدار من مالك النظام)

```sql
CREATE TABLE [dbo].[Tenants]
(
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [PublicId]         UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Tenants_PublicId DEFAULT (NEWID()),
    [Name]             NVARCHAR(200)    NOT NULL,          -- اسم الشركة/المحل
    [Code]             VARCHAR(50)      NOT NULL,          -- كود فريد للمستأجر
    [Status]           TINYINT          NOT NULL CONSTRAINT DF_Tenants_Status DEFAULT (1),
                        -- 1=Active, 2=Suspended, 3=Disabled  (TenantStatus enum)
    [ContactEmail]     NVARCHAR(256)    NULL,
    [ContactPhone]     NVARCHAR(30)     NULL,
    [ActivatedDate]    DATETIME2(3)     NULL,
    [SuspendedDate]    DATETIME2(3)     NULL,
    [DisabledDate]     DATETIME2(3)     NULL,
    [Notes]            NVARCHAR(1000)   NULL,              -- ملاحظات مالك النظام

    -- أعمدة تدقيق (لكن لا TenantId — هذا تعريف المستأجر نفسه)
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_Tenants_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_Tenants_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,

    CONSTRAINT [PK_Tenants] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UX_Tenants_Code] UNIQUE ([Code]),
    CONSTRAINT [CK_Tenants_Status] CHECK ([Status] IN (1,2,3))
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Tenants_PublicId] ON [dbo].[Tenants]([PublicId]);
GO
```

> **`Status` هو بديل SaaS بالكامل:** `1=Active` (يعمل)، `2=Suspended` (موقوف مؤقتاً — يُرفض الدخول)، `3=Disabled` (معطّل نهائياً). يغيّرها مالك النظام يدوياً عبر `TenantsController` (انظر [09-Multi-Tenant.md](09-Multi-Tenant.md)).

### 1.2 `TenantSettings` — إعدادات المستأجر

```sql
CREATE TABLE [dbo].[TenantSettings]
(
    [Currency]         VARCHAR(3)       NOT NULL CONSTRAINT DF_TenantSettings_Currency DEFAULT ('SAR'),
    [TimeZone]         VARCHAR(60)      NOT NULL CONSTRAINT DF_TenantSettings_TimeZone DEFAULT ('UTC'),
    [DefaultTaxRate]   DECIMAL(9,4)     NOT NULL CONSTRAINT DF_TenantSettings_Tax DEFAULT (0),
    [Locale]           VARCHAR(10)      NOT NULL CONSTRAINT DF_TenantSettings_Locale DEFAULT ('ar'),
    [ThemeJson]        NVARCHAR(MAX)    NULL
        CONSTRAINT CK_TenantSettings_Theme_Json CHECK ([ThemeJson] IS NULL OR ISJSON([ThemeJson]) = 1),
    [ExtraJson]        NVARCHAR(MAX)    NULL
        CONSTRAINT CK_TenantSettings_Extra_Json CHECK ([ExtraJson] IS NULL OR ISJSON([ExtraJson]) = 1),
    -- <<BASE COLUMNS>>  (Id, TenantId, audit, soft delete, ConcurrencyStamp)
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_TenantSettings_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_TenantSettings_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,

    CONSTRAINT [PK_TenantSettings] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UX_TenantSettings_Tenant] UNIQUE ([TenantId]),
    CONSTRAINT [FK_TenantSettings_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION
);
GO
```

---

## 2) Identity

### 2.1 `Users`

```sql
CREATE TABLE [dbo].[Users]
(
    [UserName]           NVARCHAR(256)  NOT NULL,
    [NormalizedUserName] NVARCHAR(256)  NOT NULL,
    [Email]              NVARCHAR(256)  NULL,
    [NormalizedEmail]    NVARCHAR(256)  NULL,
    [FullName]           NVARCHAR(200)  NOT NULL,
    [PasswordHash]       NVARCHAR(MAX)  NOT NULL,
    [SecurityStamp]      NVARCHAR(200)  NULL,
    [PhoneNumber]        NVARCHAR(30)   NULL,
    [IsActive]           BIT            NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    [IsSystemOwner]      BIT            NOT NULL CONSTRAINT DF_Users_IsSystemOwner DEFAULT (0),
    [AccessFailedCount]  INT            NOT NULL CONSTRAINT DF_Users_AccessFailed DEFAULT (0),
    [LockoutEndUtc]      DATETIME2(3)   NULL,
    [LastLoginUtc]       DATETIME2(3)   NULL,
    -- <<BASE COLUMNS>> — ملاحظة: TenantId يكون NULL لحساب مالك النظام فقط
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NULL,          -- NULL = System Owner
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_Users_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,

    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Users_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION
);
GO
-- تفرّد اسم المستخدم داخل المستأجر (لا عالمياً — مستأجران قد يملكان 'admin')
CREATE UNIQUE NONCLUSTERED INDEX [UX_Users_Tenant_UserName]
    ON [dbo].[Users]([TenantId],[NormalizedUserName]) WHERE [IsDeleted] = 0;
GO
```

### 2.2 `Roles`

```sql
CREATE TABLE [dbo].[Roles]
(
    [Name]             NVARCHAR(100)    NOT NULL,
    [NormalizedName]   NVARCHAR(100)    NOT NULL,
    [Description]      NVARCHAR(300)    NULL,
    [IsSystemRole]     BIT              NOT NULL CONSTRAINT DF_Roles_IsSystem DEFAULT (0), -- أدوار افتراضية غير قابلة للحذف
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_Roles_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_Roles_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,

    CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Roles_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Roles_Tenant_Name]
    ON [dbo].[Roles]([TenantId],[NormalizedName]) WHERE [IsDeleted] = 0;
GO
```

### 2.3 `Permissions` — مرجعي (لا `TenantId`)

```sql
CREATE TABLE [dbo].[Permissions]
(
    [Id]          INT           IDENTITY(1,1) NOT NULL,
    [Key]         VARCHAR(100)  NOT NULL,   -- 'products.create', 'sales.view', ...
    [Resource]    VARCHAR(50)   NOT NULL,   -- 'products'
    [Action]      VARCHAR(50)   NOT NULL,   -- 'create'
    [Description] NVARCHAR(200) NULL,
    CONSTRAINT [PK_Permissions] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UX_Permissions_Key] UNIQUE ([Key])
);
GO
```

### 2.4 `RolePermissions`

```sql
CREATE TABLE [dbo].[RolePermissions]
(
    [RoleId]       BIGINT       NOT NULL,
    [PermissionId] INT          NOT NULL,
    [TenantId]     BIGINT       NOT NULL,
    [CreatedDate]  DATETIME2(3) NOT NULL CONSTRAINT DF_RolePermissions_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]    BIGINT       NULL,
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY CLUSTERED ([RoleId],[PermissionId]),
    CONSTRAINT [FK_RolePermissions_Role] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RolePermissions_Permission] FOREIGN KEY ([PermissionId]) REFERENCES [dbo].[Permissions]([Id]) ON DELETE NO ACTION
);
GO
```

### 2.5 `UserRoles`

```sql
CREATE TABLE [dbo].[UserRoles]
(
    [UserId]      BIGINT       NOT NULL,
    [RoleId]      BIGINT       NOT NULL,
    [TenantId]    BIGINT       NOT NULL,
    [CreatedDate] DATETIME2(3) NOT NULL CONSTRAINT DF_UserRoles_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]   BIGINT       NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([UserId],[RoleId]),
    CONSTRAINT [FK_UserRoles_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserRoles_Role] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles]([Id]) ON DELETE NO ACTION
);
GO
```

### 2.6 `RefreshTokens`

```sql
CREATE TABLE [dbo].[RefreshTokens]
(
    [UserId]        BIGINT         NOT NULL,
    [TokenHash]     VARBINARY(64)  NOT NULL,   -- SHA-256 للتوكن (لا يُخزَّن خاماً)
    [ExpiresUtc]    DATETIME2(3)   NOT NULL,
    [CreatedUtc]    DATETIME2(3)   NOT NULL CONSTRAINT DF_RefreshTokens_Created DEFAULT (SYSUTCDATETIME()),
    [RevokedUtc]    DATETIME2(3)   NULL,
    [ReplacedByHash] VARBINARY(64) NULL,       -- لكشف إعادة الاستخدام (rotation)
    [CreatedByIp]   VARCHAR(45)    NULL,
    [TenantId]      BIGINT         NULL,        -- NULL لمالك النظام
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY CLUSTERED ([UserId],[TokenHash]),
    CONSTRAINT [FK_RefreshTokens_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE NO ACTION
);
GO
CREATE NONCLUSTERED INDEX [IX_RefreshTokens_TokenHash] ON [dbo].[RefreshTokens]([TokenHash]);
GO
```

---

## 3) Catalog

### 3.1 `Categories` — تصنيفات شجرية

```sql
CREATE TABLE [dbo].[Categories]
(
    [Name]        NVARCHAR(150)  NOT NULL,
    [ParentId]    BIGINT         NULL,          -- تصنيف أب (شجري)
    [Code]        VARCHAR(50)    NULL,
    [IsActive]    BIT            NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT (1),
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_Categories_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_Categories_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Categories_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Categories_Parent] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[Categories]([Id]) ON DELETE NO ACTION
);
GO
```

### 3.2 `Units` — وحدات القياس

```sql
CREATE TABLE [dbo].[Units]
(
    [Name]        NVARCHAR(50)   NOT NULL,   -- 'قطعة', 'كرتون', 'كجم'
    [Symbol]      NVARCHAR(20)   NULL,
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_Units_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_Units_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_Units] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Units_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION
);
GO
```

### 3.3 `Products`

```sql
CREATE TABLE [dbo].[Products]
(
    [Name]           NVARCHAR(250)  NOT NULL,
    [Sku]            VARCHAR(60)    NULL,          -- رمز المنتج الداخلي
    [CategoryId]     BIGINT         NULL,
    [BaseUnitId]     BIGINT         NOT NULL,      -- الوحدة الأساسية
    [CostPrice]      DECIMAL(18,4)  NOT NULL CONSTRAINT DF_Products_Cost DEFAULT (0),   -- WAC الحالي
    [SalePrice]      DECIMAL(18,4)  NOT NULL CONSTRAINT DF_Products_Sale DEFAULT (0),
    [TaxRate]        DECIMAL(9,4)   NOT NULL CONSTRAINT DF_Products_Tax DEFAULT (0),
    [ReorderLevel]   DECIMAL(18,4)  NOT NULL CONSTRAINT DF_Products_Reorder DEFAULT (0),
    [IsActive]       BIT            NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT (1),
    [TrackStock]     BIT            NOT NULL CONSTRAINT DF_Products_TrackStock DEFAULT (1),
    [CustomFieldsJson] NVARCHAR(MAX) NULL
        CONSTRAINT CK_Products_Custom_Json CHECK ([CustomFieldsJson] IS NULL OR ISJSON([CustomFieldsJson]) = 1),
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_Products_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_Products_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Products_Tenant]   FOREIGN KEY ([TenantId])   REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Products_Category] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Products_BaseUnit] FOREIGN KEY ([BaseUnitId]) REFERENCES [dbo].[Units]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [CK_Products_Prices] CHECK ([CostPrice] >= 0 AND [SalePrice] >= 0)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Products_Tenant_Sku]
    ON [dbo].[Products]([TenantId],[Sku]) WHERE [IsDeleted] = 0 AND [Sku] IS NOT NULL;
GO
```

### 3.4 `ProductUnits` — وحدات المنتج ومعامل التحويل

```sql
CREATE TABLE [dbo].[ProductUnits]
(
    [ProductId]      BIGINT         NOT NULL,
    [UnitId]         BIGINT         NOT NULL,
    [ConversionFactor] DECIMAL(18,6) NOT NULL,   -- 1 كرتون = 24 قطعة
    [Barcode]        VARCHAR(60)    NULL,
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_ProductUnits_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_ProductUnits_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_ProductUnits] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ProductUnits_Tenant]  FOREIGN KEY ([TenantId])  REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ProductUnits_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ProductUnits_Unit]    FOREIGN KEY ([UnitId])    REFERENCES [dbo].[Units]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [CK_ProductUnits_Factor]  CHECK ([ConversionFactor] > 0)
);
GO
```

### 3.5 `ProductBarcodes`

```sql
CREATE TABLE [dbo].[ProductBarcodes]
(
    [ProductId]   BIGINT       NOT NULL,
    [Barcode]     VARCHAR(60)  NOT NULL,
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_ProductBarcodes_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_ProductBarcodes_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_ProductBarcodes] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ProductBarcodes_Tenant]  FOREIGN KEY ([TenantId])  REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ProductBarcodes_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]) ON DELETE NO ACTION
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductBarcodes_Tenant_Barcode]
    ON [dbo].[ProductBarcodes]([TenantId],[Barcode]) WHERE [IsDeleted] = 0;
GO
```

---

## 4) Inventory

### 4.1 `Stock` — الرصيد الحالي (صف واحد لكل منتج)

```sql
CREATE TABLE [dbo].[Stock]
(
    [ProductId]      BIGINT         NOT NULL,
    [QuantityOnHand] DECIMAL(18,4)  NOT NULL CONSTRAINT DF_Stock_Qty DEFAULT (0),
    [AverageCost]    DECIMAL(18,4)  NOT NULL CONSTRAINT DF_Stock_AvgCost DEFAULT (0),  -- WAC
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_Stock_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_Stock_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_Stock] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Stock_Tenant]  FOREIGN KEY ([TenantId])  REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Stock_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]) ON DELETE NO ACTION
);
GO
-- صف رصيد واحد لكل منتج داخل المستأجر (يُوسَّع بـ StoreId عند دعم الفروع)
CREATE UNIQUE NONCLUSTERED INDEX [UX_Stock_Tenant_Product]
    ON [dbo].[Stock]([TenantId],[ProductId]) WHERE [IsDeleted] = 0;
GO
```

### 4.2 `StockMovements` — سجل الحركات (**Append-Only**)

```sql
CREATE TABLE [dbo].[StockMovements]
(
    [ProductId]      BIGINT         NOT NULL,
    [MovementType]   TINYINT        NOT NULL,      -- StockMovementType enum (§ملاحظة)
    [Quantity]       DECIMAL(18,4)  NOT NULL,      -- موجب=وارد، سالب=صادر
    [UnitCost]       DECIMAL(18,4)  NOT NULL CONSTRAINT DF_StockMovements_UnitCost DEFAULT (0),
    [BalanceAfter]   DECIMAL(18,4)  NOT NULL,      -- الرصيد بعد الحركة (snapshot)
    [AvgCostAfter]   DECIMAL(18,4)  NOT NULL,      -- WAC بعد الحركة (snapshot)
    [SourceDocType]  VARCHAR(30)    NULL,          -- 'SALES_INVOICE', 'PURCHASE_INVOICE', 'ADJUSTMENT'
    [SourceDocId]    BIGINT         NULL,          -- Id المستند المصدر
    [Notes]          NVARCHAR(300)  NULL,
    -- أعمدة (بلا Soft Delete — Append-Only لا يُحذَف)
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_StockMovements_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    CONSTRAINT [PK_StockMovements] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_StockMovements_Tenant]  FOREIGN KEY ([TenantId])  REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StockMovements_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]) ON DELETE NO ACTION
);
GO
CREATE NONCLUSTERED INDEX [IX_StockMovements_Tenant_Product_Date]
    ON [dbo].[StockMovements]([TenantId],[ProductId],[CreatedDate]);
GO
```

> **`MovementType`:** `1=PurchaseIn`, `2=SaleOut`, `3=PurchaseReturnOut`, `4=SaleReturnIn`, `5=AdjustmentIn`, `6=AdjustmentOut`, `7=OpeningBalance`. **لا `UPDATE` ولا `DELETE`** — أي تصحيح يكون بحركة معاكسة جديدة.

### 4.3 `StockAdjustments` + `StockAdjustmentItems`

```sql
CREATE TABLE [dbo].[StockAdjustments]
(
    [AdjustmentNumber] VARCHAR(30)   NOT NULL,   -- من Sequences (DocType='ADJUSTMENT')
    [Reason]           NVARCHAR(300) NULL,
    [Status]           TINYINT       NOT NULL CONSTRAINT DF_StockAdj_Status DEFAULT (1), -- 1=Draft,2=Posted
    [PostedDate]       DATETIME2(3)  NULL,
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_StockAdj_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_StockAdj_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_StockAdjustments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_StockAdjustments_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [dbo].[StockAdjustmentItems]
(
    [StockAdjustmentId] BIGINT        NOT NULL,
    [ProductId]         BIGINT        NOT NULL,
    [QuantityChange]    DECIMAL(18,4) NOT NULL,   -- موجب/سالب
    [UnitCost]          DECIMAL(18,4) NOT NULL CONSTRAINT DF_StockAdjItems_Cost DEFAULT (0),
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_StockAdjItems_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_StockAdjItems_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_StockAdjustmentItems] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_StockAdjItems_Tenant]  FOREIGN KEY ([TenantId])          REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StockAdjItems_Adj]     FOREIGN KEY ([StockAdjustmentId]) REFERENCES [dbo].[StockAdjustments]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StockAdjItems_Product] FOREIGN KEY ([ProductId])         REFERENCES [dbo].[Products]([Id]) ON DELETE NO ACTION
);
GO
```

---

## 5) Partners

### 5.1 `Customers`

```sql
CREATE TABLE [dbo].[Customers]
(
    [Name]         NVARCHAR(200)  NOT NULL,
    [Phone]        NVARCHAR(30)   NULL,
    [Email]        NVARCHAR(256)  NULL,
    [Address]      NVARCHAR(400)  NULL,
    [CreditLimit]  DECIMAL(18,4)  NOT NULL CONSTRAINT DF_Customers_CreditLimit DEFAULT (0),
    [Balance]      DECIMAL(18,4)  NOT NULL CONSTRAINT DF_Customers_Balance DEFAULT (0),  -- موجب=مدين لنا
    [IsActive]     BIT            NOT NULL CONSTRAINT DF_Customers_IsActive DEFAULT (1),
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_Customers_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_Customers_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_Customers] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Customers_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION
);
GO
```

### 5.2 `Suppliers`

```sql
CREATE TABLE [dbo].[Suppliers]
(
    [Name]      NVARCHAR(200)  NOT NULL,
    [Phone]     NVARCHAR(30)   NULL,
    [Email]     NVARCHAR(256)  NULL,
    [Address]   NVARCHAR(400)  NULL,
    [Balance]   DECIMAL(18,4)  NOT NULL CONSTRAINT DF_Suppliers_Balance DEFAULT (0),  -- موجب=مستحق لهم علينا
    [IsActive]  BIT            NOT NULL CONSTRAINT DF_Suppliers_IsActive DEFAULT (1),
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_Suppliers_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_Suppliers_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_Suppliers] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Suppliers_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION
);
GO
```

### 5.3 `CustomerPayments` / `SupplierPayments`

```sql
CREATE TABLE [dbo].[CustomerPayments]
(
    [CustomerId]   BIGINT         NOT NULL,
    [Amount]       DECIMAL(18,4)  NOT NULL,
    [PaymentDate]  DATETIME2(3)   NOT NULL CONSTRAINT DF_CustPay_Date DEFAULT (SYSUTCDATETIME()),
    [Method]       VARCHAR(20)    NOT NULL,   -- 'Cash','Transfer','Card' (وسيلة سداد لدين — ليست بوّابة دفع منصّة)
    [Reference]    NVARCHAR(100)  NULL,
    [Notes]        NVARCHAR(300)  NULL,
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_CustPay_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_CustPay_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_CustomerPayments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_CustPay_Tenant]   FOREIGN KEY ([TenantId])   REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CustPay_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers]([Id]) ON DELETE NO ACTION
);
GO
-- SupplierPayments: نفس البنية مع SupplierId بدل CustomerId (يُختصر للإيجاز)
```

> **تنبيه مفاهيمي:** `CustomerPayments`/`SupplierPayments` هي **تسديد ديون تجارية بين المستأجر وشركائه** — ليست مدفوعات منصّة ولا اشتراكات. لا علاقة لها بأي SaaS billing. `Method` مجرّد وصف نقدي/تحويل، بلا أي تكامل بوّابة دفع.

---

## 6) Sales

### 6.1 `SalesInvoices`

```sql
CREATE TABLE [dbo].[SalesInvoices]
(
    [InvoiceNumber]  VARCHAR(30)    NOT NULL,   -- من Sequences (DocType='SALES_INVOICE')
    [CustomerId]     BIGINT         NULL,       -- NULL = بيع نقدي بلا عميل مسجَّل
    [InvoiceDate]    DATETIME2(3)   NOT NULL CONSTRAINT DF_SalesInv_Date DEFAULT (SYSUTCDATETIME()),
    [Status]         TINYINT        NOT NULL CONSTRAINT DF_SalesInv_Status DEFAULT (1), -- InvoiceStatus enum
    [SubTotal]       DECIMAL(18,4)  NOT NULL CONSTRAINT DF_SalesInv_Sub DEFAULT (0),
    [DiscountTotal]  DECIMAL(18,4)  NOT NULL CONSTRAINT DF_SalesInv_Disc DEFAULT (0),
    [TaxTotal]       DECIMAL(18,4)  NOT NULL CONSTRAINT DF_SalesInv_Tax DEFAULT (0),
    [GrandTotal]     DECIMAL(18,4)  NOT NULL CONSTRAINT DF_SalesInv_Grand DEFAULT (0),
    [PaidAmount]     DECIMAL(18,4)  NOT NULL CONSTRAINT DF_SalesInv_Paid DEFAULT (0),
    [Notes]          NVARCHAR(500)  NULL,
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_SalesInv_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_SalesInv_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_SalesInvoices] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SalesInv_Tenant]   FOREIGN KEY ([TenantId])   REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SalesInv_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers]([Id]) ON DELETE NO ACTION
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_SalesInvoices_Tenant_Number]
    ON [dbo].[SalesInvoices]([TenantId],[InvoiceNumber]) WHERE [IsDeleted] = 0;
GO
```

> `InvoiceStatus`: `1=Draft`, `2=Confirmed`, `3=PartiallyReturned`, `4=FullyReturned`, `5=Cancelled`.

### 6.2 `SalesInvoiceItems` — بنود (snapshot للسعر/الضريبة)

```sql
CREATE TABLE [dbo].[SalesInvoiceItems]
(
    [SalesInvoiceId] BIGINT         NOT NULL,
    [ProductId]      BIGINT         NOT NULL,
    [Quantity]       DECIMAL(18,4)  NOT NULL,
    [UnitPrice]      DECIMAL(18,4)  NOT NULL,   -- snapshot وقت البيع
    [UnitCost]       DECIMAL(18,4)  NOT NULL,   -- snapshot لـ WAC وقت البيع (للربحية)
    [DiscountAmount] DECIMAL(18,4)  NOT NULL CONSTRAINT DF_SalesItems_Disc DEFAULT (0),
    [TaxRate]        DECIMAL(9,4)   NOT NULL CONSTRAINT DF_SalesItems_TaxRate DEFAULT (0),  -- snapshot
    [TaxAmount]      DECIMAL(18,4)  NOT NULL CONSTRAINT DF_SalesItems_Tax DEFAULT (0),
    [LineTotal]      DECIMAL(18,4)  NOT NULL,
    [ReturnedQty]    DECIMAL(18,4)  NOT NULL CONSTRAINT DF_SalesItems_Returned DEFAULT (0), -- لمنع الإرجاع الزائد
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_SalesItems_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_SalesItems_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_SalesInvoiceItems] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SalesItems_Tenant]  FOREIGN KEY ([TenantId])       REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SalesItems_Invoice] FOREIGN KEY ([SalesInvoiceId]) REFERENCES [dbo].[SalesInvoices]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SalesItems_Product] FOREIGN KEY ([ProductId])      REFERENCES [dbo].[Products]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [CK_SalesItems_Returned] CHECK ([ReturnedQty] >= 0 AND [ReturnedQty] <= [Quantity])  -- منع الإرجاع الزائد قاعدياً
);
GO
```

### 6.3 `SalesReturns` + `SalesReturnItems` (المرتجع بسعر أصلي مجمّد)

```sql
CREATE TABLE [dbo].[SalesReturns]
(
    [ReturnNumber]     VARCHAR(30)    NOT NULL,   -- Sequences (DocType='SALES_RETURN')
    [SalesInvoiceId]   BIGINT         NOT NULL,   -- يشير دائماً للفاتورة الأصلية
    [ReturnDate]       DATETIME2(3)   NOT NULL CONSTRAINT DF_SalesRet_Date DEFAULT (SYSUTCDATETIME()),
    [TotalAmount]      DECIMAL(18,4)  NOT NULL CONSTRAINT DF_SalesRet_Total DEFAULT (0),
    [Reason]           NVARCHAR(300)  NULL,
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_SalesRet_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_SalesRet_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_SalesReturns] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SalesRet_Tenant]  FOREIGN KEY ([TenantId])       REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SalesRet_Invoice] FOREIGN KEY ([SalesInvoiceId]) REFERENCES [dbo].[SalesInvoices]([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [dbo].[SalesReturnItems]
(
    [SalesReturnId]      BIGINT        NOT NULL,
    [SalesInvoiceItemId] BIGINT        NOT NULL,   -- البند الأصلي المُرجَع منه
    [ProductId]          BIGINT        NOT NULL,
    [Quantity]           DECIMAL(18,4) NOT NULL,
    [UnitPrice]          DECIMAL(18,4) NOT NULL,   -- **مجمّد من البند الأصلي** (لا سعر المنتج الحالي)
    [LineTotal]          DECIMAL(18,4) NOT NULL,
    -- <<BASE COLUMNS>>
    [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT           NOT NULL,
    [CreatedDate]      DATETIME2(3)     NOT NULL CONSTRAINT DF_SalesRetItems_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT           NULL,
    [ModifiedDate]     DATETIME2(3)     NULL,
    [ModifiedBy]       BIGINT           NULL,
    [DeletedDate]      DATETIME2(3)     NULL,
    [DeletedBy]        BIGINT           NULL,
    [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_SalesRetItems_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION       NOT NULL,
    CONSTRAINT [PK_SalesReturnItems] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SalesRetItems_Tenant]   FOREIGN KEY ([TenantId])            REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SalesRetItems_Return]   FOREIGN KEY ([SalesReturnId])       REFERENCES [dbo].[SalesReturns]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SalesRetItems_InvItem]  FOREIGN KEY ([SalesInvoiceItemId])  REFERENCES [dbo].[SalesInvoiceItems]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SalesRetItems_Product]  FOREIGN KEY ([ProductId])           REFERENCES [dbo].[Products]([Id]) ON DELETE NO ACTION
);
GO
```

> **قاعدة محورية مورّثة من التصميم القديم:** كل مرتجع يشير للفاتورة الأصلية، و`UnitPrice` **مجمّد من `SalesInvoiceItems.UnitPrice`** حتى لو تغيّر سعر المنتج لاحقاً. منع الإرجاع الزائد على مستويين: قاعدياً (`CK_SalesItems_Returned`) وتزامنياً (`UPDATE ... SET ReturnedQty += @q WHERE ReturnedQty + @q <= Quantity` ذرّياً). تفصيل في [13-Development-Rules.md](13-Development-Rules.md).

---

## 7) Purchases

بنية مماثلة لـ Sales (تُختصر لتفادي التكرار — تلتزم بنفس المعايير):

| الجدول | نظير في Sales | فروق جوهرية |
|--------|----------------|-------------|
| `PurchaseInvoices` | `SalesInvoices` | `SupplierId` بدل `CustomerId` · حركة مخزون **واردة** · تُحدّث WAC |
| `PurchaseInvoiceItems` | `SalesInvoiceItems` | لا `UnitCost` snapshot منفصل (السعر هو التكلفة) · `ReturnedQty` لمنع إرجاع زائد |
| `PurchaseReturns` | `SalesReturns` | يشير لفاتورة الشراء الأصلية · يعكس أثر رصيد المورد |
| `PurchaseReturnItems` | `SalesReturnItems` | `UnitPrice` مجمّد من بند الشراء الأصلي |

```sql
-- PurchaseInvoices: نفس أعمدة SalesInvoices مع:
--   [SupplierId] BIGINT NOT NULL  (FK → Suppliers)
--   [InvoiceNumber] من Sequences (DocType='PURCHASE_INVOICE')
--   Status enum مماثل
-- كل الأعمدة المشتركة + الفهارس + قواعد No-Cascade كما في Sales.
```

---

## 8) Ops & Settings

### 8.1 `AuditLogs` — سجل التدقيق (**Append-Only**)

```sql
CREATE TABLE [dbo].[AuditLogs]
(
    [Id]           BIGINT         IDENTITY(1,1) NOT NULL,
    [TenantId]     BIGINT         NULL,          -- NULL للأحداث الفوقية (مالك النظام)
    [UserId]       BIGINT         NULL,          -- من نفّذ الفعل
    [Action]       VARCHAR(20)    NOT NULL,      -- 'Create','Update','Delete','Login','StatusChange'
    [EntityName]   VARCHAR(100)   NOT NULL,      -- 'Product','SalesInvoice','Tenant'
    [EntityId]     BIGINT         NULL,
    [OldValuesJson] NVARCHAR(MAX) NULL
        CONSTRAINT CK_AuditLogs_Old_Json CHECK ([OldValuesJson] IS NULL OR ISJSON([OldValuesJson]) = 1),
    [NewValuesJson] NVARCHAR(MAX) NULL
        CONSTRAINT CK_AuditLogs_New_Json CHECK ([NewValuesJson] IS NULL OR ISJSON([NewValuesJson]) = 1),
    [IpAddress]    VARCHAR(45)    NULL,
    [CorrelationId] VARCHAR(50)   NULL,
    [CreatedDate]  DATETIME2(3)   NOT NULL CONSTRAINT DF_AuditLogs_CreatedDate DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([Id])
    -- لا FK صارم على TenantId/UserId لضمان بقاء السجل حتى لو حُذف الكيان soft
);
GO
CREATE NONCLUSTERED INDEX [IX_AuditLogs_Tenant_Entity_Date]
    ON [dbo].[AuditLogs]([TenantId],[EntityName],[CreatedDate]);
GO
```

> **Append-Only:** لا `UPDATE` ولا `DELETE` ولا Soft Delete. جدول تدقيق نزيه.

### 8.2 `Sequences`

معرّف في [05-Database-Design.md §10](05-Database-Design.md). يخدم كل أنواع المستندات (`SALES_INVOICE`, `PURCHASE_INVOICE`, `SALES_RETURN`, `PURCHASE_RETURN`, `ADJUSTMENT`).

---

## القسم ب — جداول الامتدادات المستقبلية (Future Extension Tables) 🔜

> **لا تُنشَأ في الإصدار الأول.** مذكورة بالاسم فقط كـ Roadmap. تُصمَّم تفصيلياً عند الوصول لمرحلتها، وتُضاف **كوحدات موازية دون تعديل جداول النواة** (انظر [04-Domain-Boundaries.md](04-Domain-Boundaries.md)).

| الامتداد | الجداول المتوقّعة (تصميم لاحق) |
|----------|-------------------------------|
| **POS** | `PosSessions`, `SuspendedInvoices`, وربطها بـ `SalesInvoices` عبر `SourceDocType='POS_SALE'` |
| **Cashier Shifts** | `PosShifts`, `CashDrawerMovements`, `ShiftReconciliations` |
| **Barcode Terminal** | لا جداول جديدة غالباً — طبقة واجهة فوق `ProductBarcodes` |
| **Promotions & Offers** | `Offers`, `OfferRules`, `OfferProducts`, `OfferUsage` |
| **Loyalty Program** | `LoyaltyAccounts`, `LoyaltyTransactions`, `LoyaltyTiers` |
| **Advanced Multi-store** | إضافة عمود `StoreId` انتقائياً لـ (`Stock`, `SalesInvoices`, `PurchaseInvoices`, `StockMovements`) + جدول `Stores` |

---

## القسم ج — جداول ممنوعة صراحةً (Forbidden — No SaaS) ❌

**لا تُنشأ هذه الجداول أبداً في SmartApp** — بديلها حقل `Tenants.Status`:

`Subscriptions` · `SubscriptionPlans` · `PlanFeatures` · `BillingCycles` · `PlatformInvoices` · `PlatformPayments` · `PaymentGatewayTransactions` · `Licenses` · أي جدول ترخيص/فوترة/دفع تلقائي.

---

## خلاصة عدّ الجداول

| القسم | العدد | الحالة |
|-------|:-----:|--------|
| **Core (الإصدار الأول)** | **30** | ✅ SQL كامل هنا |
| Extensions (POS، إلخ) | ~15 متوقّع | 🔜 تصميم لاحق (أسماء فقط) |
| SaaS/Billing/Payment | **0** | ❌ ممنوعة صراحةً |

---

_يلتزم بالمرجع الحاكم [05-Database-Design.md](05-Database-Design.md). العلاقات الكاملة في [07-ERD-Relationships.md](07-ERD-Relationships.md) والفهرسة في [08-Indexing-Strategy.md](08-Indexing-Strategy.md)._
