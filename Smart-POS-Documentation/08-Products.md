# 08 — Products (المنتجات)

> شاشة إدارة المنتجات هي العمود الفقري للنظام: كل عملية بيع أو شراء أو حركة مخزون تشير إلى منتج. تدعم هذه الوحدة الباركود المتعدّد، الوحدات المتعدّدة بمعامل تحويل، المتغيّرات (لون/حجم/وزن)، الضريبة، الخصم، تتبّع المخزون، الصلاحية، الرقم التسلسلي، رقم الدفعة، والحقول المخصّصة.

---

## 1) الهدف من الصفحة (Purpose)

- إنشاء وتعديل وعرض وأرشفة (Soft Delete) المنتجات على مستوى المستأجر (Tenant).
- ربط كل منتج بتصنيف (Category)، علامة تجارية (Brand)، ومورّد افتراضي (Supplier).
- إدارة **باركود واحد أو أكثر** لكل منتج، و**وحدات بيع متعدّدة** بمعامل تحويل من الوحدة الأساسية.
- ضبط أسعار الكلفة والبيع، الضريبة، الخصم، وحدود المخزون (Min / Max / Reorder).
- الحفاظ على **سجل تاريخ الأسعار** بحيث لا تتأثّر الفواتير القديمة عند تغيير السعر.

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية (Permission Code) | الوصف |
|----------------------------|-------|
| `products.view` | عرض قائمة المنتجات وتفاصيلها |
| `products.create` | إنشاء منتج جديد |
| `products.edit` | تعديل منتج قائم |
| `products.delete` | أرشفة منتج (Soft Delete) |
| `products.price.edit` | تعديل أسعار الكلفة/البيع (صلاحية منفصلة حسّاسة) |
| `products.cost.view` | عرض سعر الكلفة (يُخفى عن الكاشير عادةً) |
| `products.export` | تصدير المنتجات (Excel / CSV) |

- تُفحص الصلاحيات عبر `[Authorize(Policy = "products.view")]` على مستوى الـ Controller/Endpoint.
- سعر الكلفة محميّ بصلاحية مستقلّة `products.cost.view` ولا يُرسَل في الاستجابة إن غابت الصلاحية.

---

## 3) تصميم الصفحة (Page Layout)

```
┌──────────────────────────────────────────────────────────────┐
│  Products                       [ + New Product ] [ Export ▾ ] │
├──────────────────────────────────────────────────────────────┤
│  [ Search: name / SKU / barcode ]  [Category ▾] [Brand ▾] [⚙] │
├──────────────────────────────────────────────────────────────┤
│  Img │ SKU     │ Name          │ Category │ Price  │ Stock │ ⋯ │
│  �__  │ SKU-001 │ Coffee 250g   │ Beverage │ 3.500  │  120  │ ✎ │
│  �__  │ SKU-002 │ T-Shirt Blue  │ Clothing │ 9.900  │   40  │ ✎ │
├──────────────────────────────────────────────────────────────┤
│              ◄  1  2  3  …  Page size [25 ▾]  ►                │
└──────────────────────────────────────────────────────────────┘
```

نموذج الإضافة/التعديل مقسّم إلى تبويبات (Tabs):
**General · Pricing & Tax · Barcodes · Units · Variants · Inventory · Custom Fields**.

---

## 4) جميع الأزرار (Buttons)

| الزر | الموقع | الوظيفة | الصلاحية |
|------|--------|---------|----------|
| **+ New Product** | أعلى القائمة | فتح نموذج إنشاء فارغ | `products.create` |
| **Save** | تذييل النموذج | حفظ (POST/PUT) داخل Transaction | create/edit |
| **Save & New** | تذييل النموذج | حفظ ثم تفريغ النموذج | create |
| **Cancel** | تذييل النموذج | إغلاق دون حفظ | — |
| **✎ Edit** | صف الجدول | فتح النموذج للتعديل | `products.edit` |
| **🗑 Delete** | صف الجدول | أرشفة (Soft Delete) بعد تأكيد | `products.delete` |
| **+ Add Barcode** | تبويب Barcodes | إضافة سطر باركود | edit |
| **+ Add Unit** | تبويب Units | إضافة وحدة بيع بمعامل تحويل | edit |
| **+ Add Variant** | تبويب Variants | إضافة متغيّر (Color/Size) | edit |
| **Price History** | تبويب Pricing | عرض سجل تغيّر الأسعار | `products.price.edit` |
| **Export ▾** | أعلى القائمة | تصدير Excel/CSV | `products.export` |

---

## 5) جميع الحقول (Fields)

### General
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|:------:|---------|
| `Name` | نص (200) | ✔ | اسم المنتج (يدعم عربي/إنجليزي) |
| `NameEn` | نص (200) | — | اسم إنجليزي اختياري للطباعة/التصدير |
| `SKU` | نص (50) | ✔ | فريد داخل المستأجر |
| `Description` | نص طويل | — | وصف تسويقي |
| `CategoryId` | قائمة | ✔ | من شجرة التصنيفات |
| `BrandId` | قائمة | — | العلامة التجارية |
| `SupplierId` | قائمة | — | المورّد الافتراضي |
| `ImageUrl` | ملف/رابط | — | صورة رئيسية |
| `IsActive` | Boolean | ✔ | افتراضي `true` |

### Pricing & Tax
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|:------:|---------|
| `CostPrice` | DECIMAL(18,4) | ✔ | سعر الكلفة (محميّ بصلاحية) |
| `DefaultSellPrice` | DECIMAL(18,4) | ✔ | السعر الافتراضي للبيع (الأسعار المتعددة: جملة/مفرق/VIP في `ProductPrices`) |
| `TaxId` | قائمة | — | نسبة الضريبة (VAT %) |
| `IsTaxInclusive` | Boolean | ✔ | هل السعر شامل الضريبة |
| `DiscountType` | Enum | — | `None / Percentage / Fixed` |
| `DiscountValue` | DECIMAL(18,4) | — | قيمة الخصم الافتراضي |

### Inventory
| الحقل | النوع | ملاحظات |
|-------|------|---------|
| `TrackInventory` | Boolean | تفعيل تتبّع الكمية |
| `MinQuantity` | DECIMAL(18,4) | حدّ التنبيه الأدنى |
| `MaxQuantity` | DECIMAL(18,4) | الحدّ الأقصى للتخزين |
| `ReorderQuantity` | DECIMAL(18,4) | كمية إعادة الطلب |
| `HasExpiry` | Boolean | يتطلّب تاريخ صلاحية |
| `HasSerial` | Boolean | يتطلّب رقم تسلسلي |
| `HasBatch` | Boolean | يتطلّب رقم دفعة |
| `CustomFields` | JSON | حقول مرنة (`ISJSON` check) |

---

## 6) التحقق (Validation) — FluentValidation

- `Name`: مطلوب، طوله 2..200.
- `SKU`: مطلوب، فريد داخل المستأجر (فحص async ضد قاعدة البيانات، مع استبعاد `IsDeleted = 1`).
- `DefaultSellPrice >= 0` و `CostPrice >= 0`.
- عند `IsTaxInclusive = true` يجب أن يكون `TaxId` غير فارغ.
- `DiscountValue`: إن كان النوع `Percentage` فالقيمة `0..100`.
- كل `Barcode` فريد داخل المستأجر (لا يتكرّر عبر منتجات أخرى).
- كل `ProductUnit.ConversionFactor > 0`، وتوجد **وحدة أساسية واحدة فقط** (`IsBaseUnit = 1`) بمعامل `1`.
- `MinQuantity <= MaxQuantity` عند تعبئة كليهما.
- `CustomFields` يجب أن يجتاز `ISJSON()`.

---

## 7) قواعد العمل (Business Rules)

1. **وحدة أساسية إلزامية:** لكل منتج وحدة أساسية واحدة (`IsBaseUnit=1, ConversionFactor=1`)؛ باقي الوحدات تُعرّف بمعامل تحويل نسبةً إليها (مثال: Box = 12 × Piece).
2. **الأسعار على مستوى الوحدة:** يجوز تعريف سعر بيع خاص لكل وحدة (`ProductUnits.SellingPrice`)؛ إن غاب يُحسب = سعر الوحدة الأساسية × المعامل.
3. **الباركود المتعدّد:** يمكن أن يحمل كل منتج/وحدة/متغيّر باركوداً مستقلاً؛ البحث في POS يطابق أي باركود.
4. **المتغيّرات:** كل متغيّر (Variant) هو SKU فرعي له مخزونه وباركوده وسعره الخاص (يرث من الأب ما لم يُحدَّد).
5. **سعر الكلفة للفواتير:** لا يُستخدم `Products.CostPrice` مباشرة في تكلفة البيع؛ التكلفة تُحسب من طبقات المخزون (FIFO/Average) في وحدة المخزون — انظر [13-Inventory.md](13-Inventory.md).
6. **عدم تأثّر الفواتير القديمة:** أي بند فاتورة (Sales/Purchase Item) يُخزّن **نسخة snapshot** من السعر والضريبة والخصم لحظة الترحيل؛ تغيير سعر المنتج لاحقاً لا يعدّل أي بند مُرحَّل.

---

## 8) جداول قاعدة البيانات (Database Tables)

> جميع الجداول ترث الأعمدة المشتركة الإلزامية من [04-Database-Design.md](04-Database-Design.md) وتلتزم بـ Soft Delete وعزل `TenantId`.

### 8.1 Products

```sql
CREATE TABLE [dbo].[Products]
(
    [Name]            NVARCHAR(200)  NOT NULL,
    [NameEn]          NVARCHAR(200)  NULL,
    [SKU]             NVARCHAR(50)   NOT NULL,
    [Description]     NVARCHAR(MAX)  NULL,
    [ImageUrl]        NVARCHAR(500)  NULL,
    [CategoryId]      BIGINT         NOT NULL,
    [BrandId]         BIGINT         NULL,
    [SupplierId]      BIGINT         NULL,
    [BaseUnitId]      BIGINT         NOT NULL,   -- الوحدة الأساسية (من جدول Units)
    [CostPrice]       DECIMAL(18,4)  NOT NULL CONSTRAINT DF_Products_CostPrice DEFAULT (0),
    [DefaultSellPrice] DECIMAL(18,4) NOT NULL CONSTRAINT DF_Products_DefaultSellPrice DEFAULT (0), -- السعر الافتراضي؛ الأسعار المتعددة في ProductPrices
    [TaxId]           BIGINT         NULL,
    [IsTaxInclusive]  BIT            NOT NULL CONSTRAINT DF_Products_TaxIncl DEFAULT (0),
    [DiscountType]    TINYINT        NOT NULL CONSTRAINT DF_Products_DiscType DEFAULT (0), -- 0=None,1=Pct,2=Fixed
    [DiscountValue]   DECIMAL(18,4)  NOT NULL CONSTRAINT DF_Products_DiscVal DEFAULT (0),
    [TrackInventory]  BIT            NOT NULL CONSTRAINT DF_Products_Track DEFAULT (1),
    [MinQuantity]     DECIMAL(18,4)  NULL,
    [MaxQuantity]     DECIMAL(18,4)  NULL,
    [ReorderQuantity] DECIMAL(18,4)  NULL,
    [HasExpiry]       BIT            NOT NULL CONSTRAINT DF_Products_HasExpiry DEFAULT (0),
    [HasSerial]       BIT            NOT NULL CONSTRAINT DF_Products_HasSerial DEFAULT (0),
    [HasBatch]        BIT            NOT NULL CONSTRAINT DF_Products_HasBatch DEFAULT (0),
    [IsActive]        BIT            NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT (1),
    [CustomFields]    NVARCHAR(MAX)  NULL,
    [PublicId]        UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Products_PublicId DEFAULT (NEWID()),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_Products_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_Products_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_Products] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Products_Tenant]   FOREIGN KEY ([TenantId])   REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_Products_Store]    FOREIGN KEY ([StoreId])    REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_Products_Category] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories]([Id]),
    CONSTRAINT [FK_Products_Brand]    FOREIGN KEY ([BrandId])    REFERENCES [dbo].[Brands]([Id]),
    CONSTRAINT [FK_Products_Supplier] FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Suppliers]([Id]),
    CONSTRAINT [FK_Products_BaseUnit] FOREIGN KEY ([BaseUnitId]) REFERENCES [dbo].[Units]([Id]),
    CONSTRAINT [FK_Products_Tax]      FOREIGN KEY ([TaxId])      REFERENCES [dbo].[Taxes]([Id]),
    CONSTRAINT [CK_Products_CustomFields] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1),
    CONSTRAINT [CK_Products_Prices] CHECK ([CostPrice] >= 0 AND [DefaultSellPrice] >= 0)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Products_Tenant_SKU]
    ON [dbo].[Products] ([TenantId], [SKU]) WHERE [IsDeleted] = 0;
GO
CREATE NONCLUSTERED INDEX [IX_Products_Tenant_Store]
    ON [dbo].[Products] ([TenantId], [StoreId]) WHERE [IsDeleted] = 0;
GO
CREATE NONCLUSTERED INDEX [IX_Products_Category]
    ON [dbo].[Products] ([TenantId], [CategoryId]) INCLUDE ([Name], [DefaultSellPrice]) WHERE [IsDeleted] = 0;
GO
```

### 8.2 ProductBarcodes

```sql
CREATE TABLE [dbo].[ProductBarcodes]
(
    [ProductId]     BIGINT        NOT NULL,
    [ProductUnitId] BIGINT        NULL,   -- الباركود قد يخصّ وحدة بيع محدّدة
    [VariantId]     BIGINT        NULL,   -- أو متغيّراً محدّداً
    [Barcode]       NVARCHAR(50)  NOT NULL,
    [IsPrimary]     BIT           NOT NULL CONSTRAINT DF_ProductBarcodes_Primary DEFAULT (0),

    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_ProductBarcodes_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_ProductBarcodes_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_ProductBarcodes] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ProductBarcodes_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]),
    CONSTRAINT [FK_ProductBarcodes_Unit]    FOREIGN KEY ([ProductUnitId]) REFERENCES [dbo].[ProductUnits]([Id]),
    CONSTRAINT [FK_ProductBarcodes_Variant] FOREIGN KEY ([VariantId]) REFERENCES [dbo].[ProductVariants]([Id]),
    CONSTRAINT [FK_ProductBarcodes_Tenant]  FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductBarcodes_Tenant_Barcode]
    ON [dbo].[ProductBarcodes] ([TenantId], [Barcode]) WHERE [IsDeleted] = 0;
GO
CREATE NONCLUSTERED INDEX [IX_ProductBarcodes_Product]
    ON [dbo].[ProductBarcodes] ([ProductId]) WHERE [IsDeleted] = 0;
GO
```

### 8.3 ProductUnits

```sql
CREATE TABLE [dbo].[ProductUnits]
(
    [ProductId]        BIGINT        NOT NULL,
    [UnitId]           BIGINT        NOT NULL,   -- من جدول Units
    [ConversionFactor] DECIMAL(18,6) NOT NULL,   -- كم وحدة أساسية = 1 من هذه الوحدة
    [IsBaseUnit]       BIT           NOT NULL CONSTRAINT DF_ProductUnits_Base DEFAULT (0),
    [SellingPrice]     DECIMAL(18,4) NULL,        -- سعر خاص للوحدة (اختياري)
    [IsSellable]       BIT           NOT NULL CONSTRAINT DF_ProductUnits_Sellable DEFAULT (1),

    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_ProductUnits_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_ProductUnits_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_ProductUnits] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ProductUnits_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]),
    CONSTRAINT [FK_ProductUnits_Unit]    FOREIGN KEY ([UnitId])    REFERENCES [dbo].[Units]([Id]),
    CONSTRAINT [FK_ProductUnits_Tenant]  FOREIGN KEY ([TenantId])  REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [CK_ProductUnits_Factor] CHECK ([ConversionFactor] > 0)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductUnits_Product_Unit]
    ON [dbo].[ProductUnits] ([ProductId], [UnitId]) WHERE [IsDeleted] = 0;
GO
-- وحدة أساسية واحدة فقط لكل منتج
CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductUnits_OneBase]
    ON [dbo].[ProductUnits] ([ProductId]) WHERE [IsBaseUnit] = 1 AND [IsDeleted] = 0;
GO
```

### 8.4 ProductVariants

```sql
CREATE TABLE [dbo].[ProductVariants]
(
    [ProductId]    BIGINT        NOT NULL,
    [VariantSKU]   NVARCHAR(60)  NOT NULL,
    [Color]        NVARCHAR(50)  NULL,
    [Size]         NVARCHAR(50)  NULL,
    [Weight]       DECIMAL(18,4) NULL,
    [Attributes]   NVARCHAR(MAX) NULL,        -- سمات إضافية JSON
    [ExtraPrice]   DECIMAL(18,4) NOT NULL CONSTRAINT DF_ProductVariants_Extra DEFAULT (0),
    [SellingPrice] DECIMAL(18,4) NULL,        -- سعر خاص للمتغيّر (يتجاوز سعر الأب)
    [ExpiryDate]   DATE          NULL,
    [BatchNumber]  NVARCHAR(50)  NULL,
    [SerialNumber] NVARCHAR(80)  NULL,
    [IsActive]     BIT           NOT NULL CONSTRAINT DF_ProductVariants_Active DEFAULT (1),

    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_ProductVariants_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_ProductVariants_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_ProductVariants] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ProductVariants_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]),
    CONSTRAINT [FK_ProductVariants_Tenant]  FOREIGN KEY ([TenantId])  REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [CK_ProductVariants_Attrs] CHECK ([Attributes] IS NULL OR ISJSON([Attributes]) = 1)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductVariants_Tenant_SKU]
    ON [dbo].[ProductVariants] ([TenantId], [VariantSKU]) WHERE [IsDeleted] = 0;
GO
CREATE NONCLUSTERED INDEX [IX_ProductVariants_Product]
    ON [dbo].[ProductVariants] ([ProductId]) WHERE [IsDeleted] = 0;
GO
```

### 8.5 ProductPrices (السعر الحالي + أنواع الأسعار)

> **جدولان منفصلان بمسؤولية واضحة:**
> - **`ProductPrices`** (هذا القسم): يحمل **السعر الحالي النشط** لكل نوع سعر (Retail / Wholesale / VIP / ...). صفّ واحد نشط لكل (منتج/نوع/وحدة/فرع) — يُقرأ مباشرة عند البيع بلا منطق تاريخي.
> - **`ProductPriceHistory`** (القسم 8.6): سجلّ **تغييرات الأسعار عبر الزمن** (Old → New) — للتدقيق والتقارير فقط، لا يُقرأ عند البيع.
>
> `Products.DefaultSellPrice` يبقى كسعر افتراضي سريع (fallback). **الفواتير لا تتأثّر بأي تغيير لاحق** لأنها تحفظ snapshot السعر في بنودها (انظر [15-Sales-Invoices.md](15-Sales-Invoices.md)).

```sql
CREATE TABLE [dbo].[ProductPrices]
(
    [ProductId]      BIGINT        NOT NULL,
    [ProductUnitId]  BIGINT        NULL,        -- سعر خاص بوحدة معيّنة (اختياري)
    [PriceType]      VARCHAR(20)   NOT NULL,    -- 'Retail' | 'Wholesale' | 'Restaurant' | 'VIP' | ...
    [Price]          DECIMAL(18,4) NOT NULL,     -- السعر الحالي النشط لهذا النوع
    [Currency]       CHAR(3)       NOT NULL CONSTRAINT DF_ProdPrices_Currency DEFAULT ('JOD'),
    [IsDefault]      BIT           NOT NULL CONSTRAINT DF_ProdPrices_IsDefault DEFAULT (0),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,        -- سعر خاص بفرع (NULL = كل الفروع)
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_ProdPrices_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_ProdPrices_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_ProductPrices] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ProdPrices_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]),
    CONSTRAINT [FK_ProdPrices_Tenant]  FOREIGN KEY ([TenantId])  REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_ProdPrices_Store]   FOREIGN KEY ([StoreId])   REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [CK_ProdPrices_Price]   CHECK ([Price] >= 0)
);
GO

-- سعر نشط واحد فقط لكل (منتج/نوع/وحدة/فرع) — يمنع تكرار السعر الحالي
CREATE UNIQUE NONCLUSTERED INDEX [UX_ProdPrices_Active]
    ON [dbo].[ProductPrices] ([TenantId], [ProductId], [PriceType], [ProductUnitId], [StoreId])
    WHERE [IsDeleted] = 0;
GO

-- بحث سريع بالسعر عند البيع
CREATE NONCLUSTERED INDEX [IX_ProdPrices_Lookup]
    ON [dbo].[ProductPrices] ([TenantId], [ProductId], [PriceType])
    INCLUDE ([Price], [ProductUnitId], [StoreId], [IsDefault])
    WHERE [IsDeleted] = 0;
GO
```

**كيف يُختار السعر عند البيع:**
1. حدّد `PriceType` حسب سياق البيع (POS=Retail، فاتورة جملة=Wholesale، حسب العميل=VIP...).
2. اقرأ صف `ProductPrices` النشط المطابق (والفرع مطابق أو NULL) — **قراءة مباشرة، بلا منطق تواريخ**.
3. إن لم يوجد → استخدم `Products.DefaultSellPrice`.

### 8.6 ProductPriceHistory (سجل تغييرات الأسعار عبر الزمن)

> **للتدقيق والتقارير فقط.** كل تغيير في `ProductPrices.Price` (أو `Products.DefaultSellPrice`) يُسجَّل هنا صفّاً بالقيمة القديمة والجديدة. **لا يُقرأ عند البيع** — البيع يعتمد على `ProductPrices` النشط فقط.

```sql
CREATE TABLE [dbo].[ProductPriceHistory]
(
    [ProductId]      BIGINT        NOT NULL,
    [ProductUnitId]  BIGINT        NULL,
    [PriceType]      VARCHAR(20)   NOT NULL,    -- نفس أنواع ProductPrices
    [OldPrice]       DECIMAL(18,4) NOT NULL,
    [NewPrice]       DECIMAL(18,4) NOT NULL,
    [ChangedByUserId] BIGINT       NOT NULL,
    [ChangedAt]      DATETIME2(3)  NOT NULL CONSTRAINT DF_PriceHist_ChangedAt DEFAULT (SYSUTCDATETIME()),
    [Reason]         NVARCHAR(300) NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_PriceHist_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_PriceHist_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_ProductPriceHistory] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PriceHist_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]),
    CONSTRAINT [FK_PriceHist_Tenant]  FOREIGN KEY ([TenantId])  REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [CK_PriceHist_Prices]  CHECK ([OldPrice] >= 0 AND [NewPrice] >= 0)
);
GO
CREATE NONCLUSTERED INDEX [IX_PriceHist_Product_Type]
    ON [dbo].[ProductPriceHistory] ([TenantId], [ProductId], [PriceType], [ChangedAt] DESC)
    WHERE [IsDeleted] = 0;
GO
```

---

## 9) الـ API

**Base:** `/api/v1/products`

| Method | Endpoint | الوصف | Policy |
|--------|----------|-------|--------|
| GET | `/products` | قائمة مع بحث/ترقيم/فلترة | `products.view` |
| GET | `/products/{id}` | تفاصيل منتج مع الوحدات والباركود والمتغيّرات | `products.view` |
| GET | `/products/by-barcode/{barcode}` | بحث سريع لنقطة البيع | `products.view` |
| POST | `/products` | إنشاء منتج | `products.create` |
| PUT | `/products/{id}` | تعديل منتج | `products.edit` |
| PATCH | `/products/{id}/price` | تعديل سعر (يسجّل في History) | `products.price.edit` |
| DELETE | `/products/{id}` | أرشفة (Soft Delete) | `products.delete` |
| GET | `/products/{id}/price-history` | سجل الأسعار | `products.price.edit` |

### مثال: POST /products (Request)

```json
{
  "name": "Coffee Beans 250g",
  "sku": "SKU-001",
  "categoryId": 15,
  "brandId": 4,
  "supplierId": 9,
  "baseUnitId": 1,
  "costPrice": 2.0000,
  "sellingPrice": 3.5000,
  "taxId": 2,
  "isTaxInclusive": true,
  "trackInventory": true,
  "minQuantity": 10,
  "reorderQuantity": 50,
  "barcodes": [{ "barcode": "6291000001", "isPrimary": true }],
  "units": [
    { "unitId": 1, "conversionFactor": 1, "isBaseUnit": true },
    { "unitId": 3, "conversionFactor": 12, "sellingPrice": 40.0000 }
  ],
  "customFields": { "origin": "Brazil", "roast": "Medium" }
}
```

### Response (201 Created)

```json
{
  "id": 1024,
  "publicId": "9f1c8b2e-3a44-4e11-9d0a-77c0f1e2a5b3",
  "sku": "SKU-001",
  "name": "Coffee Beans 250g",
  "sellingPrice": 3.5000,
  "createdDate": "2026-07-13T09:12:44.120Z"
}
```

### مثال: PATCH /products/1024/price (Request/Response)

```json
// Request
{ "priceType": "Selling", "newPrice": 3.9000, "reason": "Supplier cost increase" }

// Response 200
{
  "productId": 1024,
  "oldPrice": 3.5000,
  "newPrice": 3.9000,
  "changedAt": "2026-07-13T10:05:00.000Z",
  "historyId": 88
}
```

---

## 10) مخطط التدفّق (Flow Chart)

```
 [Open Product Form]
        │
        ▼
 [Fill General + Pricing]
        │
        ▼
 [Add Barcodes / Units / Variants]
        │
        ▼
 ┌───────────────┐   fail
 │  Validation   ├────────► [Show field errors]
 └──────┬────────┘
        │ pass
        ▼
 [BEGIN TRANSACTION]
   ├─ INSERT Products
   ├─ INSERT ProductUnits (base + others)
   ├─ INSERT ProductBarcodes
   ├─ INSERT ProductVariants
   └─ INSERT AuditLog
 [COMMIT] ──► [Return 201] ──► [Refresh list]
```

---

## 11) ماذا يحدث عند: الحذف / التعديل / تغيير السعر

### الحذف (Delete)
- **Soft Delete فقط:** `IsDeleted=1`, `DeletedDate`, `DeletedBy`.
- يُمنع الحذف إن كان للمنتج **حركات مخزون أو بنود فواتير** مرتبطة (فحص مسبق) — يُقترح **Deactivate** بدل الحذف.
- عند الحذف تُؤرشف الأبناء (Barcodes/Units/Variants) بنفس العملية داخل Transaction.

### التعديل (Edit)
- تحديث الحقول مع تحقّق التزامن عبر `ConcurrencyStamp` (409 عند التعارض).
- تعديل الوحدات/الباركود يمرّ بنفس قواعد التفرّد.
- كل تعديل يُسجَّل في `AuditLogs` مع القيم القديمة/الجديدة.

### تغيير السعر (Price Change) — الأهم
1. يمرّ حصراً عبر `PATCH /products/{id}/price` بصلاحية `products.price.edit`.
2. داخل Transaction:
   - **تحديث السعر النشط** في `ProductPrices` (`Price = NewPrice`) للنوع/الوحدة/الفرع المعني.
   - **إدراج سجل في `ProductPriceHistory`** يحمل (`OldPrice`, `NewPrice`, `ChangedByUserId`, `ChangedAt`, `Reason`).
   - تحديث `Products.DefaultSellPrice` إن كان النوع المتغيّر هو الافتراضي (للقراءة السريعة).
3. **الفواتير القديمة لا تتأثّر إطلاقاً:** كل بند فاتورة مُرحَّل يحمل snapshot للسعر لحظة البيع (`SalesInvoiceItems.UnitPrice/TaxAmount/DiscountAmount`)؛ فالسعر الجديد يسري فقط على الفواتير الجديدة.
4. **الفصل بين الحالي والتاريخ:** `ProductPrices` يحمل السعر **الحالي** فقط (يُقرأ عند البيع)، و`ProductPriceHistory` يحمل **سجل التغييرات** (للتدقيق والتقارير) — جدولان منفصلان.
5. التقارير التاريخية تستند إلى snapshot البنود، لا إلى سعر المنتج الحالي — ما يضمن دقة الأرباح التاريخية.

---

## 12) سجل التدقيق (Audit Log)

| الحدث | يُسجَّل في AuditLogs |
|-------|---------------------|
| Create Product | `Entity=Product, Action=Create, After={...}` |
| Update Product | `Before / After` diff للحقول المتغيّرة |
| Price Change | `Action=PriceChange` + مرجع `ProductPriceHistory.Id` (سجل التغيير) |
| Delete (Archive) | `Action=Delete, DeletedBy, DeletedDate` |

كل سجل يتضمّن `TenantId, UserId, IpAddress, UtcTimestamp`.

---

## 13) الأخطاء المحتملة (Possible Errors)

| الكود | السبب | المعالجة |
|-------|-------|----------|
| `400 SKU_REQUIRED` | SKU فارغ | إظهار خطأ الحقل |
| `409 SKU_DUPLICATE` | SKU مكرّر داخل المستأجر | اقتراح SKU بديل |
| `409 BARCODE_DUPLICATE` | باركود مستخدم لمنتج آخر | إظهار المنتج المالك للباركود |
| `422 NO_BASE_UNIT` | لا توجد وحدة أساسية | إلزام تحديد وحدة أساسية واحدة |
| `409 CONCURRENCY_CONFLICT` | تعارض `ConcurrencyStamp` | إعادة تحميل ثم المحاولة |
| `409 PRODUCT_IN_USE` | حذف منتج له حركات | اقتراح Deactivate |

---

## 14) الأداء (Performance)

- فهارس مُرشَّحة على `(TenantId, SKU)` و`(TenantId, Barcode)` لبحث فوري في POS.
- بحث الباركود عبر `GET /by-barcode` يستخدم فهرس فريد → seek بدل scan.
- الاستعلامات تُرقَّم (Pagination) بحدّ أقصى 100 لكل صفحة، مع `AsNoTracking()` للقراءات.
- صور المنتجات تُخزَّن خارج القاعدة (Object Storage/CDN)، القاعدة تحفظ الرابط فقط.
- Cache قائمة التصنيفات/العلامات المستخدمة في القوائم المنسدلة (Redis لاحقاً).

---

## 15) الأمان (Security)

- كل Endpoint محميّ بـ Policy مطابق للصلاحية.
- `TenantId` يُحقن من الـ Token عبر `ITenantProvider` ولا يُقبل من العميل (منع IDOR).
- سعر الكلفة يُحجب في الاستجابة عند غياب `products.cost.view`.
- التحقق من الملكية: كل عملية على `{id}` تتأكّد أن المنتج ضمن مستأجر المستخدم (Global Query Filter).
- رفع الصور: فحص نوع الملف وحجمه، وإعادة تسميته، ومنع تنفيذ المحتوى.
- كل الكتابات داخل Transaction وUnitOfWork لضمان الذرّية.
