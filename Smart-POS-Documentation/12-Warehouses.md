# 12 — Warehouses (المستودعات، التحويل، التسوية، والتالف)

> يوثّق هذا الملف وحدة **المستودعات (Warehouses)** في Smart ERP POS: مستودعات غير محدودة لكل مستأجر/فرع، **التحويل بين المستودعات (Stock Transfer)**، **تسوية المخزون (Stock Adjustment)**، ومعالجة **التالف/المنتهي/المفقود (Damaged / Expired / Lost)**، إضافةً إلى **سجل حركة المستودع (Warehouse History)**.
> كل ما يتعلّق بالرصيد اللحظي وحركاته (`Stock`, `StockMovements`) موثّق في [13-Inventory.md](13-Inventory.md)، وهذا الملف يعتمد عليه كمرجع للحركات المحاسبية للمخزون. يلتزم هذا الملف حرفياً بـ [04-Database-Design.md](04-Database-Design.md).

---

## 1) الهدف (Purpose)

- إدارة **عدد غير محدود** من المستودعات (Warehouses) لكل مستأجر، مربوطة بفرع (`StoreId`) أو مشتركة على مستوى الشركة.
- تعريف نوع كل مستودع (رئيسي، فرعي، مستودع تالف/حجر Quarantine، مستودع عرض Showroom، عربة بيع).
- تنفيذ **التحويل الداخلي** للبضاعة بين مستودعين ضمن **معاملة ذرّية واحدة** (خصم من المصدر + إضافة للوجهة).
- تنفيذ **تسويات الجرد** (زيادة/نقص) بعد الجرد الفعلي، مع تسبيب إلزامي.
- توثيق **التالف والمنتهي والمفقود** كنوع خاص من التسوية السالبة، لأغراض محاسبية وضريبية.
- توفير **سجل حركة كامل (History)** لكل مستودع: مَن، متى، ماذا، ولماذا.

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية (Permission Claim) | الوصف |
|-----------------------------|-------|
| `Warehouses.View` | عرض قائمة المستودعات وتفاصيلها |
| `Warehouses.Create` | إنشاء مستودع جديد |
| `Warehouses.Edit` | تعديل بيانات مستودع |
| `Warehouses.Delete` | حذف (Soft Delete) مستودع |
| `StockTransfers.Create` | إنشاء أمر تحويل |
| `StockTransfers.Approve` | اعتماد/ترحيل التحويل (تنفيذ الحركة الفعلية) |
| `StockTransfers.Receive` | استلام التحويل في المستودع الوجهة |
| `StockAdjustments.Create` | إنشاء تسوية مخزون |
| `StockAdjustments.Approve` | اعتماد التسوية (لها أثر مالي) |
| `Warehouses.ViewHistory` | عرض سجل حركة المستودع |

> كل الصلاحيات تُقيَّد تلقائياً بـ `TenantId` عبر Global Query Filter. صلاحية `Approve` منفصلة عن `Create` لتطبيق **مبدأ الفصل بين المهام (Segregation of Duties)** — مَن ينشئ التسوية لا يعتمدها بالضرورة.

---

## 3) تصميم الصفحة (Page Layout)

```
┌──────────────────────────────────────────────────────────────────────┐
│  المستودعات                          [+ مستودع جديد] [تحويل] [تسوية]  │
├──────────────────────────────────────────────────────────────────────┤
│  بحث: [__________]   الفرع: [الكل ▼]   النوع: [الكل ▼]  [بحث]         │
├──────────────────────────────────────────────────────────────────────┤
│  الكود │ الاسم        │ الفرع    │ النوع     │ افتراضي │ الحالة │ إجراءات│
│  WH-01 │ الرئيسي      │ فرع صنعاء│ Main      │  ✔      │ نشط    │ ✏ 🗑 📊 │
│  WH-02 │ مستودع تالف  │ فرع صنعاء│ Damaged   │         │ نشط    │ ✏ 🗑 📊 │
│  WH-03 │ عربة بيع 1   │ فرع عدن  │ Van       │         │ نشط    │ ✏ 🗑 📊 │
├──────────────────────────────────────────────────────────────────────┤
│                                            صفحة 1 من 3   [< 1 2 3 >]   │
└──────────────────────────────────────────────────────────────────────┘
```

**تبويبات تفاصيل المستودع:** [البيانات الأساسية] · [الأرصدة الحالية] · [التحويلات] · [التسويات] · [سجل الحركة].

---

## 4) الأزرار (Buttons)

| الزر | الصلاحية | الوظيفة |
|------|----------|---------|
| **+ مستودع جديد** | `Warehouses.Create` | فتح نموذج إنشاء |
| **حفظ** | Create/Edit | حفظ البيانات (POST/PUT) |
| **حذف** | `Warehouses.Delete` | Soft Delete (بشرط أن يكون الرصيد صفراً) |
| **تحويل بضاعة** | `StockTransfers.Create` | فتح شاشة أمر تحويل |
| **اعتماد التحويل** | `StockTransfers.Approve` | ترحيل: خصم من المصدر |
| **استلام التحويل** | `StockTransfers.Receive` | إضافة الكمية للوجهة |
| **تسوية جرد** | `StockAdjustments.Create` | فتح شاشة التسوية |
| **اعتماد التسوية** | `StockAdjustments.Approve` | تطبيق الأثر على الرصيد |
| **تسجيل تالف/منتهي** | `StockAdjustments.Create` | تسوية سالبة بسبب `Damaged/Expired` |
| **سجل الحركة (📊)** | `Warehouses.ViewHistory` | عرض `StockMovements` للمستودع |

---

## 5) الحقول (Fields)

### 5.1 حقول المستودع (Warehouse)
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|--------|---------|
| `Code` | string(20) | ✔ | فريد داخل المستأجر |
| `Name` | string(150) | ✔ | اسم المستودع |
| `StoreId` | bigint | ✖ | الفرع (NULL = شركة) |
| `WarehouseType` | enum | ✔ | Main / Sub / Damaged / Quarantine / Showroom / Van |
| `IsDefault` | bool | ✔ | مستودع افتراضي واحد فقط للفرع |
| `AllowNegativeStock` | bool | ✔ | افتراضي `false` |
| `ManagerUserId` | bigint | ✖ | مسؤول المستودع |
| `Address` | string(300) | ✖ | العنوان الفعلي |
| `IsActive` | bool | ✔ | تعطيل دون حذف |

### 5.2 حقول أمر التحويل (StockTransfer)
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|--------|---------|
| `TransferNumber` | string | ✔ | من `Sequences` (`STOCK_TRANSFER`) |
| `FromWarehouseId` | bigint | ✔ | ≠ الوجهة |
| `ToWarehouseId` | bigint | ✔ | ≠ المصدر |
| `TransferDate` | datetime2 | ✔ | UTC |
| `Status` | enum | ✔ | Draft / Approved / InTransit / Received / Cancelled |
| `Notes` | string(500) | ✖ | |
| **البنود** | list | ✔ | `ProductId`, `Quantity`, `UnitId`, `UnitCost` |

### 5.3 حقول التسوية (StockAdjustment)
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|--------|---------|
| `AdjustmentNumber` | string | ✔ | من `Sequences` (`STOCK_ADJUSTMENT`) |
| `WarehouseId` | bigint | ✔ | |
| `AdjustmentType` | enum | ✔ | Increase / Decrease / Damaged / Expired / Lost / StockCount |
| `Reason` | string(500) | ✔ | **إلزامي** لأي تسوية |
| `Status` | enum | ✔ | Draft / Approved / Cancelled |
| **البنود** | list | ✔ | `ProductId`, `SystemQty`, `ActualQty`, `DifferenceQty`, `UnitCost` |

---

## 6) التحقق (Validation)

- `FromWarehouseId ≠ ToWarehouseId` في التحويل (خطأ `TRANSFER_SAME_WAREHOUSE`).
- كل بند تحويل: `Quantity > 0`، وبعد التحويل الرصيد لدى المصدر ≥ 0 (ما لم يُفعَّل `AllowNegativeStock`).
- التحويل يجب أن يحتوي بنداً واحداً على الأقل.
- التسوية: `Reason` غير فارغ، و`ActualQty ≥ 0`، و`DifferenceQty = ActualQty − SystemQty` تُحسب خادِمياً لا من العميل.
- المستودع الافتراضي: لا يُسمح بأكثر من `IsDefault = 1` لكل `(TenantId, StoreId)`.
- الحذف: يُرفض إذا وُجد أي رصيد `Stock.Quantity > 0` أو حركات غير مُرحّلة (خطأ `WAREHOUSE_NOT_EMPTY`).
- `Code` فريد ضمن المستأجر (unique filtered index).

---

## 7) قواعد العمل (Business Rules)

1. **التحويل ذرّي:** خصم المصدر + إضافة الوجهة يحدثان داخل **معاملة SQL واحدة**. أي فشل جزئي ⇒ Rollback كامل. لا وجود لحالة «خُصم ولم يُضف».
2. **دورة حياة التحويل بمرحلتين (اختياري):** إن فُعِّل نمط «مع الاستلام»، عند الاعتماد يُخصَم من المصدر ويصبح `InTransit`، وعند الاستلام يُضاف للوجهة ويصبح `Received`. إن كان النقل فورياً، فالمرحلتان تندمجان في اعتماد واحد.
3. **تكلفة التحويل:** تُنقَل التكلفة الحالية (Weighted Average Cost) من المصدر إلى الوجهة، فلا يتغيّر متوسط تكلفة المؤسسة نتيجة تحويل داخلي (التحويل **محايد التكلفة على مستوى الشركة**).
4. **التالف/المنتهي/المفقود** تسويات سالبة تُخفّض الرصيد وتُنشئ قيد إتلاف محاسبي (يخرج البند من المخزون بتكلفته). المنتهي (Expired) يعتمد على `ExpiryDate` من دفعات المخزون إن كان نظام الدفعات مفعّلاً.
5. **الجرد (StockCount):** تسوية من نوع `StockCount` تقارن `SystemQty` بـ `ActualQty` وتولّد فرقاً موجباً أو سالباً لكل بند، ثم تُعتمد دفعة واحدة.
6. **كل حركة تُسجَّل:** أي تحويل أو تسوية معتمَدة تُنشئ سجلات في `StockMovements` (مرجعها نوع المستند ورقمه) — لا رصيد يتغيّر دون حركة مُسجَّلة.
7. **منع الرصيد السالب:** افتراضياً يُرفض أي خصم يجعل الرصيد سالباً، إلا إذا فُعِّل `AllowNegativeStock` على المستودع المصدر.
8. **الفصل بين المهام:** التسوية `Draft` بلا أثر على الرصيد؛ الأثر يحدث فقط عند `Approve` من مستخدم يملك صلاحية الاعتماد.

---

## 8) جداول قاعدة البيانات (Database Tables — Full SQL)

> جميع الجداول ترث الأعمدة المشتركة الإلزامية وتلتزم بقالب [04-Database-Design.md](04-Database-Design.md).

### 8.1 Warehouses

```sql
CREATE TABLE [dbo].[Warehouses]
(
    [Code]               VARCHAR(20)   NOT NULL,
    [Name]               NVARCHAR(150) NOT NULL,
    [WarehouseType]      TINYINT       NOT NULL CONSTRAINT DF_Warehouses_Type DEFAULT (1),
        -- 1=Main, 2=Sub, 3=Damaged, 4=Quarantine, 5=Showroom, 6=Van
    [IsDefault]          BIT           NOT NULL CONSTRAINT DF_Warehouses_IsDefault DEFAULT (0),
    [AllowNegativeStock] BIT           NOT NULL CONSTRAINT DF_Warehouses_AllowNeg DEFAULT (0),
    [ManagerUserId]      BIGINT        NULL,
    [Address]            NVARCHAR(300) NULL,
    [IsActive]           BIT           NOT NULL CONSTRAINT DF_Warehouses_IsActive DEFAULT (1),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_Warehouses_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_Warehouses_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_Warehouses] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Warehouses_Tenant]  FOREIGN KEY ([TenantId])      REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_Warehouses_Store]   FOREIGN KEY ([StoreId])       REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_Warehouses_Manager] FOREIGN KEY ([ManagerUserId]) REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT [CK_Warehouses_Type] CHECK ([WarehouseType] BETWEEN 1 AND 6)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Warehouses_Tenant_Code]
    ON [dbo].[Warehouses] ([TenantId], [Code]) WHERE [IsDeleted] = 0;
GO
-- مستودع افتراضي واحد فقط لكل فرع
CREATE UNIQUE NONCLUSTERED INDEX [UX_Warehouses_Default_PerStore]
    ON [dbo].[Warehouses] ([TenantId], [StoreId])
    WHERE [IsDeleted] = 0 AND [IsDefault] = 1;
GO
CREATE NONCLUSTERED INDEX [IX_Warehouses_Tenant_Store]
    ON [dbo].[Warehouses] ([TenantId], [StoreId]) WHERE [IsDeleted] = 0;
GO
```

### 8.2 StockTransfers

```sql
CREATE TABLE [dbo].[StockTransfers]
(
    [TransferNumber]   VARCHAR(30)   NOT NULL,
    [FromWarehouseId]  BIGINT        NOT NULL,
    [ToWarehouseId]    BIGINT        NOT NULL,
    [TransferDate]     DATETIME2(3)  NOT NULL,
    [Status]           TINYINT       NOT NULL CONSTRAINT DF_StockTransfers_Status DEFAULT (1),
        -- 1=Draft, 2=Approved, 3=InTransit, 4=Received, 5=Cancelled
    [TotalCost]        DECIMAL(18,4) NOT NULL CONSTRAINT DF_StockTransfers_Total DEFAULT (0),
    [ApprovedBy]       BIGINT        NULL,
    [ApprovedDate]     DATETIME2(3)  NULL,
    [ReceivedBy]       BIGINT        NULL,
    [ReceivedDate]     DATETIME2(3)  NULL,
    [Notes]            NVARCHAR(500) NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_StockTransfers_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_StockTransfers_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_StockTransfers] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_StockTransfers_Tenant] FOREIGN KEY ([TenantId])        REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_StockTransfers_Store]  FOREIGN KEY ([StoreId])         REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_StockTransfers_From]   FOREIGN KEY ([FromWarehouseId]) REFERENCES [dbo].[Warehouses]([Id]),
    CONSTRAINT [FK_StockTransfers_To]     FOREIGN KEY ([ToWarehouseId])   REFERENCES [dbo].[Warehouses]([Id]),
    CONSTRAINT [CK_StockTransfers_DiffWh] CHECK ([FromWarehouseId] <> [ToWarehouseId]),
    CONSTRAINT [CK_StockTransfers_Status] CHECK ([Status] BETWEEN 1 AND 5)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_StockTransfers_Tenant_Number]
    ON [dbo].[StockTransfers] ([TenantId], [TransferNumber]) WHERE [IsDeleted] = 0;
GO
CREATE NONCLUSTERED INDEX [IX_StockTransfers_From_Status]
    ON [dbo].[StockTransfers] ([TenantId], [FromWarehouseId], [Status]) WHERE [IsDeleted] = 0;
GO
CREATE NONCLUSTERED INDEX [IX_StockTransfers_To_Status]
    ON [dbo].[StockTransfers] ([TenantId], [ToWarehouseId], [Status]) WHERE [IsDeleted] = 0;
GO
```

### 8.3 StockTransferItems

```sql
CREATE TABLE [dbo].[StockTransferItems]
(
    [StockTransferId]  BIGINT        NOT NULL,
    [ProductId]        BIGINT        NOT NULL,
    [UnitId]           BIGINT        NOT NULL,
    [Quantity]         DECIMAL(18,4) NOT NULL,
    [UnitCost]         DECIMAL(18,4) NOT NULL,   -- تكلفة الوحدة المنقولة (WAC من المصدر)
    [LineCost]         DECIMAL(18,4) NOT NULL,   -- Quantity * UnitCost
    [BatchNumber]      NVARCHAR(50)  NULL,
    [ExpiryDate]       DATE          NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_StockTransferItems_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_StockTransferItems_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_StockTransferItems] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_STItems_Transfer] FOREIGN KEY ([StockTransferId]) REFERENCES [dbo].[StockTransfers]([Id]),
    CONSTRAINT [FK_STItems_Product]  FOREIGN KEY ([ProductId])       REFERENCES [dbo].[Products]([Id]),
    CONSTRAINT [FK_STItems_Unit]     FOREIGN KEY ([UnitId])          REFERENCES [dbo].[Units]([Id]),
    CONSTRAINT [FK_STItems_Tenant]   FOREIGN KEY ([TenantId])        REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [CK_STItems_Qty] CHECK ([Quantity] > 0)
);
GO

CREATE NONCLUSTERED INDEX [IX_STItems_Transfer]
    ON [dbo].[StockTransferItems] ([StockTransferId]) WHERE [IsDeleted] = 0;
GO
CREATE NONCLUSTERED INDEX [IX_STItems_Product]
    ON [dbo].[StockTransferItems] ([TenantId], [ProductId]) WHERE [IsDeleted] = 0;
GO
```

### 8.4 StockAdjustments

```sql
CREATE TABLE [dbo].[StockAdjustments]
(
    [AdjustmentNumber] VARCHAR(30)   NOT NULL,
    [WarehouseId]      BIGINT        NOT NULL,
    [AdjustmentType]   TINYINT       NOT NULL,
        -- 1=Increase, 2=Decrease, 3=Damaged, 4=Expired, 5=Lost, 6=StockCount
    [Reason]           NVARCHAR(500) NOT NULL,   -- إلزامي
    [Status]           TINYINT       NOT NULL CONSTRAINT DF_StockAdjustments_Status DEFAULT (1),
        -- 1=Draft, 2=Approved, 3=Cancelled
    [TotalCostImpact]  DECIMAL(18,4) NOT NULL CONSTRAINT DF_StockAdj_Total DEFAULT (0),
    [ApprovedBy]       BIGINT        NULL,
    [ApprovedDate]     DATETIME2(3)  NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_StockAdjustments_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_StockAdjustments_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_StockAdjustments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_StockAdj_Tenant]    FOREIGN KEY ([TenantId])    REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_StockAdj_Store]     FOREIGN KEY ([StoreId])     REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_StockAdj_Warehouse] FOREIGN KEY ([WarehouseId]) REFERENCES [dbo].[Warehouses]([Id]),
    CONSTRAINT [CK_StockAdj_Type]   CHECK ([AdjustmentType] BETWEEN 1 AND 6),
    CONSTRAINT [CK_StockAdj_Status] CHECK ([Status] BETWEEN 1 AND 3)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_StockAdj_Tenant_Number]
    ON [dbo].[StockAdjustments] ([TenantId], [AdjustmentNumber]) WHERE [IsDeleted] = 0;
GO
CREATE NONCLUSTERED INDEX [IX_StockAdj_Warehouse_Status]
    ON [dbo].[StockAdjustments] ([TenantId], [WarehouseId], [Status]) WHERE [IsDeleted] = 0;
GO
```

### 8.5 StockAdjustmentItems

```sql
CREATE TABLE [dbo].[StockAdjustmentItems]
(
    [StockAdjustmentId] BIGINT        NOT NULL,
    [ProductId]         BIGINT        NOT NULL,
    [UnitId]            BIGINT        NOT NULL,
    [SystemQty]         DECIMAL(18,4) NOT NULL,   -- الرصيد الدفتري وقت الجرد
    [ActualQty]         DECIMAL(18,4) NOT NULL,   -- الرصيد الفعلي المعدود
    [DifferenceQty]     DECIMAL(18,4) NOT NULL,   -- ActualQty - SystemQty (محسوب خادِمياً)
    [UnitCost]          DECIMAL(18,4) NOT NULL,   -- WAC وقت التسوية
    [CostImpact]        DECIMAL(18,4) NOT NULL,   -- DifferenceQty * UnitCost
    [BatchNumber]       NVARCHAR(50)  NULL,
    [ExpiryDate]        DATE          NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_StockAdjItems_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_StockAdjItems_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_StockAdjustmentItems] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SAItems_Adjustment] FOREIGN KEY ([StockAdjustmentId]) REFERENCES [dbo].[StockAdjustments]([Id]),
    CONSTRAINT [FK_SAItems_Product]    FOREIGN KEY ([ProductId])         REFERENCES [dbo].[Products]([Id]),
    CONSTRAINT [FK_SAItems_Unit]       FOREIGN KEY ([UnitId])            REFERENCES [dbo].[Units]([Id]),
    CONSTRAINT [FK_SAItems_Tenant]     FOREIGN KEY ([TenantId])          REFERENCES [dbo].[Tenants]([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_SAItems_Adjustment]
    ON [dbo].[StockAdjustmentItems] ([StockAdjustmentId]) WHERE [IsDeleted] = 0;
GO
CREATE NONCLUSTERED INDEX [IX_SAItems_Product]
    ON [dbo].[StockAdjustmentItems] ([TenantId], [ProductId]) WHERE [IsDeleted] = 0;
GO
```

---

## 9) الـ API

| Method | Endpoint | الصلاحية | الوصف |
|--------|----------|----------|-------|
| GET | `/api/v1/warehouses` | `Warehouses.View` | قائمة (paged, filter) |
| GET | `/api/v1/warehouses/{id}` | `Warehouses.View` | تفاصيل |
| POST | `/api/v1/warehouses` | `Warehouses.Create` | إنشاء |
| PUT | `/api/v1/warehouses/{id}` | `Warehouses.Edit` | تعديل |
| DELETE | `/api/v1/warehouses/{id}` | `Warehouses.Delete` | Soft delete |
| POST | `/api/v1/stock-transfers` | `StockTransfers.Create` | إنشاء أمر تحويل (Draft) |
| POST | `/api/v1/stock-transfers/{id}/approve` | `StockTransfers.Approve` | ترحيل (خصم المصدر) |
| POST | `/api/v1/stock-transfers/{id}/receive` | `StockTransfers.Receive` | استلام (إضافة للوجهة) |
| POST | `/api/v1/stock-adjustments` | `StockAdjustments.Create` | إنشاء تسوية |
| POST | `/api/v1/stock-adjustments/{id}/approve` | `StockAdjustments.Approve` | اعتماد وتطبيق الأثر |
| GET | `/api/v1/warehouses/{id}/history` | `Warehouses.ViewHistory` | سجل حركة المستودع |

**مثال — إنشاء تحويل (Request):**
```json
POST /api/v1/stock-transfers
{
  "fromWarehouseId": 1,
  "toWarehouseId": 2,
  "transferDate": "2026-07-13T09:00:00Z",
  "notes": "نقل فائض للمعرض",
  "items": [
    { "productId": 501, "unitId": 1, "quantity": 20 },
    { "productId": 502, "unitId": 1, "quantity": 5 }
  ]
}
```
**Response `201 Created`:**
```json
{
  "id": 88,
  "transferNumber": "TRF-000088",
  "status": "Draft",
  "totalCost": 640.0000,
  "items": [
    { "productId": 501, "quantity": 20, "unitCost": 12.5000, "lineCost": 250.0000 },
    { "productId": 502, "quantity": 5,  "unitCost": 78.0000, "lineCost": 390.0000 }
  ]
}
```

**مثال — اعتماد تسوية تالف (Request):**
```json
POST /api/v1/stock-adjustments
{
  "warehouseId": 3,
  "adjustmentType": "Damaged",
  "reason": "كسر أثناء التخزين — تقرير رقم 2026-114",
  "items": [ { "productId": 501, "unitId": 1, "actualQty": 0, "systemQty": 3 } ]
}
```

---

## 10) مخطط التدفّق (Flow Chart) — التحويل بين مستودعين

```
        [إنشاء أمر تحويل Draft]
                 │
                 ▼
   ┌───── هل المصدر ≠ الوجهة؟ ─────┐
   │NO                            │YES
   ▼                              ▼
[رفض SAME_WH]        [التحقق: رصيد المصدر كافٍ لكل بند؟]
                                 │
                     ┌───────────┴───────────┐
                     │NO                      │YES
                     ▼                        ▼
          [رفض INSUFFICIENT]       ┌── BEGIN TRANSACTION ──┐
          (إلا AllowNegative)      │  UPDATE Stock (المصدر) │
                                   │     Quantity -= Qty     │
                                   │  INSERT StockMovements  │
                                   │     (Transfer_Out)      │
                                   │  UPDATE/INSERT Stock    │
                                   │     (الوجهة) += Qty     │
                                   │  INSERT StockMovements  │
                                   │     (Transfer_In)       │
                                   │  Status = Received      │
                                   └──── COMMIT / ROLLBACK ──┘
                                             │
                                             ▼
                                    [تسجيل Audit Log]
```

---

## 11) ماذا يحدث عند: الحذف / التعديل / الإلغاء

- **حذف مستودع:** يُرفض إن كان له رصيد > 0 أو حركات مفتوحة. عند السماح ⇒ `IsDeleted = 1` + `DeletedBy/DeletedDate` (لا حذف فعلي، حفاظاً على السجل).
- **تعديل مستودع:** يُسمح بتعديل الاسم/المدير/العنوان/`AllowNegativeStock`. تغيير النوع من/إلى `Damaged` قد يُقيَّد إن وُجدت حركات مرتبطة.
- **تعديل تحويل `Draft`:** مسموح كاملاً (لا أثر بعد على الرصيد). بعد `Approved` ⇒ لا يُعدَّل، بل يُلغى بتحويل عكسي (Reversal Transfer).
- **إلغاء تحويل معتمَد:** يُنشئ تحويلاً عكسياً يُعيد الكمية للمصدر ويُنشئ حركتَي `Transfer_In`/`Transfer_Out` عكسيتين — لا يُعدَّل السجل الأصلي.
- **إلغاء تسوية معتمَدة:** يُنشئ تسوية عكسية بنفس الكميات وبإشارة معاكسة، مع ربطها بالأصل (`ReversalOf`).

---

## 12) سجل التدقيق (Audit Log)

يُسجَّل في `AuditLogs` (انظر [04-Database-Design.md](04-Database-Design.md) القسم 9) لكل عملية:
`TenantId, UserId, Entity('StockTransfer'|'StockAdjustment'|'Warehouse'), EntityId, Action('Create'|'Approve'|'Receive'|'Cancel'|'Delete'), OldValuesJson, NewValuesJson, IpAddress, CreatedDate(UTC)`.
كل تغيير رصيد ناتج يُوثَّق أيضاً كسجل `StockMovements` مستقل (المرجع = رقم المستند)، فيتقاطع التدقيق المحاسبي مع تدقيق العمليات.

---

## 13) الأخطاء المحتملة (Possible Errors)

| الرمز | HTTP | السبب |
|-------|------|-------|
| `TRANSFER_SAME_WAREHOUSE` | 400 | المصدر = الوجهة |
| `INSUFFICIENT_STOCK` | 409 | رصيد المصدر لا يكفي و`AllowNegativeStock=false` |
| `WAREHOUSE_NOT_EMPTY` | 409 | محاولة حذف مستودع برصيد > 0 |
| `ADJUSTMENT_REASON_REQUIRED` | 400 | سبب التسوية فارغ |
| `ALREADY_APPROVED` | 409 | إعادة اعتماد مستند مُعتمَد |
| `CONCURRENCY_CONFLICT` | 409 | تعارض `ConcurrencyStamp` |
| `DEFAULT_WAREHOUSE_EXISTS` | 409 | تعيين افتراضي ثانٍ للفرع نفسه |

---

## 14) الأداء (Performance)

- فهارس مُرشَّحة `(TenantId, WarehouseId, Status)` تسرّع لوحات «التحويلات المفتوحة».
- عمليات التحويل والتسوية تُغلَّف في معاملة قصيرة تحت `READ COMMITTED SNAPSHOT` لتقليل الأقفال.
- قفل صف الرصيد عبر `UPDATE ... WITH (UPDLOCK, ROWLOCK)` على `Stock` لتفادي حالة السباق (race) عند الخصم المتزامن.
- سجل الحركة يُصفّح (paging) عبر `OFFSET/FETCH` مع فهرس على `(TenantId, WarehouseId, CreatedDate DESC)`.
- التسويات الكبيرة (جرد آلاف الأصناف) تُعالَج على دفعات (batching) داخل معاملة واحدة لتفادي تضخّم Log.

---

## 15) الأمان (Security)

- كل endpoint محميّ بـ JWT + فحص Permission Claim + Global Query Filter على `TenantId`.
- **الفصل بين المهام:** `Create` منفصل عن `Approve` لمنع التلاعب بالمخزون من مستخدم واحد.
- التسويات السالبة (تالف/مفقود) تُعامَل كعمليات حسّاسة تتطلّب اعتماداً وتُسجَّل بالكامل — درعٌ ضدّ التلاعب والاختلاس.
- منع IDOR عبر `TenantId` في كل استعلام ومنع كشف `Id` التسلسلي مباشرة في الروابط الحسّاسة.
- التحقّق من أن `FromWarehouseId`/`ToWarehouseId` يعودان لنفس المستأجر قبل أي معالجة (منع تحويل بين مستأجرين).
