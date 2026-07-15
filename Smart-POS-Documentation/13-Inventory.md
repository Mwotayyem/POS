# 13 — Inventory (المخزون الأساسي وحركاته)

> يوثّق هذا الملف **قلب النظام المخزوني** في Smart ERP POS: الرصيد اللحظي لكل منتج في كل مستودع (`Stock`)، وسجل **كل حركة مخزون** بنوعها ومرجعها والكمية قبل/بعد (`StockMovements`). يلتزم حرفياً بمعايير [04-Database-Design.md](04-Database-Design.md)، ويكمّل [12-Warehouses.md](12-Warehouses.md).
>
> **المبدأ المحوري:** المخزون يُحدَّث **تلقائياً وذرّياً** بعد كل عملية — **Purchase, Sale, Return, Transfer, Adjustment, Damage, Expiry** — ولا يتغيّر أي رصيد إلا وتُسجَّل له حركة مقابلة في `StockMovements`. لا يوجد UI مباشر لتعديل عمود `Stock.QtyOnHand` يدوياً؛ كل تغيير يمرّ عبر مستند مصدر.

---

## 1) الهدف (Purpose)

- الاحتفاظ بـ **رصيد لحظي دقيق** (`Stock`) لكل تركيبة `(Product, Warehouse)` — والدفعة `Batch` إن فُعِّلت.
- تسجيل **دفتر أستاذ حركي غير قابل للتعديل (append-only ledger)** لكل حركة مخزون في `StockMovements`.
- حساب **تكلفة المخزون** بطريقة **متوسط التكلفة المرجّح (Weighted Average Cost)** وتحديثها آلياً عند كل إدخال.
- تمكين **الجرد والمطابقة (Stock Count / Reconciliation)** ومنع **الرصيد السالب**.
- توفير **مصدر حقيقة واحد (Single Source of Truth)** للرصيد يُستمَدّ منه كل تقرير مخزوني ونقطة إعادة طلب (Reorder).

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية | الوصف |
|----------|-------|
| `Inventory.View` | عرض الأرصدة الحالية |
| `Inventory.ViewMovements` | عرض سجل الحركات (Ledger) |
| `Inventory.ViewCost` | عرض التكلفة (WAC) — صلاحية حسّاسة منفصلة |
| `Inventory.StockCount` | فتح جلسة جرد |
| `Inventory.Reconcile` | اعتماد المطابقة وتوليد تسوية |
| `Inventory.Export` | تصدير الأرصدة/الحركات |

> عرض **التكلفة** مفصول عن عرض **الكمية** — كثير من المستأجرين يمنعون الكاشير من رؤية تكلفة الشراء.

---

## 3) تصميم الصفحة (Page Layout)

```
┌──────────────────────────────────────────────────────────────────────────┐
│  المخزون الحالي                       [جرد جديد] [تصدير] [تحديث الأرصدة]  │
├──────────────────────────────────────────────────────────────────────────┤
│ المستودع:[الكل ▼] التصنيف:[الكل ▼] بحث منتج:[______]  [☐ تحت الحد فقط] 🔍 │
├──────────────────────────────────────────────────────────────────────────┤
│ المنتج          │ المستودع │ المتاح │ محجوز │ متاح للبيع │ متوسط تكلفة │ 📜│
│ حليب 1ل         │ الرئيسي  │  120   │   10  │    110     │   1.2500    │ 📜│
│ سكر 1كغ         │ الرئيسي  │   8 ⚠  │    0  │      8     │   0.9000    │ 📜│
│ زيت 1.5ل        │ المعرض   │   0 ⛔ │    0  │      0     │   3.4000    │ 📜│
├──────────────────────────────────────────────────────────────────────────┤
│  ⚠ = تحت حد الطلب   ⛔ = نفدت   📜 = دفتر الحركة   الإجمالي: 18,940.00     │
└──────────────────────────────────────────────────────────────────────────┘
```

**نافذة دفتر الحركة (📜):** جدول زمني تنازلي يعرض `Date, Type, Reference, QtyIn, QtyOut, QtyBefore, QtyAfter, UnitCost, User`.

---

## 4) الأزرار (Buttons)

| الزر | الصلاحية | الوظيفة |
|------|----------|---------|
| **جرد جديد** | `Inventory.StockCount` | فتح جلسة جرد (تجميد نظري للأرصدة) |
| **اعتماد الجرد** | `Inventory.Reconcile` | توليد `StockAdjustment` بالفروقات |
| **تصدير** | `Inventory.Export` | Excel/CSV للأرصدة أو الحركات |
| **تحديث الأرصدة** | `Inventory.View` | إعادة تحميل (لا يعيد حساب من الصفر) |
| **دفتر الحركة (📜)** | `Inventory.ViewMovements` | فتح Ledger للمنتج/المستودع |
| **إعادة احتساب** | `Inventory.Reconcile` | (إداري) إعادة بناء الرصيد من `StockMovements` |

---

## 5) الحقول (Fields)

### 5.1 رصيد المخزون (Stock)
| الحقل | النوع | ملاحظات |
|-------|------|---------|
| `ProductId` | bigint | المنتج |
| `WarehouseId` | bigint | المستودع |
| `Quantity` | decimal(18,4) | المتاح الكلي |
| `QtyOnHand` | decimal(18,4) | الرصيد الفعلي بالمخزن |
| `ReservedQty` | decimal(18,4) | محجوز (فواتير معلّقة/طلبات لم تُسلَّم) |
| `AvailableQty` | محسوب (PERSISTED) | `QtyOnHand − ReservedQty` — المتاح فعلياً للبيع |
| `AverageCost` | decimal(18,4) | WAC الحالي |
| `ReorderLevel` | decimal(18,4) | حد إعادة الطلب |
| `LastMovementDate` | datetime2 | آخر حركة UTC |

### 5.2 حركة المخزون (StockMovement)
| الحقل | النوع | ملاحظات |
|-------|------|---------|
| `MovementType` | tinyint (enum) | نوع الحركة (انظر §7.1) |
| `Direction` | tinyint | 1=In, 2=Out |
| `Quantity` | decimal(18,4) | موجب دائماً (الاتجاه في `Direction`) |
| `QuantityBefore` | decimal(18,4) | الرصيد قبل |
| `QuantityAfter` | decimal(18,4) | الرصيد بعد |
| `UnitCost` | decimal(18,4) | تكلفة الوحدة لهذه الحركة |
| `ReferenceType` | varchar(30) | نوع المستند المصدر |
| `ReferenceId` | bigint | مفتاح المستند المصدر |
| `ReferenceNumber` | varchar(30) | رقم المستند (للعرض) |

---

## 6) التحقق (Validation)

- كل حركة `Out` يجب أن تُبقي `QuantityAfter ≥ 0` (ما لم يُفعَّل `AllowNegativeStock` على المستودع).
- `QuantityAfter = QuantityBefore ± Quantity` — تُحسب خادِمياً وتُخزَّن (لا تُقبَل من العميل).
- `UnitCost ≥ 0`؛ في حركات `In` يُستخدَم لتحديث WAC، في حركات `Out` يُسجَّل WAC اللحظي.
- تفرّد الرصيد: صف واحد فقط لكل `(TenantId, WarehouseId, ProductId[, BatchNumber])`.
- لا تُقبَل حركة بكمية `0` أو سالبة.
- عملية الجرد لا تعدّل `Stock` مباشرةً — بل تولّد `StockAdjustment` يمرّ بدورة الاعتماد.

---

## 7) قواعد العمل (Business Rules)

### 7.1 أنواع الحركات (Movement Types Enum)

```
1  = Purchase_In        (استلام شراء)              → In,  يحدّث WAC
2  = Sale_Out           (بيع)                       → Out
3  = SaleReturn_In      (مرتجع بيع)                 → In
4  = PurchaseReturn_Out (مرتجع شراء)                → Out
5  = Transfer_Out       (تحويل صادر)                → Out
6  = Transfer_In        (تحويل وارد)                → In,  ينقل التكلفة
7  = Adjust_Increase    (تسوية زيادة)               → In,  يحدّث WAC
8  = Adjust_Decrease    (تسوية نقص)                 → Out
9  = Damaged_Out        (تالف)                      → Out
10 = Expired_Out        (منتهي الصلاحية)            → Out
11 = Lost_Out           (مفقود)                     → Out
12 = OpeningBalance_In  (رصيد افتتاحي)              → In,  يؤسّس WAC
13 = StockCount_Adjust  (تسوية جرد ±)               → In/Out حسب الفرق
```

### 7.2 قواعد التحديث التلقائي

| العملية المصدر | الحركة المولَّدة | أثر WAC |
|----------------|------------------|---------|
| فاتورة شراء (Receive) | `Purchase_In` | يُعاد حساب المتوسط المرجّح |
| فاتورة بيع / POS | `Sale_Out` | يُسجَّل WAC الحالي كتكلفة مبيع (COGS) |
| مرتجع بيع | `SaleReturn_In` | يعود بتكلفة البيع الأصلية (لا يشوّه WAC) |
| مرتجع شراء | `PurchaseReturn_Out` | يخرج بـ WAC الحالي |
| تحويل بين مستودعات | `Transfer_Out`+`Transfer_In` | محايد على مستوى الشركة |
| تسوية/تالف/منتهي/مفقود | `Adjust_*` / `Damaged_Out` … | نقص = COGS تلف؛ زيادة = تحدّث WAC |

### 7.3 تكلفة المخزون — القرار: Weighted Average Cost (WAC)

> **القرار المعتمد: Weighted Average Cost (المتوسط المرجّح).**

**المبرّر:**
- **بيئة POS متعدّدة القطاعات** (سوبرماركت، بقالة، صيدلية…) بأحجام حركة ضخمة؛ FIFO يتطلّب تتبّع كل طبقة تكلفة (cost layer) وربط كل بيع بطبقة — عبء حسابي وتخزيني كبير عند آلاف عمليات البيع يومياً.
- WAC يعطي **قيمة مخزون مستقرّة** ومقاومة لتذبذب أسعار الموردين، ويبسّط حساب تكلفة المبيع (COGS) لحظياً.
- يتوافق مع معظم القطاعات المستهدفة والمعايير المحاسبية الشائعة، وسهل التدقيق (رقم واحد لكل منتج/مستودع).

**معادلة إعادة الحساب عند كل إدخال (`In`):**
```
NewAvgCost = ((OldQty × OldAvgCost) + (InQty × InUnitCost)) / (OldQty + InQty)
```
- عند `Out`: الكمية تخرج بـ `AverageCost` الحالي (لا يتغيّر المتوسط).
- عند رصيد صفري ثم إدخال: `NewAvgCost = InUnitCost`.
- **تنبيه:** لتفادي انزلاق الأرقام العشرية، تُخزَّن التكلفة `DECIMAL(18,4)` ويُطبَّق تقريب بنكي (banker's rounding) عند العرض فقط، لا في التخزين.

> **بديل مستقبلي:** يُمكن تفعيل **FIFO/Batch costing** على مستوى المستأجر للصيدليات ذات تتبّع الدفعات وتواريخ الانتهاء عبر جدول طبقات تكلفة `StockCostLayers` (خارج نطاق هذا الإصدار الأساسي).

### 7.4 قواعد أخرى

1. **الذرّية:** تحديث `Stock` + إدراج `StockMovements` يحدثان في **نفس معاملة** المستند المصدر (Unit of Work) — أو لا يحدث أيّهما.
2. **منع الرصيد السالب:** يُرفض أي `Out` يُنزل الرصيد دون الصفر افتراضياً.
3. **الحجز (Reservation):** الفواتير المعلّقة (Suspended/Draft) تزيد `ReservedQty` دون خصم `QtyOnHand`؛ فيقلّ `AvailableQty` المحسوب تلقائياً. عند الترحيل يُفرَّغ الحجز ويُخصَم الفعلي من `QtyOnHand`.
4. **الرصيد الافتتاحي:** يُدخَل مرة واحدة كحركة `OpeningBalance_In` تؤسّس الكمية والتكلفة.
5. **قابلية إعادة البناء:** `SUM` لحركات `In` مطروحاً منها `Out` عبر `StockMovements` يجب أن يساوي `Stock.QtyOnHand` تماماً (فحص سلامة دوري).

---

## 8) جداول قاعدة البيانات (Database Tables — Full SQL)

### 8.1 Stock (الرصيد الحالي)

```sql
CREATE TABLE [dbo].[Stock]
(
    [ProductId]         BIGINT        NOT NULL,
    [WarehouseId]       BIGINT        NOT NULL,
    [BatchNumber]       NVARCHAR(50)  NULL,       -- NULL إن كان تتبّع الدفعات معطّلاً
    [ExpiryDate]        DATE          NULL,
    [QtyOnHand]         DECIMAL(18,4) NOT NULL CONSTRAINT DF_Stock_OnHand   DEFAULT (0),  -- الرصيد الفعلي بالمخزن
    [ReservedQty]       DECIMAL(18,4) NOT NULL CONSTRAINT DF_Stock_Reserved DEFAULT (0),  -- محجوز (فواتير معلّقة/طلبات لم تُسلَّم)
    -- المتاح للبيع = الفعلي − المحجوز (عمود محسوب، دائماً متّسق ولا يُخزَّن يدوياً)
    [AvailableQty]      AS ([QtyOnHand] - [ReservedQty]) PERSISTED,
    [AverageCost]       DECIMAL(18,4) NOT NULL CONSTRAINT DF_Stock_AvgCost  DEFAULT (0),
    [ReorderLevel]      DECIMAL(18,4) NOT NULL CONSTRAINT DF_Stock_Reorder  DEFAULT (0),
    [LastMovementDate]  DATETIME2(3)  NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_Stock_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_Stock_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_Stock] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Stock_Tenant]    FOREIGN KEY ([TenantId])    REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_Stock_Store]     FOREIGN KEY ([StoreId])     REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_Stock_Product]   FOREIGN KEY ([ProductId])   REFERENCES [dbo].[Products]([Id]),
    CONSTRAINT [FK_Stock_Warehouse] FOREIGN KEY ([WarehouseId]) REFERENCES [dbo].[Warehouses]([Id]),
    CONSTRAINT [CK_Stock_OnHand]   CHECK ([QtyOnHand] >= 0 OR [QtyOnHand] < 0),  -- يُفرض منطقياً بالكود حسب AllowNegative
    CONSTRAINT [CK_Stock_Reserved] CHECK ([ReservedQty] >= 0)
);
GO

-- تفرّد الرصيد لكل منتج/مستودع/دفعة داخل المستأجر
CREATE UNIQUE NONCLUSTERED INDEX [UX_Stock_Product_Warehouse_Batch]
    ON [dbo].[Stock] ([TenantId], [WarehouseId], [ProductId], [BatchNumber])
    WHERE [IsDeleted] = 0;
GO
-- فهرس "تحت حد الطلب" (تقارير النواقص)
CREATE NONCLUSTERED INDEX [IX_Stock_Reorder]
    ON [dbo].[Stock] ([TenantId], [WarehouseId])
    INCLUDE ([ProductId], [QtyOnHand], [ReorderLevel])
    WHERE [IsDeleted] = 0;
GO
CREATE NONCLUSTERED INDEX [IX_Stock_Product]
    ON [dbo].[Stock] ([TenantId], [ProductId]) WHERE [IsDeleted] = 0;
GO
```

### 8.2 StockMovements (دفتر الحركات — append-only)

```sql
CREATE TABLE [dbo].[StockMovements]
(
    [ProductId]        BIGINT        NOT NULL,
    [WarehouseId]      BIGINT        NOT NULL,
    [BatchNumber]      NVARCHAR(50)  NULL,
    [MovementType]     TINYINT       NOT NULL,   -- enum §7.1 (1..13)
    [Direction]        TINYINT       NOT NULL,   -- 1=In, 2=Out
    [Quantity]         DECIMAL(18,4) NOT NULL,   -- موجب دائماً
    [QuantityBefore]   DECIMAL(18,4) NOT NULL,
    [QuantityAfter]    DECIMAL(18,4) NOT NULL,
    [UnitCost]         DECIMAL(18,4) NOT NULL CONSTRAINT DF_StockMov_Cost DEFAULT (0),
    [ReferenceType]    VARCHAR(30)   NOT NULL,   -- 'PURCHASE_INVOICE','SALES_INVOICE','STOCK_TRANSFER',...
    [ReferenceId]      BIGINT        NOT NULL,   -- مفتاح المستند المصدر
    [ReferenceNumber]  VARCHAR(30)   NULL,       -- رقم المستند للعرض
    [MovementDate]     DATETIME2(3)  NOT NULL CONSTRAINT DF_StockMov_Date DEFAULT (SYSUTCDATETIME()),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_StockMov_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_StockMov_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_StockMovements] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_StockMov_Tenant]    FOREIGN KEY ([TenantId])    REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_StockMov_Store]     FOREIGN KEY ([StoreId])     REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_StockMov_Product]   FOREIGN KEY ([ProductId])   REFERENCES [dbo].[Products]([Id]),
    CONSTRAINT [FK_StockMov_Warehouse] FOREIGN KEY ([WarehouseId]) REFERENCES [dbo].[Warehouses]([Id]),
    CONSTRAINT [CK_StockMov_Type]      CHECK ([MovementType] BETWEEN 1 AND 13),
    CONSTRAINT [CK_StockMov_Direction] CHECK ([Direction] IN (1,2)),
    CONSTRAINT [CK_StockMov_Qty]       CHECK ([Quantity] > 0)
);
GO

-- الفهرس الأهم: دفتر منتج/مستودع زمنياً (للـ Ledger و re-build)
CREATE NONCLUSTERED INDEX [IX_StockMov_Product_Wh_Date]
    ON [dbo].[StockMovements] ([TenantId], [WarehouseId], [ProductId], [MovementDate] DESC)
    INCLUDE ([MovementType], [Direction], [Quantity], [QuantityAfter], [UnitCost])
    WHERE [IsDeleted] = 0;
GO
-- الرجوع من مستند مصدر إلى حركاته
CREATE NONCLUSTERED INDEX [IX_StockMov_Reference]
    ON [dbo].[StockMovements] ([TenantId], [ReferenceType], [ReferenceId])
    WHERE [IsDeleted] = 0;
GO
CREATE NONCLUSTERED INDEX [IX_StockMov_Tenant_Date]
    ON [dbo].[StockMovements] ([TenantId], [MovementDate] DESC) WHERE [IsDeleted] = 0;
GO
```

> **ملاحظة تصميمية:** `StockMovements` يُعامَل كـ **append-only**؛ لا تُحدَّث سجلاته ولا تُحذف فعلياً. أي تصحيح يتمّ بحركة عكسية جديدة، حفاظاً على سلامة الدفتر المحاسبي والتدقيق.

---

## 9) الـ API

| Method | Endpoint | الصلاحية | الوصف |
|--------|----------|----------|-------|
| GET | `/api/v1/inventory/stock` | `Inventory.View` | أرصدة (فلترة بمستودع/تصنيف/تحت الحد) |
| GET | `/api/v1/inventory/stock/{productId}` | `Inventory.View` | رصيد منتج عبر كل المستودعات |
| GET | `/api/v1/inventory/movements` | `Inventory.ViewMovements` | دفتر الحركات (paged) |
| GET | `/api/v1/inventory/movements/{productId}` | `Inventory.ViewMovements` | ledger منتج/مستودع |
| POST | `/api/v1/inventory/stock-count` | `Inventory.StockCount` | بدء جلسة جرد |
| POST | `/api/v1/inventory/stock-count/{id}/reconcile` | `Inventory.Reconcile` | توليد تسوية بالفروقات |
| POST | `/api/v1/inventory/rebuild/{productId}` | `Inventory.Reconcile` | إعادة بناء الرصيد من الحركات (إداري) |

**مثال — دفتر حركة منتج (Response):**
```json
GET /api/v1/inventory/movements/501?warehouseId=1
{
  "productId": 501,
  "warehouseId": 1,
  "currentQuantity": 110.0000,
  "averageCost": 1.2500,
  "movements": [
    { "date":"2026-07-13T08:20:00Z","type":"Sale_Out","direction":"Out",
      "quantity":10,"before":120,"after":110,"unitCost":1.2500,
      "reference":"INV-004120" },
    { "date":"2026-07-12T14:05:00Z","type":"Purchase_In","direction":"In",
      "quantity":100,"before":20,"after":120,"unitCost":1.2600,
      "reference":"PUR-000318" }
  ]
}
```

---

## 10) مخطط التدفّق (Flow Chart) — تحديث المخزون عند البيع (Sale)

```
        [ترحيل فاتورة بيع / إغلاق سلة POS]
                       │
                       ▼
        ┌──── BEGIN TRANSACTION (Unit of Work) ────┐
        │  لكل بند بيع:                              │
        │    SELECT Stock WITH (UPDLOCK, ROWLOCK)    │
        │         WHERE Product+Warehouse            │
        │              │                             │
        │              ▼                             │
        │   هل (AvailableQty - SaleQty) >= 0 ؟        │
        │        │NO (وAllowNeg=false)   │YES        │
        │        ▼                        ▼          │
        │  [ROLLBACK: INSUFFICIENT]  QtyBefore=Qty   │
        │                            Qty -= SaleQty  │
        │                            QtyAfter=Qty    │
        │                            UPDATE Stock     │
        │                            INSERT StockMovements
        │                              (Sale_Out,     │
        │                               UnitCost=WAC, │
        │                               ref=SalesInv) │
        │  تحديث LastMovementDate                     │
        └──────────── COMMIT / ROLLBACK ─────────────┘
                       │
                       ▼
     [SignalR: بثّ تحديث الرصيد]  →  [تنبيه إن Qty <= ReorderLevel]
                       │
                       ▼
              [تسجيل Audit + COGS]
```

---

## 11) ماذا يحدث عند: الحذف / التعديل / الإلغاء

- **حذف/إلغاء فاتورة بيع مرحّلة:** لا تُحذف حركاتها؛ بل تُنشَأ حركة عكسية `SaleReturn_In` (أو `Adjust_Increase` حسب السياسة) تعيد الكمية، فيبقى الدفتر متوازناً وقابلاً للتدقيق.
- **تعديل كمية فاتورة:** يُعامَل كإلغاء الحركة القديمة (عكسية) + حركة جديدة بالكمية المعدّلة — لا تعديل مباشر لسجل حركة سابق.
- **حذف منتج له رصيد:** مرفوض؛ يجب تصفير الرصيد أولاً (بيع/تحويل/تسوية).
- **تصحيح رصيد خاطئ:** عبر `StockAdjustment` معتمَد فقط (لا UPDATE يدوي على `Stock.QtyOnHand`).
- **إعادة البناء (Rebuild):** أداة إدارية تُعيد حساب `Stock.QtyOnHand` و`AverageCost` من تسلسل `StockMovements` بالكامل — تُستخدم للتحقق من السلامة أو بعد استعادة نسخة احتياطية.

---

## 12) سجل التدقيق (Audit Log)

- `StockMovements` **هو بذاته** الدفتر التدقيقي للمخزون (مَن عبر `CreatedBy`، متى عبر `MovementDate`، ماذا عبر `MovementType`، والمرجع عبر `ReferenceType/Id/Number`).
- تُسجَّل إضافةً عمليات الجرد وإعادة البناء في `AuditLogs` العام (`Action='StockCount'|'Reconcile'|'Rebuild'`).
- كل حركة مرتبطة بمستندها المصدر، فيُمكن التتبّع العكسي الكامل من الرصيد ← الحركة ← الفاتورة ← المستخدم.

---

## 13) الأخطاء المحتملة (Possible Errors)

| الرمز | HTTP | السبب |
|-------|------|-------|
| `INSUFFICIENT_STOCK` | 409 | خصم يتجاوز المتاح و`AllowNegativeStock=false` |
| `STOCK_ROW_LOCKED` | 409 | تعذّر الحصول على قفل الصف (تزامن عالٍ) — يُعاد المحاولة |
| `PRODUCT_HAS_STOCK` | 409 | حذف منتج/مستودع برصيد > 0 |
| `INVALID_MOVEMENT_TYPE` | 400 | نوع حركة خارج 1..13 |
| `RESERVE_EXCEEDS_QTY` | 409 | حجز يتجاوز المتاح |
| `REBUILD_MISMATCH` | 500 | فرق بين مجموع الحركات والرصيد المخزّن (سلامة) |
| `CONCURRENCY_CONFLICT` | 409 | تعارض `ConcurrencyStamp` عند تحديث الرصيد |

---

## 14) الأداء (Performance)

- **قفل ضيّق:** `SELECT ... WITH (UPDLOCK, ROWLOCK)` على صف `Stock` المستهدف فقط، داخل معاملة قصيرة تحت `READ COMMITTED SNAPSHOT` (RCSI) لتقليل التنازع.
- **فهرس Ledger مغطٍّ (covering)** `(TenantId, WarehouseId, ProductId, MovementDate DESC)` مع `INCLUDE` يجعل عرض دفتر الحركة استعلاماً بلا Key Lookups.
- **الأرشفة:** `StockMovements` ينمو بسرعة؛ يُقسَّم (Partitioning) بالسنة/الشهر على `MovementDate`، وتُرحَّل الأشهر القديمة لجداول أرشيف.
- **صفحات الأرصدة** تُخدَّم من `Stock` مباشرةً (رقم واحد لكل منتج) دون تجميع من الحركات — أداء O(1) لكل صف.
- **بثّ SignalR** للأرصدة يُجمَّع (debounce) لتفادي إغراق الواجهة في ذروة POS.
- تقارير النواقص تعتمد الفهرس المُرشَّح `IX_Stock_Reorder` (`Quantity <= ReorderLevel`).

---

## 15) الأمان (Security)

- **لا مسار يدوي** لتعديل `Stock.QtyOnHand`؛ كل تغيير يمرّ عبر مستند مصدر مدقَّق — يمنع التلاعب بالمخزون.
- `StockMovements` **غير قابل للتعديل/الحذف** (append-only) على مستوى منطق الأعمال؛ التصحيح بحركة عكسية فقط — سلامة الدفتر المحاسبي.
- عرض **التكلفة (WAC/COGS)** محميّ بصلاحية `Inventory.ViewCost` منفصلة عن عرض الكمية.
- عزل تام بـ `TenantId` + Global Query Filter على كلا الجدولين؛ التحقق من انتماء `Product`/`Warehouse` لنفس المستأجر قبل أي حركة (منع تسرّب/حقن عبر معرّفات مستأجر آخر).
- عمليات «إعادة البناء» و«التسوية» حسّاسة وتتطلّب صلاحيات إدارية مرتفعة وتُسجَّل بالكامل في `AuditLogs`.
