# 17 — Purchase Returns (مرتجعات الشراء)

> يوثّق هذا الملف **إرجاع البضاعة إلى المورد**. القاعدة المحورية موازية لمرتجع البيع: كل مرتجع شراء **يجب** أن يشير إلى **فاتورة الشراء الأصلية وبندها**، يخصم الكمية من المخزون، ويستخدم **تكلفة الشراء الأصلية (snapshot)** لحساب قيمة الإرجاع وتصحيح متوسط التكلفة، ويؤثّر على **رصيد المورد**. يلتزم بالمعايير في [04-Database-Design.md](04-Database-Design.md).

---

## 1) الهدف (Purpose)

- إرجاع بضاعة مُشتراة إلى المورد (تالفة، منتهية، خطأ صنف، فائض) كلياً أو جزئياً.
- خصم الكمية المرتجعة من المخزون ذرّياً (`StockMovement` بنوع `PurchaseReturn`).
- تخفيض رصيد المورد الدائن (`Suppliers.Balance -= ReturnValue`) أو استلام مبلغ نقدي منه.
- تصحيح متوسط التكلفة باستخدام تكلفة الشراء الأصلية للبند (snapshot).
- منع إرجاع كمية أكبر من المُستلمة، ومنع إرجاع ما بيع بالفعل (فحص التوفّر).

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية | الوصف |
|----------|-------|
| `Purchasing.Return.View` | عرض مرتجعات الشراء |
| `Purchasing.Return.Create` | إنشاء مرتجع مرتبط بفاتورة شراء |
| `Purchasing.Return.Confirm` | تأكيد المرتجع (يخصم المخزون) |
| `Purchasing.Return.Delete` | حذف Soft (بشروط) |
| `Purchasing.Return.RefundCash` | استلام مبلغ نقدي من المورد |

---

## 3) تصميم الصفحة (Page Layout)

```
┌──────────────────────────────────────────────────────────────────────┐
│  مرتجعات الشراء                              [+ مرتجع من فاتورة شراء]    │
├──────────────────────────────────────────────────────────────────────┤
│  ابحث عن فاتورة الشراء: [ PUR-000045 🔍 ]      المورد: مؤسسة النور       │
├──────────────────────────────────────────────────────────────────────┤
│  بنود فاتورة الشراء (اختر ما يُرتجع):                                    │
│  ☑ المنتج │ المُستلَم │ المُرتجع سابقاً │ المتبقي │ الكمية المرتجعة │ التكلفة│
│  ☑ حليب    40         5                35       [ 10 ]         12.50  │
│  ☐ سكر     10         0                10       [ 0 ]          8.00   │
├──────────────────────────────────────────────────────────────────────┤
│  طريقة التسوية: (•) خصم من رصيد المورد ( ) نقدي   الإجمالي: 125.00      │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 4) الأزرار (Buttons)

| الزر | الإجراء | الصلاحية |
|------|---------|----------|
| **+ مرتجع من فاتورة شراء** | البحث وتحميل بنود الفاتورة | `Return.Create` |
| **مرتجع كامل** | تعبئة كل الكميات المتبقية | `Return.Create` |
| **تأكيد المرتجع** | خصم المخزون + تسوية المورد | `Return.Confirm` |
| **تسوية نقدية** | استلام نقد بدل خصم الرصيد | `Return.RefundCash` |
| **طباعة** | إشعار مدين للمورد | `Return.View` |
| **حذف** | Soft Delete (بشروط) | `Return.Delete` |

---

## 5) الحقول (Fields)

### رأس المرتجع
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|--------|---------|
| `PurchaseInvoiceId` | مرجع | ✔ | **فاتورة الشراء الأصلية (إجباري)** |
| `SupplierId` | مرجع | ✔ | يُشتقّ من الفاتورة الأصلية |
| `WarehouseId` | مرجع | ✔ | المستودع الذي يُخصم منه |
| `ReturnDate` | تاريخ | ✔ | تاريخ المرتجع |
| `SettlementMethod` | قائمة | ✔ | SupplierBalance / Cash |
| `Reason` | نص/قائمة | ✘ | سبب الإرجاع |

### بنود المرتجع
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|--------|---------|
| `PurchaseInvoiceItemId` | مرجع | ✔ | **بند الفاتورة الأصلي (إجباري)** |
| `ProductId` | مرجع | محسوب | من البند الأصلي |
| `ReturnedQty` | عدد | ✔ | ≤ (المُستلم − المُرتجع سابقاً) وضمن المتاح |
| `UnitCost` | مبلغ | محسوب | **snapshot تكلفة الشراء الأصلية — مقفول** |
| `LineTotal` | مبلغ | محسوب | `ReturnedQty × UnitCost` (± ضريبة متناسبة) |

---

## 6) التحقق (Validation)

- `PurchaseInvoiceId` إلزامي، موجود، مُرحَّل، وينتمي لنفس المستأجر والمورد.
- كل بند مرتبط بـ `PurchaseInvoiceItemId` من **نفس** الفاتورة.
- **منع الإرجاع الزائد:** `ReturnedQty ≤ (PurchaseInvoiceItem.ReceivedQty − PreviouslyReturned)`.
- **فحص التوفّر المخزوني:** `ReturnedQty ≤ QtyOnHand` (لا يمكن إرجاع بضاعة بيعت أو غير متوفرة) ما لم يُسمح بالسالب.
- `UnitCost` يُؤخذ من البند الأصلي دائماً — لا يُقبل من الواجهة.
- الصرف/الاستلام النقدي يتطلّب `RefundCash`.

---

## 7) قواعد العمل (Business Rules)

1. **الربط الإجباري:** `PurchaseReturns.PurchaseInvoiceId` NOT NULL — لا مرتجع بلا فاتورة شراء.
2. **تجميد التكلفة (Cost Freeze):** قيمة الإرجاع من `PurchaseInvoiceItems.UnitCost` (snapshot لحظة الشراء). تغيّر أسعار الموردين لاحقاً لا يؤثّر.
3. **خصم المخزون:** عند التأكيد يُخصم `ReturnedQty` من `Stock` مع `StockMovement` بنوع `PurchaseReturn`، ويُعاد حساب متوسط التكلفة:

   ```
   NewAvgCost = ( (OldQtyOnHand × OldAvgCost) − (ReturnedQty × OriginalUnitCost) )
                / (OldQtyOnHand − ReturnedQty)
   -- عند بلوغ الكمية صفراً يُحتفظ بآخر AvgCost معروف
   ```

4. **أثر رصيد المورد:** `Suppliers.Balance -= ReturnValue` (إشعار مدين على المورد)، أو `Payment` بنوع `Direction=Inflow` عند الاسترداد النقدي.
5. **التتبّع التراكمي:** يُزاد عدّاد المُرتجع على بند فاتورة الشراء لمنع تجاوز المُستلم عبر عدة مرتجعات.
6. كل ذلك داخل **Transaction ذرّي**. الرقم من `Sequences` بـ `DocType='PURCHASE_RETURN'`.

---

## 8) جداول قاعدة البيانات (Database Tables — SQL كامل)

### 8.1 PurchaseReturns

```sql
CREATE TABLE [dbo].[PurchaseReturns]
(
    [PurchaseInvoiceId] BIGINT        NOT NULL,   -- FK لفاتورة الشراء الأصلية (إجباري)
    [SupplierId]        BIGINT        NOT NULL,
    [WarehouseId]       BIGINT        NULL,
    [ReturnNumber]      VARCHAR(30)   NOT NULL,    -- من Sequences: 'PRT-000034'
    [ReturnDate]        DATETIME2(3)  NOT NULL,
    [SettlementMethod]  TINYINT       NOT NULL,    -- 0=SupplierBalance,1=Cash
    [Status]            TINYINT       NOT NULL CONSTRAINT DF_PR_Status DEFAULT (0),
        -- 0=Draft,1=Confirmed,2=Cancelled
    [Reason]            NVARCHAR(300) NULL,
    [SubTotal]          DECIMAL(18,4) NOT NULL CONSTRAINT DF_PR_SubTotal DEFAULT (0),
    [TaxAmount]         DECIMAL(18,4) NOT NULL CONSTRAINT DF_PR_Tax DEFAULT (0),
    [TotalReturn]       DECIMAL(18,4) NOT NULL CONSTRAINT DF_PR_Total DEFAULT (0),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_PurchaseReturns_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_PurchaseReturns_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_PurchaseReturns] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PR_Tenant]    FOREIGN KEY ([TenantId])          REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_PR_Store]     FOREIGN KEY ([StoreId])           REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_PR_Invoice]   FOREIGN KEY ([PurchaseInvoiceId]) REFERENCES [dbo].[PurchaseInvoices]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PR_Supplier]  FOREIGN KEY ([SupplierId])        REFERENCES [dbo].[Suppliers]([Id])        ON DELETE NO ACTION,
    CONSTRAINT [FK_PR_Warehouse] FOREIGN KEY ([WarehouseId])       REFERENCES [dbo].[Warehouses]([Id])       ON DELETE NO ACTION,
    CONSTRAINT [CK_PR_Total] CHECK ([TotalReturn] >= 0)
);
GO
CREATE NONCLUSTERED INDEX [IX_PR_Tenant_Store] ON [dbo].[PurchaseReturns]([TenantId],[StoreId]) WHERE [IsDeleted]=0;
CREATE UNIQUE NONCLUSTERED INDEX [UX_PR_Number] ON [dbo].[PurchaseReturns]([TenantId],[ReturnNumber]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_PR_Invoice]  ON [dbo].[PurchaseReturns]([TenantId],[PurchaseInvoiceId]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_PR_Supplier] ON [dbo].[PurchaseReturns]([TenantId],[SupplierId]) WHERE [IsDeleted]=0;
GO
```

### 8.2 PurchaseReturnItems

```sql
CREATE TABLE [dbo].[PurchaseReturnItems]
(
    [PurchaseReturnId]      BIGINT        NOT NULL,
    [PurchaseInvoiceItemId] BIGINT        NOT NULL,  -- FK لبند فاتورة الشراء الأصلي (إجباري)
    [ProductId]             BIGINT        NOT NULL,
    [ReturnedQty]           DECIMAL(18,4) NOT NULL,
    [UnitCost]              DECIMAL(18,4) NOT NULL,   -- snapshot: تكلفة الشراء الأصلية (ثابتة)
    [DiscountAmount]        DECIMAL(18,4) NOT NULL CONSTRAINT DF_PRI_Disc DEFAULT (0),
    [TaxAmount]             DECIMAL(18,4) NOT NULL CONSTRAINT DF_PRI_Tax DEFAULT (0),
    [LineTotal]             DECIMAL(18,4) NOT NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_PRI_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_PRI_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_PurchaseReturnItems] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PRI_Tenant]      FOREIGN KEY ([TenantId])              REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_PRI_Return]      FOREIGN KEY ([PurchaseReturnId])      REFERENCES [dbo].[PurchaseReturns]([Id])      ON DELETE NO ACTION,
    CONSTRAINT [FK_PRI_InvoiceItem] FOREIGN KEY ([PurchaseInvoiceItemId]) REFERENCES [dbo].[PurchaseInvoiceItems]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PRI_Product]     FOREIGN KEY ([ProductId])             REFERENCES [dbo].[Products]([Id])             ON DELETE NO ACTION,
    CONSTRAINT [CK_PRI_Qty] CHECK ([ReturnedQty] > 0 AND [UnitCost] >= 0)
);
GO
CREATE NONCLUSTERED INDEX [IX_PRI_Return]      ON [dbo].[PurchaseReturnItems]([PurchaseReturnId]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_PRI_InvoiceItem] ON [dbo].[PurchaseReturnItems]([TenantId],[PurchaseInvoiceItemId]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_PRI_Product]     ON [dbo].[PurchaseReturnItems]([TenantId],[ProductId]) WHERE [IsDeleted]=0;
GO
```

> **ضمان عدم الإرجاع الزائد + توفّر المخزون على مستوى DB:** التأكيد يُنفّذ عبر `UPDATE Stock SET QtyOnHand -= @Qty WHERE QtyOnHand >= @Qty` وتحديث عدّاد المُرتجع بشرط `(ReceivedQty - PreviouslyReturned) >= @Qty`. أي شرط يفشل ⇒ 0 صف ⇒ رفض المرتجع (منع تجاوز تزامني وبيع-ثم-إرجاع).

---

## 9) الـ API

> Base: `/api/v1/purchasing/returns`.

| Method | Endpoint | الوصف |
|--------|----------|-------|
| `GET`  | `/eligible/{purchaseInvoiceId}` | بنود الفاتورة القابلة للإرجاع (مع المتبقي والمتاح) |
| `POST` | `/` | إنشاء مرتجع (Draft) مرتبط بفاتورة شراء |
| `POST` | `/{id}/confirm` | تأكيد → خصم مخزون + تسوية مورد |
| `GET`  | `/{id}` | جلب مرتجع |
| `DELETE` | `/{id}` | حذف Soft (بشروط) |

**مثال — إنشاء مرتجع شراء جزئي (Request):**

```json
POST /api/v1/purchasing/returns
{
  "purchaseInvoiceId": 45,
  "warehouseId": 3,
  "settlementMethod": 0,
  "reason": "بضاعة تالفة",
  "lines": [
    { "purchaseInvoiceItemId": 700, "returnedQty": 10 }
  ]
}
```

**Response (201):**

```json
{
  "purchaseReturnId": 34,
  "returnNumber": "PRT-000034",
  "linkedInvoice": "PUR-000045",
  "lines": [
    { "productId": 55, "returnedQty": 10, "unitCost": 12.5000, "lineTotal": 125.0000 }
  ],
  "totalReturn": 125.0000,
  "supplierNewBalance": 5275.0000,
  "stockDeducted": true
}
```

---

## 10) مخطط التدفّق (Flow Chart)

```
   ┌────────────────────────────┐
   │ اختيار فاتورة الشراء الأصلية │ (إجباري)
   └───────────┬────────────────┘
               ▼
   [تحميل البنود + المتبقي القابل للإرجاع + المتاح مخزونياً]
               ▼
   ┌────────────────────────────┐
   │ إدخال الكميات المرتجعة       │  التكلفة = snapshot (مقفول)
   └───────────┬────────────────┘
               ▼
   ┌────────────────────────────┐
   │ تأكيد المرتجع               │──► BEGIN TRAN
   └───────────┬────────────────┘
               ▼
   [UPDATE Stock -= Qty WHERE Qty>=المطلوب]  ← يمنع السالب والتجاوز
               ▼ (0 صف ⇒ رفض)
   [إعادة حساب AvgCost] ── [StockMovement: PurchaseReturn]
               ▼
   [Supplier.Balance -= Value  أو  Payment Inflow] ── [Sequences++]
               ▼
          COMMIT TRAN  (أي فشل ⇒ ROLLBACK)
```

---

## 11) ماذا يحدث عند: الحذف / التعديل / تغيير السعر

- **الحذف:** مرتجع `Draft` يُحذف Soft بلا أثر. مرتجع `Confirmed` **لا يُحذف**؛ التصحيح عبر إلغاء (Cancel) بمعاملة عكسية: إعادة الكمية للمخزون، تصحيح متوسط التكلفة، رفع `Supplier.Balance`.
- **التعديل:** في `Draft` فقط. بعد التأكيد مُجمَّد.
- **تغيّر تكلفة/سعر المورد لاحقاً:** لا يؤثّر — قيمة الإرجاع مثبّتة على `UnitCost` snapshot في `PurchaseInvoiceItems` لحظة الشراء الأصلي.

---

## 12) سجل التدقيق (Audit Log)

`AuditLogs`: `EntityName='PurchaseReturn'`, `Action` (Created/Confirmed/Cancelled), `OldValues/NewValues` (JSON) متضمّنة `LinkedPurchaseInvoiceNumber` و`OriginalUnitCost` و`SupplierBalanceDelta`. حركات المخزون في `StockMovements` كـ ledger ثابت.

---

## 13) الأخطاء المحتملة (Possible Errors)

| الكود | الحالة | الرسالة |
|-------|--------|---------|
| `PRT-4001` | 400 | لا يمكن إنشاء مرتجع دون فاتورة شراء أصلية |
| `PRT-4002` | 422 | الكمية المرتجعة تتجاوز المُستلم المتبقي |
| `PRT-4003` | 422 | الكمية المرتجعة تتجاوز المتاح في المخزون (بيعت جزئياً) |
| `PRT-4004` | 422 | فاتورة الشراء غير مُرحَّلة/ملغاة |
| `PRT-4005` | 409 | تعارض تزامني على المخزون أو عدّاد الإرجاع |

---

## 14) الأداء (Performance)

- خصم المخزون ومنع التجاوز عبر **UPDATE مشروط ذرّي واحد** (بلا SELECT منفصل).
- فهرس `IX_PR_Supplier` يخدم كشف حساب المورد ومطابقة الإشعارات المدينة.
- فهرس `IX_PR_Invoice` يعرض كل مرتجعات فاتورة الشراء فوراً.
- إعادة حساب متوسط التكلفة بعملية حسابية واحدة بلا جلب صف-صف.

---

## 15) الأمان (Security)

- **مصدر التكلفة الوحيد** هو snapshot الفاتورة server-side — يمنع الاحتيال بتضخيم قيمة الإرجاع للمورد.
- **Tenant Isolation** يمنع ربط مرتجع بفاتورة شراء لمستأجر آخر.
- **منع الإرجاع الزائد وفحص التوفّر** مفروضان قاعديّاً وتزامنياً (لا إرجاع لبضاعة بيعت).
- **Immutability** بعد التأكيد؛ التصحيح عبر إلغاء مُدقَّق.
- **Concurrency** عبر `ROWVERSION` وعبر UPDATE المشروط على المخزون.
