# 16 — Sales Returns (مرتجعات البيع)

> **القاعدة المحورية (Golden Rule):** كل مرتجع بيع **يجب** أن يشير دائماً إلى **الفاتورة الأصلية وبنودها الأصلية**، يُعيد الكمية إلى المخزون، ويكون **مبلغ الإرجاع = سعر البيع الأصلي المُخزَّن على بند الفاتورة (snapshot)** — **حتى لو تغيّر سعر المنتج لاحقاً**. يدعم المرتجع الكامل والجزئي، ويمنع إرجاع كمية أكبر من المُباعة. يلتزم بالمعايير في [04-Database-Design.md](04-Database-Design.md).

---

## 1) الهدف (Purpose)

- استرجاع بضاعة مُباعة (كلياً أو جزئياً) مع ربطها الإجباري بفاتورة البيع الأصلية.
- إعادة الكمية المرتجعة إلى المخزون ذرّياً (`StockMovement` بنوع `SaleReturn`).
- ردّ القيمة للعميل بـ **سعر البيع الأصلي** بغضّ النظر عن أي تغيّر لاحق في سعر المنتج.
- تخفيض رصيد العميل المدين أو صرف نقدي حسب طريقة الردّ.
- منع الاسترجاع الزائد (Over-Return) عبر التتبّع التراكمي لكل بند فاتورة.

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية | الوصف |
|----------|-------|
| `Sales.Return.View` | عرض المرتجعات |
| `Sales.Return.Create` | إنشاء مرتجع مرتبط بفاتورة |
| `Sales.Return.Confirm` | تأكيد المرتجع (يعيد المخزون ويردّ القيمة) |
| `Sales.Return.Delete` | حذف Soft (بشروط) |
| `Sales.Return.RefundCash` | صرف نقدي مقابل المرتجع |
| `Sales.Return.OverridePrice` | (نادر) تعديل قيمة الردّ يدوياً — مُدقَّق بشدّة |

---

## 3) تصميم الصفحة (Page Layout)

```
┌──────────────────────────────────────────────────────────────────────┐
│  مرتجعات البيع                                  [+ مرتجع من فاتورة]      │
├──────────────────────────────────────────────────────────────────────┤
│  ابحث عن الفاتورة الأصلية: [ INV-000231 🔍 ]                            │
├──────────────────────────────────────────────────────────────────────┤
│  بنود الفاتورة الأصلية (اختر ما يُرتجع):                                 │
│  ☑ المنتج │ المُباع │ المُرتجع سابقاً │ المتبقي │ الكمية المرتجعة │ السعر  │
│  ☑ حليب    10        2                8        [ 3 ]           2.50   │
│  ☐ سكر     5         0                5        [ 0 ]           1.20   │
├──────────────────────────────────────────────────────────────────────┤
│  طريقة الردّ: (•) خصم من الرصيد ( ) نقدي        الإجمالي المُرتجع: 7.50  │
└──────────────────────────────────────────────────────────────────────┘
```

**ملاحظة UX:** لا يمكن بدء مرتجع دون اختيار فاتورة أصلية. السعر معروض للقراءة فقط (snapshot) وغير قابل للتعديل إلا بصلاحية `OverridePrice`.

---

## 4) الأزرار (Buttons)

| الزر | الإجراء | الصلاحية |
|------|---------|----------|
| **+ مرتجع من فاتورة** | البحث عن فاتورة أصلية وتحميل بنودها | `Return.Create` |
| **مرتجع كامل** | تعبئة كل الكميات المتبقية تلقائياً | `Return.Create` |
| **تأكيد المرتجع** | إعادة المخزون + ردّ القيمة | `Return.Confirm` |
| **صرف نقدي** | ردّ نقدي بدل خصم الرصيد | `Return.RefundCash` |
| **طباعة** | إشعار دائن / إيصال مرتجع | `Return.View` |
| **حذف** | Soft Delete (بشروط) | `Return.Delete` |

---

## 5) الحقول (Fields)

### رأس المرتجع
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|--------|---------|
| `SalesInvoiceId` | مرجع | ✔ | **الفاتورة الأصلية (إجباري)** |
| `CustomerId` | مرجع | ✔ | يُشتقّ من الفاتورة الأصلية |
| `ReturnDate` | تاريخ | ✔ | تاريخ المرتجع |
| `RefundMethod` | قائمة | ✔ | BalanceCredit / Cash |
| `Reason` | قائمة/نص | ✘ | سبب الإرجاع (تالف/خطأ صنف/...) |

### بنود المرتجع
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|--------|---------|
| `SalesInvoiceItemId` | مرجع | ✔ | **بند الفاتورة الأصلي (إجباري)** |
| `ProductId` | مرجع | محسوب | من البند الأصلي |
| `ReturnedQty` | عدد | ✔ | ≤ (المُباع − المُرتجع سابقاً) |
| `UnitPrice` | مبلغ | محسوب | **snapshot من البند الأصلي — غير قابل للتعديل** |
| `LineRefund` | مبلغ | محسوب | `ReturnedQty × UnitPrice` (بعد الخصم النسبي) |

---

## 6) التحقق (Validation)

- `SalesInvoiceId` إلزامي، موجود، مؤكّد (`Confirmed`/`Paid`)، وينتمي لنفس المستأجر والعميل.
- كل بند مرتجع مرتبط بـ `SalesInvoiceItemId` من **نفس** الفاتورة.
- **منع الاسترجاع الزائد:** `ReturnedQty ≤ (SalesInvoiceItem.Quantity − SalesInvoiceItem.ReturnedQty)`. يُفرَض قاعديّاً عبر `CHECK` + منطقياً في الـ Command.
- `UnitPrice` يُؤخذ من البند الأصلي دائماً؛ لا يُقبل سعر من العميل/الواجهة.
- لا مرتجع بمجموع كميات = 0.
- الصرف النقدي يتطلّب `RefundCash`؛ وإلا يقتصر على خصم الرصيد.

---

## 7) قواعد العمل (Business Rules)

1. **الربط الإجباري:** لا يوجد مرتجع "حرّ" بلا فاتورة. `SalesReturns.SalesInvoiceId` NOT NULL.
2. **تجميد السعر (Price Freeze):** قيمة الردّ تُحسب من `SalesInvoiceItems.UnitPrice` (snapshot لحظة البيع). تغيّر سعر المنتج في `Products` **لا يؤثّر إطلاقاً** على مبلغ المرتجع.

   ```
   LineRefund = ReturnedQty × OriginalUnitPrice
   -- بعد تطبيق نفس نسبة خصم البند الأصلي والضريبة الأصلية بالتناسب
   ProportionalDiscount = ReturnedQty × (OriginalLineDiscount / OriginalQuantity)
   ProportionalTax      = ReturnedQty × (OriginalLineTax      / OriginalQuantity)
   LineRefund = (ReturnedQty × OriginalUnitPrice) − ProportionalDiscount + ProportionalTax
   ```

3. **إعادة المخزون:** عند التأكيد يُضاف `ReturnedQty` إلى `Stock` مع `StockMovement` بنوع `SaleReturn`. تُعاد **بتكلفة COGS الأصلية** (`SalesInvoiceItems.UnitCost` snapshot) للحفاظ على دقّة متوسط التكلفة.
4. **التتبّع التراكمي:** يُزاد `SalesInvoiceItems.ReturnedQty` تراكمياً لمنع تجاوز المُباع عبر عدة مرتجعات.
5. **ردّ القيمة:** إمّا خفض `Customer.Balance` (إشعار دائن) أو `Payment` بنوع `Direction=Outflow` (صرف نقدي).
6. كل ذلك داخل **Transaction ذرّي واحد**؛ أي فشل ⇒ Rollback. الرقم من `Sequences` بـ `DocType='SALES_RETURN'`.

---

## 8) جداول قاعدة البيانات (Database Tables — SQL كامل)

### 8.1 SalesReturns

```sql
CREATE TABLE [dbo].[SalesReturns]
(
    [SalesInvoiceId]  BIGINT        NOT NULL,   -- FK للفاتورة الأصلية (إجباري)
    [CustomerId]      BIGINT        NOT NULL,
    [WarehouseId]     BIGINT        NULL,
    [ReturnNumber]    VARCHAR(30)   NOT NULL,   -- من Sequences: 'SRT-000078'
    [ReturnDate]      DATETIME2(3)  NOT NULL,
    [RefundMethod]    TINYINT       NOT NULL,   -- 0=BalanceCredit,1=Cash
    [Status]          TINYINT       NOT NULL CONSTRAINT DF_SR_Status DEFAULT (0),
        -- 0=Draft,1=Confirmed,2=Cancelled
    [Reason]          NVARCHAR(300) NULL,
    [SubTotal]        DECIMAL(18,4) NOT NULL CONSTRAINT DF_SR_SubTotal DEFAULT (0),
    [TaxAmount]       DECIMAL(18,4) NOT NULL CONSTRAINT DF_SR_Tax DEFAULT (0),
    [TotalRefund]     DECIMAL(18,4) NOT NULL CONSTRAINT DF_SR_Refund DEFAULT (0),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_SalesReturns_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_SalesReturns_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_SalesReturns] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SR_Tenant]    FOREIGN KEY ([TenantId])       REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_SR_Store]     FOREIGN KEY ([StoreId])        REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_SR_Invoice]   FOREIGN KEY ([SalesInvoiceId]) REFERENCES [dbo].[SalesInvoices]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SR_Customer]  FOREIGN KEY ([CustomerId])     REFERENCES [dbo].[Customers]([Id])     ON DELETE NO ACTION,
    CONSTRAINT [FK_SR_Warehouse] FOREIGN KEY ([WarehouseId])    REFERENCES [dbo].[Warehouses]([Id])    ON DELETE NO ACTION,
    CONSTRAINT [CK_SR_Refund] CHECK ([TotalRefund] >= 0)
);
GO
CREATE NONCLUSTERED INDEX [IX_SR_Tenant_Store] ON [dbo].[SalesReturns]([TenantId],[StoreId]) WHERE [IsDeleted]=0;
CREATE UNIQUE NONCLUSTERED INDEX [UX_SR_Number] ON [dbo].[SalesReturns]([TenantId],[ReturnNumber]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_SR_Invoice] ON [dbo].[SalesReturns]([TenantId],[SalesInvoiceId]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_SR_Customer] ON [dbo].[SalesReturns]([TenantId],[CustomerId]) WHERE [IsDeleted]=0;
GO
```

### 8.2 SalesReturnItems

```sql
CREATE TABLE [dbo].[SalesReturnItems]
(
    [SalesReturnId]      BIGINT        NOT NULL,
    [SalesInvoiceItemId] BIGINT        NOT NULL,  -- FK لبند الفاتورة الأصلي (إجباري)
    [ProductId]          BIGINT        NOT NULL,
    [ReturnedQty]        DECIMAL(18,4) NOT NULL,
    [UnitPrice]          DECIMAL(18,4) NOT NULL,   -- snapshot: سعر البيع الأصلي (ثابت)
    [UnitCost]           DECIMAL(18,4) NOT NULL CONSTRAINT DF_SRI_Cost DEFAULT (0), -- COGS الأصلي
    [DiscountAmount]     DECIMAL(18,4) NOT NULL CONSTRAINT DF_SRI_Disc DEFAULT (0), -- خصم متناسب
    [TaxAmount]          DECIMAL(18,4) NOT NULL CONSTRAINT DF_SRI_Tax DEFAULT (0),  -- ضريبة متناسبة
    [LineRefund]         DECIMAL(18,4) NOT NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_SRI_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_SRI_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_SalesReturnItems] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SRI_Tenant]      FOREIGN KEY ([TenantId])           REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_SRI_Return]      FOREIGN KEY ([SalesReturnId])      REFERENCES [dbo].[SalesReturns]([Id])      ON DELETE NO ACTION,
    CONSTRAINT [FK_SRI_InvoiceItem] FOREIGN KEY ([SalesInvoiceItemId]) REFERENCES [dbo].[SalesInvoiceItems]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SRI_Product]     FOREIGN KEY ([ProductId])          REFERENCES [dbo].[Products]([Id])          ON DELETE NO ACTION,
    CONSTRAINT [CK_SRI_Qty] CHECK ([ReturnedQty] > 0 AND [UnitPrice] >= 0)
);
GO
CREATE NONCLUSTERED INDEX [IX_SRI_Return]      ON [dbo].[SalesReturnItems]([SalesReturnId]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_SRI_InvoiceItem] ON [dbo].[SalesReturnItems]([TenantId],[SalesInvoiceItemId]) WHERE [IsDeleted]=0;
CREATE NONCLUSTERED INDEX [IX_SRI_Product]     ON [dbo].[SalesReturnItems]([TenantId],[ProductId]) WHERE [IsDeleted]=0;
GO
```

> **ضمان عدم الاسترجاع الزائد على مستوى DB:** بالإضافة إلى فحص الـ Command، يُطبَّق قيد منطقي في الإجراء المخزّن للتأكيد: تحديث `SalesInvoiceItems.ReturnedQty` عبر `UPDATE ... WHERE (Quantity - ReturnedQty) >= @ReturnedQty` — إن أثّر على 0 صف ⇒ رفض المرتجع (منع تجاوز تزامني).

---

## 9) الـ API

> Base: `/api/v1/sales/returns`.

| Method | Endpoint | الوصف |
|--------|----------|-------|
| `GET`  | `/eligible/{invoiceId}` | جلب بنود الفاتورة القابلة للإرجاع (مع المتبقي) |
| `POST` | `/` | إنشاء مرتجع (Draft) مرتبط بفاتورة |
| `POST` | `/{id}/confirm` | تأكيد → إعادة مخزون + ردّ قيمة |
| `GET`  | `/{id}` | جلب مرتجع |
| `DELETE` | `/{id}` | حذف Soft (بشروط) |

**مثال — إنشاء مرتجع جزئي (Request):**

```json
POST /api/v1/sales/returns
{
  "salesInvoiceId": 231,
  "refundMethod": 0,
  "reason": "منتج تالف",
  "lines": [
    { "salesInvoiceItemId": 900, "returnedQty": 3 }
  ]
}
```

**Response (201):** لاحظ أن `unitPrice` جاء من snapshot الفاتورة، لا من سعر المنتج الحالي.

```json
{
  "salesReturnId": 78,
  "returnNumber": "SRT-000078",
  "linkedInvoice": "INV-000231",
  "lines": [
    { "productId": 55, "returnedQty": 3, "unitPrice": 2.5000, "lineRefund": 7.5000 }
  ],
  "totalRefund": 7.5000,
  "customerNewBalance": 757.5000,
  "stockReturned": true
}
```

---

## 10) مخطط التدفّق (Flow Chart)

```
   ┌────────────────────────────┐
   │ اختيار الفاتورة الأصلية      │ (إجباري)
   └───────────┬────────────────┘
               ▼
   [تحميل البنود + المتبقي القابل للإرجاع]
               ▼
   ┌────────────────────────────┐
   │ إدخال الكميات المرتجعة       │  السعر = snapshot (مقفول)
   └───────────┬────────────────┘
               ▼
   ┌────────────────────────────┐
   │ تأكيد المرتجع               │──► BEGIN TRAN
   └───────────┬────────────────┘
               ▼
   [UPDATE ReturnedQty WHERE المتبقي>=المطلوب]  ← يمنع التجاوز
               ▼ (0 صف ⇒ رفض)
   [Stock += ReturnedQty بتكلفة COGS الأصلية] ── [StockMovement: SaleReturn]
               ▼
   [Refund: خفض Balance أو Payment Outflow] ── [Sequences++]
               ▼
          COMMIT TRAN  (أي فشل ⇒ ROLLBACK)
```

---

## 11) ماذا يحدث عند: الحذف / التعديل / تغيير السعر

- **الحذف:** مرتجع `Draft` يُحذف Soft بلا أثر. مرتجع `Confirmed` **لا يُحذف** لأنه أثّر محاسبياً؛ التصحيح عبر إلغاء (Cancel) يُنشئ معاملة عكسية: خصم الكمية المُعادة من المخزون، إرجاع `SalesInvoiceItems.ReturnedQty`، عكس ردّ القيمة.
- **التعديل:** مسموح فقط في `Draft`. بعد التأكيد مُجمَّد.
- **تغيير سعر المنتج لاحقاً (القاعدة المحورية):** لا يؤثّر مطلقاً. مبلغ الردّ مثبّت على `UnitPrice` المُخزَّن في `SalesInvoiceItems` لحظة البيع (snapshot). حتى لو صار سعر المنتج اليوم 4.00 بينما بيع أصلاً بـ 2.50 — **يُردّ 2.50**.

---

## 12) سجل التدقيق (Audit Log)

`AuditLogs`: `EntityName='SalesReturn'`, `Action` (Created/Confirmed/Cancelled), `OldValues/NewValues` (JSON) متضمّنة `LinkedInvoiceNumber` و`OriginalUnitPrice`. أي استخدام لـ `OverridePrice` يُسجَّل بعلامة تحذير عالية للمراجعة.

---

## 13) الأخطاء المحتملة (Possible Errors)

| الكود | الحالة | الرسالة |
|-------|--------|---------|
| `SRT-4001` | 400 | لا يمكن إنشاء مرتجع دون فاتورة أصلية |
| `SRT-4002` | 422 | الكمية المرتجعة تتجاوز المُباع المتبقي |
| `SRT-4003` | 422 | الفاتورة الأصلية غير مؤكّدة/ملغاة |
| `SRT-4004` | 409 | تعارض تزامني على كمية الإرجاع |
| `SRT-4005` | 403 | تعديل سعر الردّ يتطلّب صلاحية OverridePrice |

---

## 14) الأداء (Performance)

- فحص ومنع التجاوز عبر **UPDATE مشروط ذرّي** (بلا SELECT-ثم-UPDATE) يحمي من سباق الاسترجاع المتزامن.
- فهرس `IX_SR_Invoice` يخدم عرض "كل مرتجعات هذه الفاتورة" فوراً.
- إعادة المخزون بـ Set-based UPDATE على `Stock`.

---

## 15) الأمان (Security)

- **مصدر السعر الوحيد** هو snapshot الفاتورة server-side — لا يُقبل سعر من الواجهة (يمنع الاحتيال بردّ سعر أعلى).
- **Tenant Isolation** يمنع ربط مرتجع بفاتورة مستأجر آخر.
- **منع الاسترجاع الزائد** مفروض قاعديّاً وتزامنياً.
- **Immutability** بعد التأكيد؛ التصحيح عبر إلغاء مُدقَّق فقط.
- `OverridePrice` صلاحية عالية الحساسية، مُدقَّقة بالكامل.
