# 14 — Purchase Invoices (فواتير الشراء والاستلام)

> يوثّق هذه الوثيقة دورة الشراء الكاملة: **أمر الشراء (Purchase Order)** → **استلام البضاعة (Receive Inventory)** → **فاتورة المورد (Supplier Invoice)**، مع دعم **الاستلام الجزئي (Partial Receiving)** وأثره على المخزون والتكلفة ورصيد المورد. يلتزم هذا الملف حرفياً بالمعايير الحاكمة في [04-Database-Design.md](04-Database-Design.md).

---

## 1) الهدف (Purpose)

- تسجيل التزام الشراء تجاه المورد قبل وصول البضاعة (Purchase Order — التزام غير محاسبي).
- استلام البضاعة كلياً أو جزئياً، ورفع رصيد المخزون **ذرّياً** ضمن معاملة واحدة.
- إنشاء فاتورة الشراء (Supplier Invoice) التي تحرّك **رصيد المورد الدائن (Accounts Payable)**.
- حساب **تكلفة الشراء المرجّحة (Weighted Average Cost)** أو **FIFO** حسب إعداد المستأجر.
- الربط الكامل بالمورد وبأمر الشراء الأصلي لأغراض التتبّع والتدقيق والمرتجعات.

> **قاعدة الفصل المحاسبي:** أمر الشراء (PO) لا يحرّك مخزوناً ولا رصيد مورد. **الاستلام** يحرّك المخزون. **الفاتورة** تحرّك رصيد المورد. قد يتزامن الاستلام والفاتورة في عملية واحدة (Receive & Bill) حسب الإعداد.

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية (Permission Claim) | الوصف |
|-----------------------------|-------|
| `Purchasing.PurchaseOrder.View` | عرض أوامر الشراء |
| `Purchasing.PurchaseOrder.Create` | إنشاء أمر شراء |
| `Purchasing.PurchaseOrder.Edit` | تعديل أمر شراء بحالة `Draft` |
| `Purchasing.PurchaseOrder.Approve` | اعتماد أمر الشراء |
| `Purchasing.Receiving.Create` | استلام بضاعة (يرفع المخزون) |
| `Purchasing.PurchaseInvoice.Create` | إنشاء فاتورة مورد |
| `Purchasing.PurchaseInvoice.Delete` | حذف (Soft Delete) فاتورة |
| `Purchasing.Cost.Override` | تعديل تكلفة الوحدة يدوياً |

- التحقق على مستويين: **API** عبر `[Authorize(Policy = "...")]`، و**UI** بإخفاء الأزرار غير المصرّح بها.
- كل عملية مقيّدة بـ `TenantId` و`StoreId` الحاليين عبر الـ Global Query Filter.

---

## 3) تصميم الصفحة (Page Layout)

```
┌──────────────────────────────────────────────────────────────────────┐
│  فواتير الشراء            [+ أمر شراء] [+ استلام] [+ فاتورة مورد]        │
├──────────────────────────────────────────────────────────────────────┤
│  فلاتر: [المورد ▼] [الحالة ▼] [الفرع ▼] [من تاريخ] [إلى تاريخ] [بحث🔍]  │
├──────────────────────────────────────────────────────────────────────┤
│  # الرقم │ المورد │ التاريخ │ الحالة │ الإجمالي │ المدفوع │ المتبقي │ ⚙  │
│  PO-000012 مؤسسة النور 2026-07-01 مُستلَم جزئياً 5,400  0     5,400  ⋮  │
│  ...                                                                    │
├──────────────────────────────────────────────────────────────────────┤
│  الصفحات:  ◄ 1 2 3 ►      إجمالي السجلات: 128                          │
└──────────────────────────────────────────────────────────────────────┘
```

**نموذج الإدخال (Purchase Invoice Form):** رأس الفاتورة (المورد، الفرع، تاريخ الفاتورة، رقم فاتورة المورد الخارجي، أمر الشراء المرتبط) + شبكة بنود (منتج، كمية مطلوبة، كمية مستلمة، تكلفة الوحدة، خصم البند، ضريبة البند، إجمالي البند) + ملخّص (الإجمالي الفرعي، الخصم، الضريبة، الشحن، الإجمالي النهائي).

---

## 4) الأزرار (Buttons)

| الزر | الإجراء | الصلاحية |
|------|---------|----------|
| **+ أمر شراء** | فتح نموذج PO جديد بحالة `Draft` | `PurchaseOrder.Create` |
| **اعتماد** | تحويل PO من `Draft` إلى `Approved` | `PurchaseOrder.Approve` |
| **استلام** | فتح شاشة الاستلام لأمر معتمد (كلي/جزئي) | `Receiving.Create` |
| **+ فاتورة مورد** | إنشاء فاتورة من استلام أو مباشرة | `PurchaseInvoice.Create` |
| **حفظ كمسودّة** | حفظ دون ترحيل (لا يحرّك مخزون) | `PurchaseInvoice.Create` |
| **ترحيل (Post)** | تثبيت الفاتورة → تحريك المورد | `PurchaseInvoice.Create` |
| **طباعة** | طباعة PDF | `PurchaseInvoice.View` |
| **حذف** | Soft Delete (يُعكَس أثر المخزون) | `PurchaseInvoice.Delete` |

---

## 5) الحقول (Fields)

### رأس الفاتورة
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|--------|---------|
| `SupplierId` | مرجع | ✔ | من قائمة الموردين النشطين |
| `PurchaseOrderId` | مرجع | ✘ | الربط بأمر الشراء (اختياري للشراء المباشر) |
| `StoreId` / `WarehouseId` | مرجع | ✔ | وجهة الاستلام |
| `InvoiceDate` | تاريخ | ✔ | تاريخ فاتورة المورد |
| `SupplierInvoiceNo` | نص | ✘ | رقم الفاتورة الخارجي لدى المورد |
| `PaymentTerms` | قائمة | ✘ | نقدي / آجل (30/60/90 يوم) |

### بنود الفاتورة
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|--------|---------|
| `ProductId` | مرجع | ✔ | المنتج |
| `OrderedQty` | عدد | ✔ | الكمية المطلوبة (من PO) |
| `ReceivedQty` | عدد | ✔ | الكمية المستلمة فعلياً (≤ المطلوبة) |
| `UnitCost` | مبلغ | ✔ | تكلفة الوحدة قبل الضريبة |
| `DiscountAmount` | مبلغ | ✘ | خصم على مستوى البند |
| `TaxRate` | نسبة | ✘ | نسبة الضريبة (%) |
| `LineTotal` | مبلغ | محسوب | `(ReceivedQty × UnitCost) − Discount + Tax` |

---

## 6) التحقق (Validation)

تُطبَّق عبر **FluentValidation** على مستوى الـ Command (Server-side) وتُكرَّر Client-side.

- `SupplierId` موجود ونشط وينتمي لنفس `TenantId`.
- كل بند: `OrderedQty > 0`، `0 ≤ ReceivedQty ≤ OrderedQty`، `UnitCost ≥ 0`.
- `DiscountAmount` لكل بند لا يتجاوز `ReceivedQty × UnitCost`.
- لا يمكن ترحيل فاتورة بلا بنود، ولا بنود بكمية مستلمة = 0 جميعاً.
- عند الربط بـ PO: مجموع الكميات المستلمة تراكمياً عبر كل الاستلامات ≤ الكمية المطلوبة في PO (منع الاستلام الزائد ما لم يُفعَّل `AllowOverReceiving`).
- `SupplierInvoiceNo` فريد لكل مورد داخل المستأجر (منع التكرار).

---

## 7) قواعد العمل (Business Rules)

1. **PO لا يؤثّر على المخزون** — هو التزام فقط. حالته: `Draft → Approved → PartiallyReceived → Received → Closed / Cancelled`.
2. **الاستلام يرفع المخزون** ذرّياً ويُنشئ حركة `StockMovement` بنوع `PurchaseReceipt` (مرجع [13-Inventory.md](13-Inventory.md)).
3. **الاستلام الجزئي:** يُسمح باستلام دفعات متعددة لنفس الـ PO. تُخزَّن `ReceivedQty` تراكمياً على بند الـ PO (`PurchaseOrderItems.ReceivedQty`)، وتتغيّر حالة الـ PO تلقائياً إلى `PartiallyReceived` ثم `Received` عند اكتمال كل البنود.
4. **تحديث التكلفة (Weighted Average):** عند كل استلام يُعاد حساب متوسط التكلفة:

   ```
   NewAvgCost = ( (OldQtyOnHand × OldAvgCost) + (ReceivedQty × UnitCost) )
                / (OldQtyOnHand + ReceivedQty)
   ```

5. **الفاتورة تحرّك رصيد المورد** (`Suppliers.Balance += InvoiceTotal`) — يُسجَّل كـ Accounts Payable.
6. كل ترحيل يجري داخل **Transaction واحد** (Unit of Work): بنود + مخزون + تكلفة + رصيد مورد + تسلسل الرقم. أي فشل → Rollback كامل.
7. رقم الفاتورة يُولَّد من جدول `Sequences` بـ `DocType='PURCHASE_INVOICE'` (مرجع البند 10 في 04).

---

## 8) جداول قاعدة البيانات (Database Tables — SQL كامل)

> جميع الجداول ترث **الأعمدة المشتركة الإجبارية** وقيود `PK/FK/Index` وفق [04-Database-Design.md](04-Database-Design.md).

### 8.1 PurchaseOrders

```sql
CREATE TABLE [dbo].[PurchaseOrders]
(
    [SupplierId]      BIGINT        NOT NULL,
    [WarehouseId]     BIGINT        NULL,
    [OrderNumber]     VARCHAR(30)   NOT NULL,   -- من Sequences: 'PO-000012'
    [OrderDate]       DATETIME2(3)  NOT NULL,
    [ExpectedDate]    DATETIME2(3)  NULL,
    [Status]          TINYINT       NOT NULL CONSTRAINT DF_PurchaseOrders_Status DEFAULT (0),
        -- 0=Draft,1=Approved,2=PartiallyReceived,3=Received,4=Closed,5=Cancelled
    [SubTotal]        DECIMAL(18,4) NOT NULL CONSTRAINT DF_PurchaseOrders_SubTotal DEFAULT (0),
    [DiscountAmount]  DECIMAL(18,4) NOT NULL CONSTRAINT DF_PurchaseOrders_Discount DEFAULT (0),
    [TaxAmount]       DECIMAL(18,4) NOT NULL CONSTRAINT DF_PurchaseOrders_Tax DEFAULT (0),
    [ShippingAmount]  DECIMAL(18,4) NOT NULL CONSTRAINT DF_PurchaseOrders_Shipping DEFAULT (0),
    [GrandTotal]      DECIMAL(18,4) NOT NULL CONSTRAINT DF_PurchaseOrders_Grand DEFAULT (0),
    [Notes]           NVARCHAR(500) NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_PurchaseOrders_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_PurchaseOrders_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PurchaseOrders_Tenant]    FOREIGN KEY ([TenantId])   REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_PurchaseOrders_Store]     FOREIGN KEY ([StoreId])    REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_PurchaseOrders_Supplier]  FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Suppliers]([Id])  ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseOrders_Warehouse] FOREIGN KEY ([WarehouseId])REFERENCES [dbo].[Warehouses]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [CK_PurchaseOrders_Totals] CHECK ([GrandTotal] >= 0)
);
GO
CREATE NONCLUSTERED INDEX [IX_PurchaseOrders_Tenant_Store] ON [dbo].[PurchaseOrders]([TenantId],[StoreId]) WHERE [IsDeleted]=0;
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_PurchaseOrders_Number] ON [dbo].[PurchaseOrders]([TenantId],[OrderNumber]) WHERE [IsDeleted]=0;
GO
CREATE NONCLUSTERED INDEX [IX_PurchaseOrders_Supplier] ON [dbo].[PurchaseOrders]([TenantId],[SupplierId],[Status]) WHERE [IsDeleted]=0;
GO
```

### 8.2 PurchaseOrderItems

```sql
CREATE TABLE [dbo].[PurchaseOrderItems]
(
    [PurchaseOrderId] BIGINT        NOT NULL,
    [ProductId]       BIGINT        NOT NULL,
    [OrderedQty]      DECIMAL(18,4) NOT NULL,
    [ReceivedQty]     DECIMAL(18,4) NOT NULL CONSTRAINT DF_POItems_ReceivedQty DEFAULT (0), -- تراكمي
    [UnitCost]        DECIMAL(18,4) NOT NULL,
    [DiscountAmount]  DECIMAL(18,4) NOT NULL CONSTRAINT DF_POItems_Discount DEFAULT (0),
    [TaxRate]         DECIMAL(9,4)  NOT NULL CONSTRAINT DF_POItems_TaxRate DEFAULT (0),
    [LineTotal]       DECIMAL(18,4) NOT NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_POItems_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_POItems_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_PurchaseOrderItems] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_POItems_Tenant]  FOREIGN KEY ([TenantId])        REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_POItems_Order]   FOREIGN KEY ([PurchaseOrderId]) REFERENCES [dbo].[PurchaseOrders]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_POItems_Product] FOREIGN KEY ([ProductId])       REFERENCES [dbo].[Products]([Id])       ON DELETE NO ACTION,
    CONSTRAINT [CK_POItems_Qty] CHECK ([OrderedQty] > 0 AND [ReceivedQty] >= 0 AND [ReceivedQty] <= [OrderedQty])
);
GO
CREATE NONCLUSTERED INDEX [IX_POItems_Order]   ON [dbo].[PurchaseOrderItems]([PurchaseOrderId]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_POItems_Product] ON [dbo].[PurchaseOrderItems]([TenantId],[ProductId]) WHERE [IsDeleted]=0;
GO
```

### 8.3 PurchaseInvoices

```sql
CREATE TABLE [dbo].[PurchaseInvoices]
(
    [SupplierId]        BIGINT        NOT NULL,
    [PurchaseOrderId]   BIGINT        NULL,      -- الربط بأمر الشراء (اختياري)
    [WarehouseId]       BIGINT        NULL,
    [InvoiceNumber]     VARCHAR(30)   NOT NULL,  -- من Sequences: 'PUR-000045'
    [SupplierInvoiceNo] NVARCHAR(50)  NULL,      -- رقم فاتورة المورد الخارجي
    [InvoiceDate]       DATETIME2(3)  NOT NULL,
    [Status]            TINYINT       NOT NULL CONSTRAINT DF_PurchaseInvoices_Status DEFAULT (0),
        -- 0=Draft,1=Posted,2=PartiallyPaid,3=Paid,4=Cancelled
    [SubTotal]          DECIMAL(18,4) NOT NULL CONSTRAINT DF_PI_SubTotal DEFAULT (0),
    [DiscountAmount]    DECIMAL(18,4) NOT NULL CONSTRAINT DF_PI_Discount DEFAULT (0),
    [TaxAmount]         DECIMAL(18,4) NOT NULL CONSTRAINT DF_PI_Tax DEFAULT (0),
    [ShippingAmount]    DECIMAL(18,4) NOT NULL CONSTRAINT DF_PI_Shipping DEFAULT (0),
    [GrandTotal]        DECIMAL(18,4) NOT NULL CONSTRAINT DF_PI_Grand DEFAULT (0),
    [PaidAmount]        DECIMAL(18,4) NOT NULL CONSTRAINT DF_PI_Paid DEFAULT (0),
    [Notes]             NVARCHAR(500) NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_PurchaseInvoices_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_PurchaseInvoices_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_PurchaseInvoices] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PI_Tenant]    FOREIGN KEY ([TenantId])        REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_PI_Store]     FOREIGN KEY ([StoreId])         REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_PI_Supplier]  FOREIGN KEY ([SupplierId])      REFERENCES [dbo].[Suppliers]([Id])      ON DELETE NO ACTION,
    CONSTRAINT [FK_PI_Order]     FOREIGN KEY ([PurchaseOrderId]) REFERENCES [dbo].[PurchaseOrders]([Id])  ON DELETE NO ACTION,
    CONSTRAINT [FK_PI_Warehouse] FOREIGN KEY ([WarehouseId])     REFERENCES [dbo].[Warehouses]([Id])      ON DELETE NO ACTION,
    CONSTRAINT [CK_PI_Paid] CHECK ([PaidAmount] >= 0 AND [PaidAmount] <= [GrandTotal])
);
GO
CREATE NONCLUSTERED INDEX [IX_PI_Tenant_Store] ON [dbo].[PurchaseInvoices]([TenantId],[StoreId]) WHERE [IsDeleted]=0;
CREATE UNIQUE NONCLUSTERED INDEX [UX_PI_Number] ON [dbo].[PurchaseInvoices]([TenantId],[InvoiceNumber]) WHERE [IsDeleted]=0;
CREATE UNIQUE NONCLUSTERED INDEX [UX_PI_SupplierInvoiceNo] ON [dbo].[PurchaseInvoices]([TenantId],[SupplierId],[SupplierInvoiceNo]) WHERE [IsDeleted]=0 AND [SupplierInvoiceNo] IS NOT NULL;
CREATE NONCLUSTERED INDEX [IX_PI_Supplier_Status] ON [dbo].[PurchaseInvoices]([TenantId],[SupplierId],[Status]) WHERE [IsDeleted]=0;
GO
```

### 8.4 PurchaseInvoiceItems

```sql
CREATE TABLE [dbo].[PurchaseInvoiceItems]
(
    [PurchaseInvoiceId]   BIGINT        NOT NULL,
    [PurchaseOrderItemId] BIGINT        NULL,      -- ربط ببند أمر الشراء (للاستلام الجزئي)
    [ProductId]           BIGINT        NOT NULL,
    [ReceivedQty]         DECIMAL(18,4) NOT NULL,  -- الكمية المستلمة في هذه الفاتورة
    [UnitCost]            DECIMAL(18,4) NOT NULL,
    [DiscountAmount]      DECIMAL(18,4) NOT NULL CONSTRAINT DF_PII_Discount DEFAULT (0),
    [TaxRate]             DECIMAL(9,4)  NOT NULL CONSTRAINT DF_PII_TaxRate DEFAULT (0),
    [TaxAmount]           DECIMAL(18,4) NOT NULL CONSTRAINT DF_PII_TaxAmt DEFAULT (0),
    [LineTotal]           DECIMAL(18,4) NOT NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_PII_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_PII_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_PurchaseInvoiceItems] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PII_Tenant]   FOREIGN KEY ([TenantId])            REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_PII_Invoice]  FOREIGN KEY ([PurchaseInvoiceId])   REFERENCES [dbo].[PurchaseInvoices]([Id])   ON DELETE NO ACTION,
    CONSTRAINT [FK_PII_POItem]   FOREIGN KEY ([PurchaseOrderItemId]) REFERENCES [dbo].[PurchaseOrderItems]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PII_Product]  FOREIGN KEY ([ProductId])           REFERENCES [dbo].[Products]([Id])           ON DELETE NO ACTION,
    CONSTRAINT [CK_PII_Qty] CHECK ([ReceivedQty] > 0 AND [UnitCost] >= 0)
);
GO
CREATE NONCLUSTERED INDEX [IX_PII_Invoice] ON [dbo].[PurchaseInvoiceItems]([PurchaseInvoiceId]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_PII_Product] ON [dbo].[PurchaseInvoiceItems]([TenantId],[ProductId]) WHERE [IsDeleted]=0;
GO
```

---

## 9) الـ API

> Base: `/api/v1/purchasing`. كل الطلبات تحمل `Authorization: Bearer <JWT>` و`X-Tenant-Id` مُستنتَج من التوكن.

| Method | Endpoint | الوصف |
|--------|----------|-------|
| `POST` | `/purchase-orders` | إنشاء أمر شراء |
| `POST` | `/purchase-orders/{id}/approve` | اعتماد PO |
| `POST` | `/purchase-orders/{id}/receive` | استلام (كلي/جزئي) → يرفع المخزون |
| `POST` | `/purchase-invoices` | إنشاء فاتورة مورد |
| `POST` | `/purchase-invoices/{id}/post` | ترحيل الفاتورة → تحريك المورد |
| `GET`  | `/purchase-invoices/{id}` | جلب فاتورة |
| `DELETE` | `/purchase-invoices/{id}` | حذف Soft |

**مثال — استلام جزئي (Request):**

```json
POST /api/v1/purchasing/purchase-orders/12/receive
{
  "warehouseId": 3,
  "lines": [
    { "purchaseOrderItemId": 101, "receivedQty": 40, "unitCost": 12.5000 },
    { "purchaseOrderItemId": 102, "receivedQty": 10, "unitCost": 8.0000 }
  ]
}
```

**Response (201):**

```json
{
  "purchaseInvoiceId": 45,
  "invoiceNumber": "PUR-000045",
  "poStatus": "PartiallyReceived",
  "stockMovementsCreated": 2,
  "grandTotal": 580.0000
}
```

---

## 10) مخطط التدفّق (Flow Chart)

```
        ┌──────────────┐
        │ إنشاء PO      │ (Draft) — لا أثر مخزوني
        └──────┬───────┘
               ▼
        ┌──────────────┐
        │ اعتماد PO     │ (Approved)
        └──────┬───────┘
               ▼
   ┌───────────────────────┐
   │ استلام (كلي/جزئي)      │──► BEGIN TRAN
   └───────────┬───────────┘
               ▼
   [Stock += ReceivedQty] ── [إعادة حساب AvgCost] ── [StockMovement: PurchaseReceipt]
               ▼
   [PO.ReceivedQty تراكمي] ── [تحديث حالة PO]
               ▼
   ┌───────────────────────┐
   │ إنشاء/ترحيل الفاتورة   │──► [Supplier.Balance += Total] ── [Sequences++]
   └───────────┬───────────┘
               ▼
          COMMIT TRAN  (أي فشل ⇒ ROLLBACK كامل)
```

---

## 11) ماذا يحدث عند: الحذف / التعديل / تغيير السعر

- **الحذف (Soft Delete):** يُمنع حذف فاتورة **مُرحَّلة ومدفوعة جزئياً/كلياً**. حذف فاتورة مرحّلة غير مدفوعة يستوجب **معاملة عكسية**: خصم الكمية من المخزون، إعادة حساب متوسط التكلفة، خفض `Supplier.Balance`، وإنشاء `StockMovement` عكسي بنوع `PurchaseReceiptReversal`. لا يُحذف السجل فعلياً (`IsDeleted=1`).
- **التعديل:** فاتورة `Draft` قابلة للتعديل الحر. فاتورة `Posted` **لا تُعدَّل**؛ يُنشأ بدلاً منها **إشعار دائن/مرتجع شراء** (مرجع [17-Purchase-Returns.md](17-Purchase-Returns.md)) للحفاظ على السلامة المحاسبية.
- **تغيير تكلفة المنتج لاحقاً:** لا يؤثّر على فواتير سابقة — `UnitCost` مُخزَّن كـ **snapshot** على البند. متوسط التكلفة الحالي يُعاد حسابه فقط عند استلامات **جديدة**.

---

## 12) سجل التدقيق (Audit Log)

كل عملية تُقيَّد في `AuditLogs` مع: `TenantId`, `UserId`, `EntityName='PurchaseInvoice'`, `EntityId`, `Action` (Created/Posted/Received/Deleted), `OldValues`/`NewValues` (JSON diff), `IpAddress`, `Timestamp (UTC)`. حركات المخزون تُقيَّد إضافياً في `StockMovements` كـ ledger غير قابل للتعديل.

---

## 13) الأخطاء المحتملة (Possible Errors)

| الكود | الحالة | الرسالة |
|-------|--------|---------|
| `PUR-4001` | 400 | كمية الاستلام تتجاوز الكمية المطلوبة |
| `PUR-4002` | 409 | رقم فاتورة المورد مكرّر لنفس المورد |
| `PUR-4003` | 409 | تعارض تزامن (ConcurrencyStamp) — أُعيد تحميل السجل |
| `PUR-4004` | 422 | محاولة حذف فاتورة مدفوعة |
| `PUR-4005` | 404 | أمر الشراء غير موجود أو محذوف |

---

## 14) الأداء (Performance)

- الاستلام الجزئي يُنفَّذ بـ **Set-based UPDATE** على `Stock` (لا صف-صف) لتقليل زمن القفل.
- تفعيل **RCSI** لتقليل الأقفال على `Stock` أثناء الاستلامات المتزامنة.
- فهرس `IX_PI_Supplier_Status` يخدم شاشة كشف حساب المورد.
- قراءة/زيادة `Sequences` عبر `UPDATE ... OUTPUT INSERTED.NextValue` (قفل صف واحد فقط).

---

## 15) الأمان (Security)

- **IDOR:** كل استعلام يبدأ بـ `TenantId` عبر Global Query Filter؛ لا يُقبل معرّف من خارج المستأجر.
- **Least Privilege:** `Cost.Override` صلاحية منفصلة عن الإنشاء.
- **Immutability:** الفواتير المرحّلة غير قابلة للتعديل (تصحيح عبر مرتجع فقط).
- **Concurrency:** `ROWVERSION` يمنع الكتابة فوق تحديث متزامن للمخزون.
- كل الإجراءات مُدقَّقة (Audit) وقابلة للتتبّع للمستخدم و IP.
