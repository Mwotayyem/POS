# Database — ERD (مخطط علاقات الكيانات)

> مخطط علاقات الكيانات (Entity Relationship Diagram) لنظام **Smart ERP POS**. يوضّح الكيانات الرئيسية وعلاقاتها و**الكاردناليتي** (Cardinality). كل جدول أعمال يرث الأعمدة المشتركة من [04-Database-Design.md](../04-Database-Design.md) ولا تُكرّر هنا.

---

## 1) رموز المخطط (Notation)

| الرمز | المعنى |
|-------|--------|
| `──1───N──` | علاقة واحد-إلى-متعدّد (One-to-Many) |
| `──N───M──` | علاقة متعدّد-إلى-متعدّد (عبر Junction Table) |
| `──1───1──` | علاقة واحد-إلى-واحد |
| `(PK)` | مفتاح أساسي |
| `(FK)` | مفتاح خارجي |
| `▲` | يشير إلى الجدول الأب (الطرف One) |

> كل FK يستخدم `ON DELETE NO ACTION` لأن الحذف **Soft Delete**.

---

## 2) القمّة — العزل متعدّد المستأجرين (Tenancy Core)

```
                         ┌────────────────┐
                         │    Tenants     │  (الجذر — يملك كل شيء)
                         │  Id (PK)       │
                         │  Name, Slug    │
                         │  BusinessType  │
                         └───────┬────────┘
                                 │ 1
                 ┌───────────────┼───────────────────────────┐
                 │ N             │ N                          │ N
          ┌──────▼──────┐ ┌──────▼──────┐            ┌────────▼────────┐
          │   Stores    │ │    Users    │            │ TenantSettings  │
          │  Id (PK)    │ │  Id (PK)    │            │  (1:1 مع Tenant)│
          │  TenantId FK│ │  TenantId FK│            └─────────────────┘
          └──────┬──────┘ └─────────────┘
                 │ 1
                 │ N
          ┌──────▼───────┐
          │ StoreSettings│  (1:1 مع Store)
          └──────────────┘
```

> **قاعدة حاكمة:** `TenantId` موجود في **كل** جدول أعمال أدناه (لا يُرسم على كل سهم تجنّباً للازدحام). العزل يتم عبر EF Core Global Query Filter.

---

## 3) الهوية والصلاحيات (Identity & Access — RBAC)

```
   ┌─────────┐   N        M   ┌─────────┐   N        M   ┌──────────────┐
   │  Users  │────UserRoles───│  Roles  │─RolePermissions│ Permissions  │
   │ Id (PK) │                │ Id (PK) │                │ Id (PK)      │
   └────┬────┘                └─────────┘                └──────────────┘
        │ 1
        │ N
   ┌────▼──────────┐
   │ RefreshTokens │   (Token, ExpiresAt, RevokedAt)
   └───────────────┘

   العلاقات N:M:
   • Users  ──< UserRoles >──  Roles          (مستخدم له عدّة أدوار)
   • Roles  ──< RolePermissions >── Permissions (دور له عدّة صلاحيات)
```

---

## 4) الكتالوج (Catalog)

```
 ┌────────────┐ 1     N ┌──────────────┐ N     1 ┌────────────┐
 │ Categories │─────────│   Products   │─────────│   Brands   │
 │ Id (PK)    │         │  Id (PK)     │         │  Id (PK)   │
 │ ParentId FK│◄─┐ self │  CategoryId  │         └────────────┘
 └────────────┘  │ tree │  BrandId FK  │
       (شجرة)  ──┘      │  BaseUnitId  │──────────┐ N
                        └──────┬───────┘          │
                     1 │       │ 1         1 ┌─────▼──────┐
          ┌────────────▼──┐ ┌──▼────────────┐│   Units    │
          │ ProductBarcodes│ │ ProductUnits ││  Id (PK)   │
          │ (Barcode uniq) │ │ (Unit+Factor)│└────────────┘
          └────────────────┘ └──────────────┘
                        │ 1
                        │ N
                ┌───────▼─────────┐   ┌──────────────────┐  ┌─────────────────────┐
                │ ProductVariants │   │  ProductPrices   │  │ ProductPriceHistory │
                │ (Size/Color/SKU)│   │ (السعر الحالي/   │  │ (سجل تغيّر الأسعار   │
                └─────────────────┘   │  نوع: Retail/VIP)│  │  Old→New عبر الزمن)  │
                                      └──────────────────┘  └─────────────────────┘
                                            ▲ N                     ▲ N
                                            └──────── Product 1 ─────┘

 الكاردناليتي:
 • Category  1───N  Products          • Category self 1───N (شجرة تصنيفات)
 • Brand     1───N  Products          • Product 1───N ProductBarcodes
 • Product   1───N  ProductUnits      • Product 1───N ProductVariants
 • Unit      1───N  ProductUnits      • Product 1───N ProductPrices (السعر الحالي/نوع)
 • Product   1───N  ProductPriceHistory (سجل التغييرات)
```

---

## 5) الشركاء (Partners)

```
 ┌────────────┐ 1        N ┌──────────────────┐
 │  Suppliers │────────────│ SupplierPayments │
 │ Id (PK)    │            └──────────────────┘
 └────────────┘

 ┌────────────┐ 1        N ┌──────────────────┐
 │  Customers │────────────│ CustomerPayments │
 │ Id (PK)    │            └──────────────────┘
 │ CreditLimit│
 │ LoyaltyPts │
 └────────────┘
```

---

## 6) المخزون (Inventory) — القلب التشغيلي

```
 ┌────────────┐ 1      N ┌──────────────────┐ N      1 ┌────────────┐
 │ Warehouses │──────────│      Stock       │──────────│  Products  │
 │ Id (PK)    │          │  Id (PK)         │          │  Id (PK)   │
 │ StoreId FK │          │  WarehouseId FK  │          └────────────┘
 └────────────┘          │  ProductId FK    │
       │ 1               │  QtyOnHand       │
       │                 │  QtyReserved     │
       │ N               └────────┬─────────┘
 ┌─────▼──────────┐               │ 1
 │ StockTransfers │               │ N
 │ (From/To WH)   │        ┌──────▼─────────┐
 └────────────────┘        │ StockMovements │  (كل حركة: In/Out/Adjust/Transfer)
                           │  RefDocType    │  ← يربط بالفاتورة المصدر
                           │  RefDocId      │
                           └────────────────┘
        ┌──────────────────┐
        │ StockAdjustments │  (تسويات الجرد — يولّد StockMovements)
        └──────────────────┘

 الكاردناليتي:
 • Warehouse 1───N Stock       • Product 1───N Stock (رصيد المنتج في كل مستودع)
 • Stock     1───N StockMovements (سجل حركات كل رصيد)
 • Warehouse 1───N StockTransfers (كمصدر أو وجهة)
```

---

## 7) المشتريات (Purchasing)

```
 ┌────────────┐ 1    N ┌────────────────┐ 1    N ┌────────────────────┐
 │ Suppliers  │────────│ PurchaseOrders │────────│  PurchaseInvoices  │
 │ Id (PK)    │        │ Id (PK)        │        │  Id (PK)           │
 └────────────┘        │ SupplierId FK  │        │  PurchaseOrderId FK │
                       └────────────────┘        │  SupplierId FK      │
                                                 └─────────┬──────────┘
                                                           │ 1
                                          ┌────────────────┼──────────────┐
                                          │ N                             │ N
                             ┌────────────▼─────────────┐   ┌─────────────▼────────┐
                             │  PurchaseInvoiceItems    │   │   PurchaseReturns    │
                             │  ProductId FK            │   │   (مرتجع الشراء)     │
                             │  Qty, UnitCost           │   └──────────────────────┘
                             └──────────────────────────┘

 الكاردناليتي:
 • Supplier        1───N PurchaseOrders / PurchaseInvoices
 • PurchaseOrder   1───N PurchaseInvoices (طلب قد يُفوتر جزئياً)
 • PurchaseInvoice 1───N PurchaseInvoiceItems
 • PurchaseInvoice 1───N PurchaseReturns
```

---

## 8) المبيعات (Sales)

```
 ┌────────────┐ 1    N ┌─────────────────┐ 1        N ┌────────────────────┐
 │ Customers  │────────│  SalesInvoices  │────────────│  SalesInvoiceItems │
 │ Id (PK)    │        │  Id (PK)        │            │  ProductId FK      │
 └────────────┘        │  CustomerId FK  │            │  Qty, UnitPrice    │
                       │  StoreId FK     │            └────────────────────┘
                       └───────┬─────────┘
              ┌────────────────┼────────────────┐
              │ 1              │ 1              │ 1
      ┌───────▼──────┐  ┌──────▼───────┐  ┌─────▼──────────┐
      │   Payments   │  │ SalesReturns │  │ (خصم المخزون) │
      │ (نقد/بطاقة/  │  │  Id (PK)     │──┤ StockMovements │
      │  آجل/محفظة)  │  └──────┬───────┘  └────────────────┘
      └──────────────┘         │ 1
                               │ N
                      ┌────────▼─────────┐
                      │ SalesReturnItems │
                      └──────────────────┘

 الكاردناليتي:
 • Customer      1───N SalesInvoices
 • SalesInvoice  1───N SalesInvoiceItems     • SalesInvoice 1───N Payments
 • SalesInvoice  1───N SalesReturns          • SalesReturn  1───N SalesReturnItems
```

---

## 9) نقطة البيع (POS)

```
 ┌─────────┐ 1   N ┌───────────┐ 1   N ┌──────────────┐
 │  Users  │───────│ PosShifts │───────│ PosSessions  │
 │ (كاشير)│       │ Open/Close│       │ (جلسة عمل)  │
 └─────────┘       └─────┬─────┘       └──────────────┘
                         │ 1
              ┌──────────┼────────────┐
              │ N                     │ N
   ┌──────────▼──────────┐  ┌─────────▼──────────┐
   │ CashDrawerMovements │  │ SuspendedInvoices  │
   │ (In/Out نقدية)      │  │ (فواتير معلّقة)   │
   └─────────────────────┘  └────────────────────┘

 الكاردناليتي:
 • User  1───N PosShifts            • PosShift 1───N PosSessions
 • PosShift 1───N CashDrawerMovements / SuspendedInvoices
```

---

## 10) العروض (Promotions)

```
 ┌────────────┐ 1    N ┌────────────┐ N     M ┌────────────┐
 │   Offers   │────────│ OfferRules │         │  Products  │
 │  Id (PK)   │        └────────────┘         └─────┬──────┘
 │  Type      │ 1                                    │
 │  StartDate │────────────< OfferProducts >─────────┘  N:M
 │  EndDate   │                                       (منتجات العرض)
 └─────┬──────┘
       │ 1
       │ N
 ┌─────▼──────┐
 │ OfferUsage │  (تتبّع استخدام العرض لكل فاتورة/عميل)
 └────────────┘

 الكاردناليتي:
 • Offer 1───N OfferRules     • Offer N───M Products (عبر OfferProducts)
 • Offer 1───N OfferUsage
```

---

## 11) العمليات (Ops / Cross-Cutting)

```
 ┌───────────────┐   ┌────────────┐   ┌────────────┐   ┌───────────┐
 │ Notifications │   │  AuditLogs │   │  Settings  │   │ Sequences │
 │ (SignalR)     │   │ (تدقيق)   │   │ (JSON)     │   │ (ترقيم)  │
 └───────────────┘   └────────────┘   └────────────┘   └───────────┘
   كلّها ترتبط بـ Tenant (وغالباً User) لكنها لا تدخل في العلاقات المحاسبية.
```

---

## 12) نظرة كلّية على العلاقات الرئيسية (Master Relationship Map)

```
 Tenant ──1:N── Store ──1:N── Warehouse ──1:N── Stock ──1:N── StockMovements
   │                                              ▲
   │                                              │ (RefDoc)
   ├──1:N── Product ──1:N── Stock ────────────────┘
   ├──1:N── Supplier ──1:N── PurchaseInvoice ──1:N── PurchaseInvoiceItems
   ├──1:N── Customer ──1:N── SalesInvoice   ──1:N── SalesInvoiceItems
   │                              │
   │                              ├──1:N── Payments
   │                              └──1:N── SalesReturns ──1:N── SalesReturnItems
   ├──1:N── Offer ──N:M── Product (OfferProducts)
   └──1:N── User ──N:M── Role ──N:M── Permission
```

---

## 13) ملاحظات الأمانة المرجعية (Referential Integrity Notes)

1. **كل FK** يشير إلى الجدول الأب بـ `ON DELETE NO ACTION` — لا حذف تسلسلي (Cascade) لأن الحذف soft.
2. **`StockMovements`** لا يُحذف أبداً (سجل محاسبي غير قابل للتعديل — Append Only).
3. حركة المخزون تُربط بمصدرها عبر `(RefDocType, RefDocId)` بدل FK صريح — لأن المصدر قد يكون فاتورة بيع أو شراء أو تسوية أو تحويل.
4. علاقات N:M **دائماً** عبر Junction Table صريح يحمل الأعمدة المشتركة (لا Skip Navigation بلا junction).

التفاصيل الكاملة لكل FK في [Relationships.md](Relationships.md).
