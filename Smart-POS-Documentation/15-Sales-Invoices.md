# 15 — Sales Invoices (فواتير البيع)

> يوثّق هذا الملف **فواتير البيع خارج نقطة البيع (POS)** — البيع بالجملة/الآجل/العملاء المسجّلين: بنودها، الخصم، الضريبة، الربط بالعميل، والدفع. أثر البيع على المخزون يجري **ذرّياً ضمن معاملة واحدة**. حالات الفاتورة: `Draft / Confirmed / Paid`. يلتزم بالمعايير الحاكمة في [04-Database-Design.md](04-Database-Design.md).

---

## 1) الهدف (Purpose)

- إصدار فاتورة بيع لعميل مسجّل مع دعم البيع النقدي والآجل.
- حساب **الإجماليات والضريبة والخصم** على مستوى البند والفاتورة بدقّة `DECIMAL(18,4)`.
- خصم الكميات من المخزون وتسجيل **تكلفة البضاعة المباعة (COGS)** لكل بند كـ snapshot.
- تحريك **رصيد العميل المدين (Accounts Receivable)** وتسجيل الدفعات.
- إدارة دورة حياة الفاتورة: `Draft → Confirmed → Paid` مع منع التلاعب بعد التأكيد.

> **الفرق عن POS ([18-POS.md](18-POS.md)):** فاتورة البيع هنا كيان back-office يدعم المسودّات، الدفع الجزئي الآجل، وربط العميل الائتماني. فاتورة POS سريعة، نقدية غالباً، ومرتبطة بشفت الكاشير.

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية | الوصف |
|----------|-------|
| `Sales.Invoice.View` | عرض فواتير البيع |
| `Sales.Invoice.Create` | إنشاء فاتورة (مسودّة) |
| `Sales.Invoice.Confirm` | تأكيد الفاتورة (يخصم المخزون) |
| `Sales.Invoice.Delete` | حذف Soft (بشروط) |
| `Sales.Discount.Apply` | تطبيق خصم يدوي |
| `Sales.Discount.OverLimit` | تجاوز حدّ الخصم المسموح |
| `Sales.Payment.Receive` | تسجيل دفعة على الفاتورة |
| `Sales.Invoice.SellBelowCost` | البيع بأقل من التكلفة |

- التحقق مزدوج (API Policy + إخفاء أزرار UI)، ومقيّد بـ `TenantId/StoreId`.

---

## 3) تصميم الصفحة (Page Layout)

```
┌──────────────────────────────────────────────────────────────────────┐
│  فواتير البيع                                   [+ فاتورة بيع جديدة]     │
├──────────────────────────────────────────────────────────────────────┤
│  فلاتر: [العميل ▼] [الحالة ▼] [الفرع ▼] [من] [إلى] [بحث🔍]             │
├──────────────────────────────────────────────────────────────────────┤
│  # الرقم │ العميل │ التاريخ │ الحالة │ الإجمالي │ المدفوع │ المتبقي │ ⚙  │
│  INV-000231 شركة الأمل 2026-07-05 مؤكدة 1,265.00 500.00 765.00  ⋮     │
├──────────────────────────────────────────────────────────────────────┤
│                             ملخّص أسفل الشبكة: إجمالي المبيعات / المتبقي │
└──────────────────────────────────────────────────────────────────────┘
```

**نموذج الفاتورة:** رأس (العميل، الفرع/المستودع، تاريخ، شروط الدفع) + شبكة بنود (منتج، كمية، سعر البيع، خصم البند %، ضريبة البند %، إجمالي البند) + ملخّص (Subtotal، خصم إجمالي، ضريبة، الإجمالي النهائي، المدفوع، المتبقي) + لوحة الدفع.

---

## 4) الأزرار (Buttons)

| الزر | الإجراء | الصلاحية |
|------|---------|----------|
| **+ فاتورة جديدة** | نموذج فارغ بحالة `Draft` | `Invoice.Create` |
| **حفظ كمسودّة** | حفظ دون خصم مخزون | `Invoice.Create` |
| **تأكيد (Confirm)** | خصم المخزون + تحريك رصيد العميل | `Invoice.Confirm` |
| **استلام دفعة** | تسجيل Payment | `Payment.Receive` |
| **طباعة / PDF** | إخراج الفاتورة | `Invoice.View` |
| **حذف** | Soft Delete (بشروط) | `Invoice.Delete` |
| **تحويل لمرتجع** | فتح مرتجع مرتبط | `Sales.Return.Create` |

---

## 5) الحقول (Fields)

### رأس الفاتورة
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|--------|---------|
| `CustomerId` | مرجع | ✔ | العميل (قد يكون "عميل نقدي" افتراضي) |
| `WarehouseId` | مرجع | ✔ | مصدر الخصم المخزوني |
| `InvoiceDate` | تاريخ | ✔ | تاريخ الإصدار |
| `PaymentType` | قائمة | ✔ | Cash / Credit / Card / Mixed |
| `DueDate` | تاريخ | ✘ | للبيع الآجل |

### بنود الفاتورة
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|--------|---------|
| `ProductId` | مرجع | ✔ | المنتج |
| `Quantity` | عدد | ✔ | > 0 وضمن المتاح |
| `UnitPrice` | مبلغ | ✔ | سعر البيع (snapshot) |
| `UnitCost` | مبلغ | محسوب | التكلفة لحظة البيع (COGS snapshot) |
| `DiscountPercent` | نسبة | ✘ | خصم البند |
| `TaxRate` | نسبة | ✘ | نسبة الضريبة |
| `LineTotal` | مبلغ | محسوب | انظر معادلة البند |

---

## 6) التحقق (Validation)

- `CustomerId` موجود ونشط داخل المستأجر؛ للبيع الآجل يجب ألا يتجاوز الإجمالي **حدّ الائتمان** (`Customer.CreditLimit − OutstandingBalance`).
- كل بند: `Quantity > 0`، `UnitPrice ≥ 0`، `0 ≤ DiscountPercent ≤ 100`.
- عند التأكيد: `Quantity ≤ QtyOnHand` للمستودع (ما لم يُفعَّل `AllowNegativeStock`).
- إجمالي خصم البنود لا يتجاوز حدّ الخصم المسموح للمستخدم إلا بصلاحية `Discount.OverLimit`.
- البيع بأقل من التكلفة يتطلّب `Invoice.SellBelowCost`، وإلا يُرفض.
- لا يُمكن تأكيد فاتورة بلا بنود.

---

## 7) قواعد العمل (Business Rules)

### معادلات الحساب (بالترتيب)

```
LineNet       = Quantity × UnitPrice
LineDiscount  = LineNet × (DiscountPercent / 100)
LineTaxable   = LineNet − LineDiscount
LineTax       = LineTaxable × (TaxRate / 100)
LineTotal     = LineTaxable + LineTax

SubTotal      = Σ LineNet
TotalDiscount = Σ LineDiscount + InvoiceLevelDiscount
TaxAmount     = Σ LineTax
GrandTotal    = SubTotal − TotalDiscount + TaxAmount
RemainingAmount = GrandTotal − PaidAmount
```

### قواعد الحالات
1. **Draft:** لا أثر مخزوني/محاسبي — قابلة للتعديل والحذف الحر.
2. **Confirmed:** يُخصم المخزون، يُسجَّل COGS، يرتفع `Customer.Balance` بالمتبقي — غير قابلة للتعديل.
3. **Paid:** عند اكتمال الدفعات (`PaidAmount = GrandTotal`) تتحوّل تلقائياً.
4. **أثر البيع على المخزون ضمن معاملة واحدة:** خصم `Stock`، إنشاء `StockMovement` بنوع `SaleIssue`، تسجيل `Payment` (إن وُجد)، زيادة `Sequences` — كلها في **Transaction ذرّي**؛ أي فشل ⇒ Rollback.
5. رقم الفاتورة من `Sequences` بـ `DocType='SALES_INVOICE'` (البند 10 في 04).

---

## 8) جداول قاعدة البيانات (Database Tables — SQL كامل)

### 8.1 SalesInvoices

```sql
CREATE TABLE [dbo].[SalesInvoices]
(
    [CustomerId]      BIGINT        NOT NULL,
    [WarehouseId]     BIGINT        NULL,
    [InvoiceNumber]   VARCHAR(30)   NOT NULL,   -- من Sequences: 'INV-000231'
    [InvoiceType]     VARCHAR(20)   NOT NULL CONSTRAINT DF_SI_InvoiceType DEFAULT ('POS'),
        -- نوع المستند: 'POS' | 'Web' | 'Wholesale' | 'Return' | 'Quotation'
        -- يحدّد سلوك الفاتورة (POS تفريغ فوري، Quotation لا تمسّ المخزون، Wholesale سعر جملة...)
    [InvoiceDate]     DATETIME2(3)  NOT NULL,
    [DueDate]         DATETIME2(3)  NULL,
    [PaymentType]     TINYINT       NOT NULL CONSTRAINT DF_SI_PaymentType DEFAULT (0),
        -- 0=Cash,1=Credit,2=Card,3=Mixed
    [Status]          TINYINT       NOT NULL CONSTRAINT DF_SI_Status DEFAULT (0),
        -- 0=Draft,1=Confirmed,2=PartiallyPaid,3=Paid,4=Cancelled
    [SubTotal]        DECIMAL(18,4) NOT NULL CONSTRAINT DF_SI_SubTotal DEFAULT (0),
    [DiscountAmount]  DECIMAL(18,4) NOT NULL CONSTRAINT DF_SI_Discount DEFAULT (0),
    [TaxAmount]       DECIMAL(18,4) NOT NULL CONSTRAINT DF_SI_Tax DEFAULT (0),
    [GrandTotal]      DECIMAL(18,4) NOT NULL CONSTRAINT DF_SI_Grand DEFAULT (0),
    [PaidAmount]      DECIMAL(18,4) NOT NULL CONSTRAINT DF_SI_Paid DEFAULT (0),
    [Notes]           NVARCHAR(500) NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_SalesInvoices_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_SalesInvoices_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_SalesInvoices] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SI_Tenant]    FOREIGN KEY ([TenantId])    REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_SI_Store]     FOREIGN KEY ([StoreId])     REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_SI_Customer]  FOREIGN KEY ([CustomerId])  REFERENCES [dbo].[Customers]([Id])  ON DELETE NO ACTION,
    CONSTRAINT [FK_SI_Warehouse] FOREIGN KEY ([WarehouseId]) REFERENCES [dbo].[Warehouses]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [CK_SI_Paid] CHECK ([PaidAmount] >= 0 AND [PaidAmount] <= [GrandTotal]),
    CONSTRAINT [CK_SI_Grand] CHECK ([GrandTotal] >= 0),
    CONSTRAINT [CK_SI_InvoiceType] CHECK ([InvoiceType] IN ('POS','Web','Wholesale','Return','Quotation'))
);
GO
CREATE NONCLUSTERED INDEX [IX_SI_Tenant_Store] ON [dbo].[SalesInvoices]([TenantId],[StoreId]) WHERE [IsDeleted]=0;
CREATE UNIQUE NONCLUSTERED INDEX [UX_SI_Number] ON [dbo].[SalesInvoices]([TenantId],[InvoiceNumber]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_SI_Customer_Status] ON [dbo].[SalesInvoices]([TenantId],[CustomerId],[Status]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_SI_Date] ON [dbo].[SalesInvoices]([TenantId],[InvoiceDate]) INCLUDE ([GrandTotal],[PaidAmount]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_SI_Type] ON [dbo].[SalesInvoices]([TenantId],[InvoiceType],[Status]) WHERE [IsDeleted]=0;
GO
```

> **ملاحظة تصميمية:** `InvoiceType` هو **نوع المستند** (يحكم سلوك الفاتورة ومعالجتها)، ويختلف عن أي مفهوم "قناة تسويقية". مثلاً `Quotation` (عرض سعر) لا يخصم المخزون ولا يُنشئ قيداً مالياً حتى يُحوَّل لفاتورة فعلية، بينما `POS` يفرّغ المخزون فوراً. لا نعتمد على عمود `Channel` منفصل — `InvoiceType` يكفي ويحكم منطق المعالجة.
```

### 8.2 SalesInvoiceItems

```sql
CREATE TABLE [dbo].[SalesInvoiceItems]
(
    [SalesInvoiceId]  BIGINT        NOT NULL,
    [ProductId]       BIGINT        NOT NULL,
    [Quantity]        DECIMAL(18,4) NOT NULL,
    [UnitPrice]       DECIMAL(18,4) NOT NULL,   -- سعر البيع (snapshot) — يُستخدم في المرتجعات
    [UnitCost]        DECIMAL(18,4) NOT NULL CONSTRAINT DF_SII_Cost DEFAULT (0), -- COGS snapshot
    [DiscountPercent] DECIMAL(9,4)  NOT NULL CONSTRAINT DF_SII_DiscPct DEFAULT (0),
    [DiscountAmount]  DECIMAL(18,4) NOT NULL CONSTRAINT DF_SII_DiscAmt DEFAULT (0),
    [TaxRate]         DECIMAL(9,4)  NOT NULL CONSTRAINT DF_SII_TaxRate DEFAULT (0),
    [TaxAmount]       DECIMAL(18,4) NOT NULL CONSTRAINT DF_SII_TaxAmt DEFAULT (0),
    [LineTotal]       DECIMAL(18,4) NOT NULL,
    [ReturnedQty]     DECIMAL(18,4) NOT NULL CONSTRAINT DF_SII_Returned DEFAULT (0), -- تراكمي للمرتجعات

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_SII_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_SII_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_SalesInvoiceItems] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SII_Tenant]  FOREIGN KEY ([TenantId])       REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_SII_Invoice] FOREIGN KEY ([SalesInvoiceId]) REFERENCES [dbo].[SalesInvoices]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SII_Product] FOREIGN KEY ([ProductId])      REFERENCES [dbo].[Products]([Id])      ON DELETE NO ACTION,
    CONSTRAINT [CK_SII_Qty] CHECK ([Quantity] > 0 AND [ReturnedQty] >= 0 AND [ReturnedQty] <= [Quantity])
);
GO
CREATE NONCLUSTERED INDEX [IX_SII_Invoice] ON [dbo].[SalesInvoiceItems]([SalesInvoiceId]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_SII_Product] ON [dbo].[SalesInvoiceItems]([TenantId],[ProductId]) WHERE [IsDeleted]=0;
GO
```

### 8.3 Payments

```sql
CREATE TABLE [dbo].[Payments]
(
    [ReferenceType]   TINYINT       NOT NULL,   -- 1=SalesInvoice,2=PurchaseInvoice,3=SalesReturn,4=PurchaseReturn
    [ReferenceId]     BIGINT        NOT NULL,   -- Id للمستند المرتبط (SalesInvoiceId مثلاً)
    [CustomerId]      BIGINT        NULL,
    [SupplierId]      BIGINT        NULL,
    [PaymentNumber]   VARCHAR(30)   NOT NULL,   -- من Sequences: 'PAY-000567'
    [PaymentDate]     DATETIME2(3)  NOT NULL,
    [Method]          TINYINT       NOT NULL,   -- 0=Cash,1=Card,2=BankTransfer,3=Cheque,4=Wallet
    [Direction]       TINYINT       NOT NULL,   -- 1=Inflow(قبض),2=Outflow(دفع/إرجاع)
    [Amount]          DECIMAL(18,4) NOT NULL,
    [Status]          VARCHAR(20)   NOT NULL CONSTRAINT DF_Payments_Status DEFAULT ('Completed'),
        -- 'Pending' (شيك مؤجّل/تحويل قيد التأكيد) | 'Completed' (تمّ فعلاً) | 'Cancelled' (أُلغي/ارتدّ)
        -- فقط الدفعات Completed تُحتسب في رصيد العميل/المورد والصندوق
    [ReferenceNo]     NVARCHAR(50)  NULL,        -- رقم مرجعي (شيك/تحويل)
    [Notes]           NVARCHAR(300) NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_Payments_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_Payments_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_Payments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Payments_Tenant]   FOREIGN KEY ([TenantId])   REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_Payments_Store]    FOREIGN KEY ([StoreId])    REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_Payments_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Payments_Supplier] FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Suppliers]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [CK_Payments_Status] CHECK ([Status] IN ('Pending','Completed','Cancelled'))
);
GO
CREATE NONCLUSTERED INDEX [IX_Payments_Tenant_Store] ON [dbo].[Payments]([TenantId],[StoreId]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_Payments_Status] ON [dbo].[Payments]([TenantId],[Status]) WHERE [IsDeleted]=0;
CREATE UNIQUE NONCLUSTERED INDEX [UX_Payments_Number] ON [dbo].[Payments]([TenantId],[PaymentNumber]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_Payments_Reference] ON [dbo].[Payments]([TenantId],[ReferenceType],[ReferenceId]) WHERE [IsDeleted]=0;
GO
```

---

## 9) الـ API

> Base: `/api/v1/sales`.

| Method | Endpoint | الوصف |
|--------|----------|-------|
| `POST` | `/invoices` | إنشاء فاتورة (Draft) |
| `POST` | `/invoices/{id}/confirm` | تأكيد → خصم مخزون + رصيد عميل |
| `POST` | `/invoices/{id}/payments` | تسجيل دفعة |
| `GET`  | `/invoices/{id}` | جلب فاتورة كاملة |
| `GET`  | `/invoices` | قائمة مع فلاتر وترقيم |
| `DELETE` | `/invoices/{id}` | حذف Soft (بشروط) |

**مثال — تأكيد فاتورة (Request):**

```json
POST /api/v1/sales/invoices/231/confirm
{
  "warehouseId": 2,
  "payment": { "method": 0, "amount": 500.0000 }
}
```

**Response (200):**

```json
{
  "invoiceId": 231,
  "invoiceNumber": "INV-000231",
  "status": "PartiallyPaid",
  "grandTotal": 1265.0000,
  "paidAmount": 500.0000,
  "remainingAmount": 765.0000,
  "stockMovementsCreated": 3,
  "customerNewBalance": 765.0000
}
```

---

## 10) مخطط التدفّق (Flow Chart)

```
   ┌──────────────┐
   │ إنشاء (Draft) │ — لا أثر مخزوني/محاسبي
   └──────┬───────┘
          ▼
   ┌──────────────────────┐
   │ تأكيد (Confirm)       │──► BEGIN TRAN
   └──────────┬───────────┘
              ▼
   [تحقق حد الائتمان] ── [تحقق توفّر الكمية]
              ▼
   [Stock -= Qty] ── [StockMovement: SaleIssue] ── [حفظ COGS snapshot]
              ▼
   [Customer.Balance += Remaining] ── [Sequences++]
              ▼
   [تسجيل Payment (اختياري)] ──► إن اكتمل الدفع ⇒ Status=Paid
              ▼
         COMMIT TRAN  (أي فشل ⇒ ROLLBACK كامل)
```

---

## 11) ماذا يحدث عند: الحذف / التعديل / تغيير السعر

- **الحذف:** فاتورة `Draft` تُحذف Soft بلا أثر. فاتورة `Confirmed` **لا تُحذف مباشرة**؛ التصحيح عبر **مرتجع بيع** ([16-Sales-Returns.md](16-Sales-Returns.md)). حذف إداري استثنائي (بصلاحية عليا) يُنفّذ معاملة عكسية: إعادة الكمية للمخزون، `StockMovement` بنوع `SaleIssueReversal`، خفض رصيد العميل، عكس الدفعات.
- **التعديل:** مسموح فقط في `Draft`. بعد `Confirmed` كل شيء مُجمَّد (Immutable) حفاظاً على السلامة المحاسبية.
- **تغيير سعر المنتج لاحقاً:** لا يمسّ الفواتير الصادرة — `UnitPrice` و`UnitCost` محفوظان كـ **snapshot** على البند. هذه هي القاعدة التي تضمن أن **مبلغ المرتجع لاحقاً = سعر البيع الأصلي** (مرجع [16-Sales-Returns.md](16-Sales-Returns.md)).

---

## 12) سجل التدقيق (Audit Log)

`AuditLogs`: `EntityName='SalesInvoice'`, `Action` (Created/Confirmed/PaymentReceived/Deleted), `OldValues/NewValues` (JSON), `UserId`, `IpAddress`, `Timestamp UTC`. الدفعات في `Payments` والحركات في `StockMovements` تشكّل ledger غير قابل للتعديل يدعم إعادة البناء المحاسبي.

---

## 13) الأخطاء المحتملة (Possible Errors)

| الكود | الحالة | الرسالة |
|-------|--------|---------|
| `SAL-4001` | 422 | الكمية المطلوبة تتجاوز المتاح في المخزون |
| `SAL-4002` | 422 | تجاوز حدّ ائتمان العميل |
| `SAL-4003` | 403 | البيع بأقل من التكلفة يتطلّب صلاحية خاصة |
| `SAL-4004` | 409 | تعارض تزامن على المخزون (ConcurrencyStamp) |
| `SAL-4005` | 422 | لا يمكن تعديل فاتورة مؤكدة |
| `SAL-4006` | 400 | مبلغ الدفعة يتجاوز المتبقي |

---

## 14) الأداء (Performance)

- خصم المخزون بـ **UPDATE ... WHERE QtyOnHand >= @Qty** (فحص وتحديث ذرّي يمنع البيع الزائد دون قفل متشائم).
- فهرس `IX_SI_Date` (Covering عبر `INCLUDE`) يخدم تقارير المبيعات اليومية دون Key Lookup.
- ترقيم صفحات Keyset (Seek) بدل `OFFSET` للقوائم الكبيرة.
- تجميع كتابة البنود بـ `TVP`/`SqlBulkCopy` عند الفواتير الكبيرة.

---

## 15) الأمان (Security)

- **Tenant Isolation** على كل استعلام (Global Query Filter).
- **صلاحيات مفصّلة** للخصم والبيع تحت التكلفة (فصل الواجبات).
- **Immutability** بعد التأكيد يمنع التلاعب بالإجماليات.
- **Concurrency** عبر `ROWVERSION` على الفاتورة والمخزون.
- التحقق من حدّ الائتمان server-side حصراً (لا يُوثَق بالـ client).
- كل عملية مالية مُدقَّقة ومربوطة بالمستخدم و IP.
