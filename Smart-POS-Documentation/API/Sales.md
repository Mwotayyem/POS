# API — Sales & POS (البيع ونقطة البيع)

> توثيق endpoints البيع ونقطة البيع في **Smart ERP POS**. تحت `/api/v1/sales` و`/api/v1/pos`. كل عملية بيع تُنفَّذ في **معاملة ذرّية** (فاتورة + بنود + دفعات + حركات مخزون + تحديث رصيد). يلتزم بـ [15-Sales-Invoices.md](../15-Sales-Invoices.md) و[18-POS.md](../18-POS.md).

---

## 1) الصلاحيات

| العملية | الصلاحية |
|---------|----------|
| إنشاء فاتورة / بيع | `sales.create` |
| قراءة | `sales.read` |
| مرتجع | `sales.return` |
| تعليق/استئناف | `pos.suspend` |
| فتح/إغلاق شفت | `pos.shift` |

---

## 2) POST /api/v1/sales/invoices — إنشاء فاتورة بيع (كاملة)

> الطريقة المفضّلة في POS: إرسال الفاتورة كاملة دفعة واحدة (ذرّية). ترقيم المستند عبر `sp_GetNextDocumentNumber`.

**Request**
```json
{
  "customerId": null,
  "storeId": 12,
  "shiftId": 3300,
  "channel": "POS",
  "items": [
    { "productId": 8801, "unitId": 1, "qty": 2, "unitPrice": 0.75, "discountAmount": 0.00 },
    { "productId": 8802, "unitId": 3, "qty": 1, "unitPrice": 16.00, "discountAmount": 1.00, "offerId": 45 }
  ],
  "payments": [
    { "method": "Cash", "amount": 17.50 }
  ]
}
```

**Response 201 Created**
```json
{
  "success": true,
  "data": {
    "id": 990012,
    "invoiceNumber": "INV-000123",
    "subTotal": 17.50, "discountAmount": 1.00,
    "taxAmount": 2.64, "total": 19.14,
    "paidAmount": 17.50, "changeAmount": 0.00,
    "status": "Completed",
    "createdDate": "2026-07-13T09:40:12Z",
    "stockMovements": [
      { "productId": 8801, "qty": -2, "balanceAfter": 478 },
      { "productId": 8802, "qty": -24, "balanceAfter": 96 }
    ]
  }
}
```

**Response 409 Conflict** — مخزون غير كافٍ.
```json
{ "success": false, "error": { "code": "INSUFFICIENT_STOCK", "message": "الكمية غير متوفرة", "details": [ { "productId": 8801, "requested": 2, "available": 1 } ] } }
```

---

## 3) POST /api/v1/sales/invoices/draft — إنشاء مسودّة (تراكمية)

> بديل تدريجي: إنشاء فاتورة مفتوحة ثم إضافة البنود واحداً تلو الآخر (شاشات لمس بطيئة الشبكة).

**Response 201 Created**
```json
{ "success": true, "data": { "id": 990013, "status": "Draft", "invoiceNumber": null } }
```

## POST /api/v1/sales/invoices/{id}/items — إضافة بند

**Request**
```json
{ "productId": 8801, "unitId": 1, "qty": 3, "unitPrice": 0.75 }
```

**Response 200 OK**
```json
{
  "success": true,
  "data": {
    "itemId": 5501, "lineTotal": 2.25,
    "invoiceTotals": { "subTotal": 2.25, "taxAmount": 0.36, "total": 2.61 }
  }
}
```

## DELETE /api/v1/sales/invoices/{id}/items/{itemId} — حذف بند من المسودّة
**Response 200 OK** — يعيد حساب الإجماليات.

---

## 4) POST /api/v1/sales/invoices/{id}/payments — إضافة دفعة

> يدعم الدفع المتعدّد الطرق (نقد + بطاقة) لفاتورة واحدة عبر `Payments`.

**Request**
```json
{ "method": "Card", "amount": 10.00, "reference": "AUTH-88213" }
```

**Response 200 OK**
```json
{
  "success": true,
  "data": {
    "paidAmount": 10.00, "remaining": 9.14, "changeAmount": 0.00,
    "isFullyPaid": false
  }
}
```

## POST /api/v1/sales/invoices/{id}/finalize — إتمام الفاتورة
> يُغلق المسودّة ذرّياً: يخصم المخزون، يولّد الرقم، يضبط `Status=Completed`. يفشل إن كان `paidAmount < total` للبيع النقدي.

---

## 5) POST /api/v1/pos/invoices/{id}/suspend — تعليق فاتورة

**Request**
```json
{ "shiftId": 3300, "label": "الطاولة 5" }
```

**Response 200 OK** — يُخزَّن snapshot في `SuspendedInvoices`.
```json
{ "success": true, "data": { "suspendedId": 7001, "label": "الطاولة 5", "suspendedAt": "2026-07-13T09:45:00Z" } }
```

## GET /api/v1/pos/suspended — قائمة الفواتير المعلّقة للشفت
**Response 200 OK**
```json
{ "success": true, "data": [ { "suspendedId": 7001, "label": "الطاولة 5", "itemCount": 4, "total": 12.30 } ] }
```

## POST /api/v1/pos/suspended/{id}/resume — استئناف
**Response 200 OK** — يعيد بناء الفاتورة من الـ snapshot ويحذف السجل المعلّق.
```json
{ "success": true, "data": { "invoiceId": 990014, "items": [ /* البنود المستعادة */ ] } }
```

---

## 6) POST /api/v1/sales/returns — مرتجع بيع

> يعكس حركة المخزون (يُرجع الكمية) ويولّد استرداداً. يُنفَّذ ذرّياً.

**Request**
```json
{
  "salesInvoiceId": 990012,
  "reason": "منتج تالف",
  "refundMethod": "Cash",
  "items": [
    { "salesInvoiceItemId": 5501, "qty": 1, "unitPrice": 0.75 }
  ]
}
```

**Response 201 Created**
```json
{
  "success": true,
  "data": {
    "id": 44001, "returnNumber": "RET-000045",
    "totalAmount": 0.87,
    "stockMovements": [ { "productId": 8801, "qty": 1, "balanceAfter": 479 } ],
    "refund": { "method": "Cash", "amount": 0.87 }
  }
}
```

**Response 422** — كمية المرتجع تتجاوز المُباع.
```json
{ "success": false, "error": { "code": "RETURN_EXCEEDS_SOLD", "message": "الكمية المرتجعة أكبر من المباعة" } }
```

---

## 7) POS Shift — الشفت

## POST /api/v1/pos/shifts/open
**Request**
```json
{ "storeId": 12, "openingCash": 100.00 }
```
**Response 201 Created**
```json
{ "success": true, "data": { "shiftId": 3300, "openedAt": "2026-07-13T08:00:00Z", "status": "Open" } }
```

## POST /api/v1/pos/shifts/{id}/close — إغلاق يومي (EOD)
**Request**
```json
{ "closingCash": 845.50 }
```
**Response 200 OK** — يحسب الفرق (Variance) بين المتوقّع والفعلي.
```json
{
  "success": true,
  "data": {
    "shiftId": 3300, "expectedCash": 848.00, "closingCash": 845.50,
    "variance": -2.50, "invoiceCount": 132, "totalSales": 748.00, "status": "Closed"
  }
}
```

## POST /api/v1/pos/shifts/{id}/cash-movement — حركة صندوق يدوية
**Request**
```json
{ "type": "Out", "amount": 20.00, "reason": "مصروف نثري" }
```

---

## 8) قواعد الأعمال (Business Rules)

1. **الذرّية:** الفاتورة + بنودها + دفعاتها + حركات المخزون + تحديث `Stock.QtyOnHand` في **Transaction واحد**. أي فشل يُلغي الكل.
2. **السعر والضريبة يُلتقطان لحظة البيع** (Snapshot) — تغييرها لاحقاً لا يؤثّر على الفاتورة.
3. **البيع لا يتم بلا شفت مفتوح** في قناة POS.
4. **`ChangeAmount`** يُحسب للبيع النقدي (`paidAmount - total`).
5. **المرتجع** لا يتجاوز الكمية المباعة، ويعكس المخزون والرصيد.
6. **العزل:** كل فاتورة تحمل `TenantId` و`StoreId` تلقائياً.

---

## 9) رموز الحالة والأداء

| الرمز | الحالة |
|-------|--------|
| 201 | فاتورة/مرتجع/شفت أُنشئ |
| 200 | تحديث/إضافة بند/دفعة |
| 409 | مخزون غير كافٍ / تعارض تزامن |
| 422 | خرق قاعدة أعمال (مرتجع يتجاوز، بيع بلا شفت) |

**الأداء:** إنشاء فاتورة POS مستهدف p95 < 200ms — يعتمد على فهارس البيع (انظر [Indexes.md](../Database/Indexes.md#5)) و RCSI لتقليل الأقفال.
