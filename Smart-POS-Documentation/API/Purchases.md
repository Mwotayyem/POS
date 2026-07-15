# API — Purchases (المشتريات)

> توثيق endpoints المشتريات في **Smart ERP POS**. تحت `/api/v1/purchases`. تغطّي دورة الشراء: أمر شراء (PO) ← فاتورة شراء ← استلام (زيادة المخزون) ← مرتجع. كل استلام يزيد المخزون ذرّياً. يلتزم بـ [14-Purchase-Invoices.md](../14-Purchase-Invoices.md) و[17-Purchase-Returns.md](../17-Purchase-Returns.md).

---

## 1) الصلاحيات

| العملية | الصلاحية |
|---------|----------|
| أمر شراء | `purchases.order` |
| فاتورة شراء | `purchases.invoice` |
| استلام | `purchases.receive` |
| مرتجع شراء | `purchases.return` |
| قراءة | `purchases.read` |

---

## 2) POST /api/v1/purchases/orders — إنشاء أمر شراء (PO)

> أمر شراء قبل الاستلام — **لا يؤثّر على المخزون**. حالته `Draft` ثم `Confirmed`.

**Request**
```json
{
  "supplierId": 220,
  "storeId": 12,
  "expectedDate": "2026-07-20",
  "items": [
    { "productId": 8801, "unitId": 3, "qty": 50, "unitCost": 15.00 },
    { "productId": 8802, "unitId": 3, "qty": 30, "unitCost": 14.50 }
  ]
}
```

**Response 201 Created**
```json
{
  "success": true,
  "data": {
    "id": 6001, "orderNumber": "PO-000078",
    "status": "Draft", "totalAmount": 1185.00,
    "expectedDate": "2026-07-20"
  }
}
```

## POST /api/v1/purchases/orders/{id}/confirm — تأكيد الأمر
**Response 200 OK**
```json
{ "success": true, "data": { "id": 6001, "status": "Confirmed" } }
```

---

## 3) POST /api/v1/purchases/invoices — فاتورة شراء + استلام

> إنشاء فاتورة الشراء من أمر (أو مباشرةً) **مع استلام المخزون** ذرّياً. يزيد `Stock.QtyOnHand` ويولّد `StockMovements` من نوع `In`، ويُحدِّث متوسط التكلفة (`AvgCost`).

**Request**
```json
{
  "supplierId": 220,
  "purchaseOrderId": 6001,
  "storeId": 12,
  "warehouseId": 7,
  "supplierInvoiceNo": "SUP-9931",
  "items": [
    { "productId": 8801, "unitId": 3, "qty": 50, "unitCost": 15.00, "taxRate": 16.0, "batchNo": "B2026-07", "expiryDate": "2027-01-31" },
    { "productId": 8802, "unitId": 3, "qty": 30, "unitCost": 14.50, "taxRate": 16.0 }
  ],
  "paidAmount": 500.00
}
```

**Response 201 Created**
```json
{
  "success": true,
  "data": {
    "id": 7100, "invoiceNumber": "PUR-000091",
    "subTotal": 1185.00, "taxAmount": 189.60, "total": 1374.60,
    "paidAmount": 500.00, "balance": 874.60, "status": "Received",
    "stockMovements": [
      { "productId": 8801, "qty": 1200, "balanceAfter": 1680, "newAvgCost": 0.63 },
      { "productId": 8802, "qty": 720,  "balanceAfter": 840,  "newAvgCost": 0.61 }
    ]
  }
}
```
> ملاحظة: `qty` أعلاه بوحدة الأساس (50 كرتونة × 24 = 1200 قطعة) بعد تطبيق `conversionFactor`.

**Response 422** — أمر الشراء مُلغى/مستلَم بالكامل.
```json
{ "success": false, "error": { "code": "ORDER_ALREADY_RECEIVED", "message": "أمر الشراء مستلَم بالكامل" } }
```

---

## 4) POST /api/v1/purchases/invoices/{id}/receive — استلام جزئي

> عند استلام أمر شراء على دفعات. يزيد المخزون بالكمية المستلمة فقط ويترك الباقي معلّقاً.

**Request**
```json
{
  "warehouseId": 7,
  "items": [ { "purchaseOrderItemId": 9001, "qtyReceived": 20 } ]
}
```

**Response 200 OK**
```json
{
  "success": true,
  "data": {
    "invoiceId": 7100, "status": "PartiallyReceived",
    "receivedNow": 20, "remaining": 30,
    "stockMovements": [ { "productId": 8801, "qty": 480, "balanceAfter": 960 } ]
  }
}
```

---

## 5) GET /api/v1/purchases/invoices — قائمة مع Pagination

**Query:** `page`, `pageSize`, `supplierId`, `status`, `fromDate`, `toDate`.

**Response 200 OK**
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 7100, "invoiceNumber": "PUR-000091",
        "supplierId": 220, "supplierName": "Beverage Distributors",
        "total": 1374.60, "paidAmount": 500.00, "balance": 874.60,
        "status": "Received", "createdDate": "2026-07-13T10:00:00Z"
      }
    ],
    "pagination": { "page": 1, "pageSize": 25, "totalItems": 412, "totalPages": 17 }
  }
}
```

---

## 6) GET /api/v1/purchases/invoices/{id} — تفاصيل الفاتورة

**Response 200 OK** — يشمل البنود والدفعات.
```json
{
  "success": true,
  "data": {
    "id": 7100, "invoiceNumber": "PUR-000091", "status": "Received",
    "supplier": { "id": 220, "name": "Beverage Distributors" },
    "items": [
      { "productId": 8801, "productName": "Coca-Cola 330ml", "qty": 50, "unitCost": 15.00, "lineTotal": 750.00, "batchNo": "B2026-07", "expiryDate": "2027-01-31" }
    ],
    "payments": [ { "amount": 500.00, "method": "BankTransfer", "paidAt": "2026-07-13T10:05:00Z" } ]
  }
}
```

---

## 7) POST /api/v1/purchases/invoices/{id}/payments — دفعة للمورّد

**Request**
```json
{ "amount": 874.60, "method": "BankTransfer", "reference": "TRF-77120" }
```

**Response 200 OK** — يُحدِّث `Suppliers.CurrentBalance` و`SupplierPayments`.
```json
{ "success": true, "data": { "paidAmount": 1374.60, "balance": 0.00, "isFullyPaid": true } }
```

---

## 8) POST /api/v1/purchases/returns — مرتجع شراء

> إرجاع بضاعة للمورّد — **يخفّض المخزون** (`StockMovements` نوع `Out`) ويولّد إشعاراً دائناً. ذرّي.

**Request**
```json
{
  "purchaseInvoiceId": 7100,
  "warehouseId": 7,
  "reason": "بضاعة تالفة عند الاستلام",
  "items": [ { "productId": 8801, "unitId": 3, "qty": 2, "unitCost": 15.00 } ]
}
```

**Response 201 Created**
```json
{
  "success": true,
  "data": {
    "id": 8800, "returnNumber": "PRET-000015",
    "totalAmount": 34.80,
    "stockMovements": [ { "productId": 8801, "qty": -48, "balanceAfter": 1632 } ],
    "creditNote": { "amount": 34.80, "appliedToBalance": true }
  }
}
```

**Response 409** — مخزون غير كافٍ للإرجاع (بيع قبل الإرجاع).
```json
{ "success": false, "error": { "code": "INSUFFICIENT_STOCK", "message": "الكمية المطلوب إرجاعها غير متوفرة" } }
```

---

## 9) قواعد الأعمال (Business Rules)

1. **PO لا يؤثّر على المخزون** — الأثر يبدأ عند الاستلام (فاتورة/receive).
2. **الاستلام يزيد المخزون + يُحدِّث `AvgCost`** (المتوسط المرجّح) ضمن نفس المعاملة.
3. **الكمية تُحوَّل لوحدة الأساس** عبر `conversionFactor` قبل تحديث `Stock`.
4. **مرتجع الشراء يخفّض المخزون** ولا يتجاوز المستلَم، ويولّد Credit Note على رصيد المورّد.
5. **الذرّية:** فاتورة + بنود + حركات + رصيد + رصيد المورّد في Transaction واحد.
6. **الترقيم** عبر `sp_GetNextDocumentNumber` (DocType: `PURCHASE_INVOICE`, `PURCHASE_RETURN`).

---

## 10) رموز الحالة

| الرمز | الحالة |
|-------|--------|
| 201 | أمر/فاتورة/مرتجع أُنشئ |
| 200 | استلام/دفعة/تأكيد |
| 409 | مخزون غير كافٍ للإرجاع / تعارض تزامن |
| 422 | أمر مستلَم بالكامل / خرق قاعدة |
| 403 | صلاحية ناقصة |
