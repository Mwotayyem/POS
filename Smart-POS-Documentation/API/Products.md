# API — Products (المنتجات)

> توثيق endpoints المنتجات في **Smart ERP POS**. كل الطلبات تحت `/api/v1/products` وتتطلّب `Authorization: Bearer` و`X-Tenant-Slug`. العزل تلقائي بـ `TenantId` عبر Global Query Filter. يلتزم بـ [08-Products.md](../08-Products.md).

---

## 1) الصلاحيات (Permissions)

| العملية | الصلاحية المطلوبة |
|---------|-------------------|
| قراءة/بحث | `products.read` |
| إنشاء | `products.create` |
| تعديل | `products.update` |
| حذف (soft) | `products.delete` |
| رفع جماعي | `products.bulk` |

---

## 2) GET /api/v1/products — قائمة مع Pagination

**Query Params:** `page` (افتراضي 1), `pageSize` (افتراضي 25، أقصى 100), `sort` (مثل `name,-createdDate`), `categoryId`, `brandId`, `isActive`.

**Response 200 OK**
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 8801, "name": "Coca-Cola 330ml", "sku": "CC-330",
        "categoryId": 12, "categoryName": "Beverages",
        "brandId": 4, "brandName": "Coca-Cola",
        "baseUnitId": 1, "baseUnitName": "Piece",
        "costPrice": 0.35, "sellPrice": 0.75, "taxRate": 16.0,
        "trackStock": true, "reorderLevel": 24,
        "primaryBarcode": "5449000000996", "isActive": true
      }
    ],
    "pagination": { "page": 1, "pageSize": 25, "totalItems": 1342, "totalPages": 54 }
  }
}
```

---

## 3) GET /api/v1/products/{id} — تفاصيل منتج

**Response 200 OK** — يشمل الوحدات والباركود والمتغيّرات.
```json
{
  "success": true,
  "data": {
    "id": 8801, "name": "Coca-Cola 330ml", "sku": "CC-330",
    "costPrice": 0.35, "sellPrice": 0.75, "taxRate": 16.0,
    "barcodes": [
      { "id": 501, "barcode": "5449000000996", "unitId": 1, "isPrimary": true },
      { "id": 502, "barcode": "5449000011114", "unitId": 3, "isPrimary": false }
    ],
    "units": [
      { "unitId": 1, "unitName": "Piece",   "conversionFactor": 1,  "price": 0.75 },
      { "unitId": 3, "unitName": "Carton24", "conversionFactor": 24, "price": 16.50 }
    ],
    "variants": [],
    "stock": [ { "warehouseId": 7, "warehouseName": "Main", "qtyOnHand": 480 } ]
  }
}
```

**Response 404**
```json
{ "success": false, "error": { "code": "NOT_FOUND", "message": "المنتج غير موجود" } }
```

---

## 4) POST /api/v1/products — إنشاء منتج

**Request**
```json
{
  "name": "Pepsi 330ml",
  "sku": "PP-330",
  "categoryId": 12,
  "brandId": 5,
  "baseUnitId": 1,
  "costPrice": 0.34,
  "sellPrice": 0.72,
  "taxRate": 16.0,
  "trackStock": true,
  "reorderLevel": 24,
  "barcodes": [ { "barcode": "5449000054227", "unitId": 1, "isPrimary": true } ],
  "units": [ { "unitId": 3, "conversionFactor": 24, "price": 16.00 } ]
}
```

**Response 201 Created**
```json
{ "success": true, "data": { "id": 8802, "name": "Pepsi 330ml", "sku": "PP-330" } }
```

**Response 409 Conflict** — باركود أو SKU مكرّر داخل المستأجر.
```json
{ "success": false, "error": { "code": "DUPLICATE_BARCODE", "message": "الباركود مستخدم مسبقاً" } }
```

---

## 5) PUT /api/v1/products/{id} — تعديل منتج

**Request**
```json
{ "name": "Pepsi 330ml Can", "sellPrice": 0.75, "reorderLevel": 36, "isActive": true }
```

**Response 200 OK**
```json
{ "success": true, "data": { "id": 8802, "modifiedDate": "2026-07-13T09:14:00Z" } }
```

**Response 409** — تعارض تزامن (ConcurrencyStamp قديم).
```json
{ "success": false, "error": { "code": "CONCURRENCY_CONFLICT", "message": "تم تعديل السجل من مستخدم آخر" } }
```

---

## 6) DELETE /api/v1/products/{id} — حذف soft

**Response 200 OK** — يضبط `IsDeleted=1`, `DeletedBy`, `DeletedDate`. لا حذف فعلي.
```json
{ "success": true, "data": { "id": 8802, "isDeleted": true } }
```

**Response 409** — المنتج مرتبط بحركات نشطة.
```json
{ "success": false, "error": { "code": "HAS_DEPENDENCIES", "message": "لا يمكن حذف منتج له حركات مخزون أو فواتير" } }
```

---

## 7) GET /api/v1/products/search — بحث سريع (POS)

**Query:** `q` (اسم/SKU/باركود جزئي), `limit` (افتراضي 20).

**Response 200 OK** — نتائج خفيفة لشاشة البيع (يستخدم فهرس `IX_Products_Tenant_Name`).
```json
{
  "success": true,
  "data": [
    { "id": 8801, "name": "Coca-Cola 330ml", "sku": "CC-330", "sellPrice": 0.75, "qtyOnHand": 480 },
    { "id": 8802, "name": "Pepsi 330ml Can", "sku": "PP-330", "sellPrice": 0.75, "qtyOnHand": 120 }
  ]
}
```

---

## 8) GET /api/v1/products/barcode/{barcode} — بحث بالباركود

> الاستدعاء الأكثر تكراراً في POS. يستخدم `UX_ProductBarcodes_Tenant_Barcode`. زمن استجابة مستهدف < 50ms.

**Response 200 OK**
```json
{
  "success": true,
  "data": {
    "productId": 8801, "name": "Coca-Cola 330ml",
    "unitId": 1, "unitName": "Piece", "conversionFactor": 1,
    "sellPrice": 0.75, "taxRate": 16.0, "qtyOnHand": 480
  }
}
```

**Response 404**
```json
{ "success": false, "error": { "code": "BARCODE_NOT_FOUND", "message": "باركود غير معروف" } }
```

---

## 9) POST /api/v1/products/bulk — رفع جماعي

> رفع/تحديث دفعة منتجات (استيراد Excel/CSV محوّل JSON). يُعالَج داخل معاملة واحدة، ويعيد ملخّص النتائج.

**Request**
```json
{
  "mode": "upsert",
  "items": [
    { "sku": "CC-330", "name": "Coca-Cola 330ml", "sellPrice": 0.75, "categoryId": 12 },
    { "sku": "PP-500", "name": "Pepsi 500ml", "sellPrice": 1.10, "categoryId": 12 }
  ]
}
```

**Response 200 OK**
```json
{
  "success": true,
  "data": {
    "created": 1, "updated": 1, "failed": 0,
    "errors": []
  }
}
```

**Response 207 Multi-Status** — نجاح جزئي.
```json
{
  "success": true,
  "data": {
    "created": 1, "updated": 0, "failed": 1,
    "errors": [ { "row": 2, "sku": "PP-500", "code": "INVALID_CATEGORY", "message": "التصنيف غير موجود" } ]
  }
}
```

---

## 10) قواعد الأعمال (Business Rules)

1. `Barcode` و`Sku` فريدان **داخل المستأجر** فقط (`WHERE IsDeleted=0`).
2. `TrackStock=false` للخدمات — لا تُنشأ صفوف `Stock`.
3. تغيير `SellPrice` لا يؤثّر على الفواتير السابقة (السعر يُلتقط عند البيع — Snapshot).
4. الحذف soft فقط؛ منتج له حركات لا يُحذف بل يُعطَّل (`isActive=false`).
5. كل استجابة مغلّفة بـ `{ success, data }` أو `{ success, error }` (تنسيق موحّد).

---

## 11) الأداء (Performance)

- `search` و`barcode` يستخدمان فهارس Covering — بلا Key Lookups.
- Pagination إجباري (`pageSize` ≤ 100) — لا استرجاع كامل الجدول.
- `bulk` عبر EF Core Bulk أو TVP للدفعات الكبيرة (> 500 صف).
- نتائج البحث قابلة للتخزين المؤقت (Redis) قصير الأمد للكتالوجات الثابتة.
