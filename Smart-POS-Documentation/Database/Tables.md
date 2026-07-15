# Database — Tables (فهرس الجداول الشامل)

> فهرس كامل لكل جداول **Smart ERP POS** مجمّعة حسب الوحدة. لكل جدول: وصف مختصر + **الأعمدة الخاصة به** (غير المشتركة). الأعمدة المشتركة (`Id, TenantId, StoreId, CreatedDate, CreatedBy, ModifiedDate, ModifiedBy, DeletedDate, DeletedBy, IsDeleted, ConcurrencyStamp`) موروثة من [04-Database-Design.md](../04-Database-Design.md) ولا تُكرَّر.

---

## 1) Tenancy — التعدّدية

| الجدول | الوصف | أهم الأعمدة الخاصة |
|--------|-------|--------------------|
| `Tenants` | المستأجر (الشركة). الجذر الذي يملك كل البيانات. | `Name`, `Slug` (فريد), `BusinessType`, `Status`, `SubscriptionPlan`, `PublicId` |
| `Stores` | فرع/متجر تابع للمستأجر. | `Name`, `Code`, `Address`, `Phone`, `TimeZone`, `IsActive` |
| `TenantSettings` | إعدادات المستأجر (شعار/ألوان/عملة/ضريبة). 1:1 مع Tenant. | `LogoUrl`, `PrimaryColor`, `CurrencyCode`, `DefaultTaxRate`, `Settings` (JSON) |
| `StoreSettings` | إعدادات خاصة بالفرع تتجاوز إعداد المستأجر. | `ReceiptTemplate` (JSON), `PrinterConfig` (JSON), `Settings` (JSON) |

> **ملاحظة:** `Tenants` نفسه لا يحمل `TenantId` (هو الجذر)، ويستخدم `PublicId UNIQUEIDENTIFIER` للكشف الخارجي.

---

## 2) Identity — الهوية والصلاحيات

| الجدول | الوصف | أهم الأعمدة الخاصة |
|--------|-------|--------------------|
| `Users` | مستخدمو النظام (مالك/مدير/كاشير/محاسب). | `Email` (فريد/مستأجر), `PasswordHash`, `FullName`, `Phone`, `IsActive`, `LastLoginAt`, `EmailConfirmed` |
| `Roles` | الأدوار (RBAC). | `Name`, `NormalizedName`, `IsSystemRole` |
| `Permissions` | الصلاحيات الدقيقة (مثل `products.create`). | `Code` (فريد), `Module`, `DisplayName` |
| `RolePermissions` | **Junction** Roles↔Permissions (N:M). | `RoleId` (FK), `PermissionId` (FK) |
| `UserRoles` | **Junction** Users↔Roles (N:M). | `UserId` (FK), `RoleId` (FK) |
| `RefreshTokens` | رموز التحديث لـ JWT. | `Token` (فريد), `ExpiresAt`, `RevokedAt`, `ReplacedByToken`, `UserId` (FK) |

---

## 3) Catalog — الكتالوج

| الجدول | الوصف | أهم الأعمدة الخاصة |
|--------|-------|--------------------|
| `Products` | المنتج الرئيسي. | `Name`, `Sku`, `CategoryId` (FK), `BrandId` (FK), `BaseUnitId` (FK), `CostPrice`, `DefaultSellPrice`, `TaxId` (FK), `TrackInventory` (BIT), `ReorderQuantity` |
| `ProductBarcodes` | باركود متعدّد لكل منتج (يدعم وحدات مختلفة). | `Barcode` (فريد/مستأجر), `ProductId` (FK), `UnitId` (FK), `IsPrimary` |
| `ProductUnits` | وحدات القياس ومعاملات التحويل للمنتج. | `ProductId` (FK), `UnitId` (FK), `ConversionFactor` `DECIMAL(18,6)`, `Barcode`, `SellingPrice` |
| `ProductVariants` | متغيّرات (مقاس/لون/SKU). | `ProductId` (FK), `Attributes` (JSON), `VariantSku`, `SellingPrice` |
| `ProductPrices` | **السعر الحالي** لكل نوع سعر (Retail/Wholesale/VIP...). | `ProductId` (FK), `PriceType`, `Price`, `IsDefault`, `StoreId` (سعر بفرع) |
| `ProductPriceHistory` | **سجل تغييرات الأسعار** عبر الزمن (تدقيق/تقارير). | `ProductId` (FK), `PriceType`, `OldPrice`, `NewPrice`, `ChangedByUserId`, `ChangedAt`, `Reason` |
| `Categories` | تصنيفات شجرية (self-reference). | `Name`, `ParentId` (FK self), `Path`, `Level`, `SortOrder` |
| `Brands` | العلامات التجارية. | `Name`, `LogoUrl` |
| `Units` | وحدات القياس المرجعية (قطعة/كرتونة/كجم). | `Name`, `Symbol`, `Precision` |

---

## 4) Partners — الشركاء

| الجدول | الوصف | أهم الأعمدة الخاصة |
|--------|-------|--------------------|
| `Suppliers` | الموردون. | `Name`, `Code`, `Phone`, `Email`, `TaxNumber`, `OpeningBalance`, `CurrentBalance` |
| `Customers` | العملاء. | `Name`, `Phone`, `Email`, `CreditLimit` `DECIMAL(18,4)`, `LoyaltyPoints`, `CurrentBalance`, `CustomerType` |
| `SupplierPayments` | دفعات للموردين (AP). | `SupplierId` (FK), `Amount`, `PaymentMethod`, `RefDocId`, `PaidAt` |
| `CustomerPayments` | دفعات من العملاء (AR). | `CustomerId` (FK), `Amount`, `PaymentMethod`, `RefDocId`, `PaidAt` |

---

## 5) Inventory — المخزون

| الجدول | الوصف | أهم الأعمدة الخاصة |
|--------|-------|--------------------|
| `Warehouses` | المستودعات (يرتبط كل واحد بفرع). | `Name`, `Code`, `Type` (Main/Shelf/Damaged), `IsDefault` |
| `Stock` | رصيد المنتج في مستودع (صف لكل Product×Warehouse). | `ProductId` (FK), `WarehouseId` (FK), `QtyOnHand` `DECIMAL(18,4)`, `QtyReserved`, `AvgCost` |
| `StockMovements` | سجل حركات المخزون (Append-Only، لا حذف). | `StockId` (FK), `MovementType` (In/Out/Adjust/Transfer), `Qty`, `RefDocType`, `RefDocId`, `BalanceAfter` |
| `StockAdjustments` | تسويات الجرد (تولّد Movements). | `WarehouseId` (FK), `Reason`, `Status`, `TotalVariance` |
| `StockTransfers` | تحويل بين المستودعات/الفروع. | `FromWarehouseId` (FK), `ToWarehouseId` (FK), `Status`, `TransferNumber` |

---

## 6) Purchasing — المشتريات

| الجدول | الوصف | أهم الأعمدة الخاصة |
|--------|-------|--------------------|
| `PurchaseOrders` | أمر شراء (قبل الاستلام). | `SupplierId` (FK), `OrderNumber`, `Status` (Draft/Confirmed/Received), `ExpectedDate`, `TotalAmount` |
| `PurchaseInvoices` | فاتورة شراء (الاستلام الفعلي). | `SupplierId` (FK), `PurchaseOrderId` (FK), `InvoiceNumber`, `SubTotal`, `TaxAmount`, `Total`, `PaidAmount`, `Status` |
| `PurchaseInvoiceItems` | بنود فاتورة الشراء. | `PurchaseInvoiceId` (FK), `ProductId` (FK), `Qty`, `UnitCost`, `TaxRate`, `LineTotal`, `BatchNo`, `ExpiryDate` |
| `PurchaseReturns` | مرتجعات الشراء. | `PurchaseInvoiceId` (FK), `ReturnNumber`, `Reason`, `TotalAmount`, `Status` |

---

## 7) Sales — المبيعات

| الجدول | الوصف | أهم الأعمدة الخاصة |
|--------|-------|--------------------|
| `SalesInvoices` | فاتورة البيع (POS أو Back-office). | `CustomerId` (FK), `InvoiceNumber`, `SubTotal`, `DiscountAmount`, `TaxAmount`, `Total`, `PaidAmount`, `ChangeAmount`, `Status`, `Channel` (POS/Web) |
| `SalesInvoiceItems` | بنود فاتورة البيع. | `SalesInvoiceId` (FK), `ProductId` (FK), `Qty`, `UnitPrice`, `DiscountAmount`, `TaxRate`, `LineTotal`, `OfferId` (FK) |
| `SalesReturns` | مرتجعات البيع. | `SalesInvoiceId` (FK), `ReturnNumber`, `Reason`, `RefundMethod`, `TotalAmount`, `Status` |
| `SalesReturnItems` | بنود مرتجع البيع. | `SalesReturnId` (FK), `SalesInvoiceItemId` (FK), `Qty`, `UnitPrice`, `LineTotal` |
| `Payments` | دفعات الفاتورة (متعدّدة الطرق لفاتورة واحدة). | `SalesInvoiceId` (FK), `Method` (Cash/Card/Credit/Wallet), `Amount`, `Reference`, `PaidAt` |

---

## 8) POS — نقطة البيع

| الجدول | الوصف | أهم الأعمدة الخاصة |
|--------|-------|--------------------|
| `PosShifts` | شفت الكاشير (فتح/إغلاق يومي). | `UserId` (FK), `OpenedAt`, `ClosedAt`, `OpeningCash`, `ClosingCash`, `ExpectedCash`, `Variance`, `Status` |
| `PosSessions` | جلسة تسجيل دخول على جهاز POS. | `ShiftId` (FK), `TerminalId`, `StartedAt`, `EndedAt`, `IpAddress` |
| `CashDrawerMovements` | حركات نقدية الصندوق (Cash In/Out). | `ShiftId` (FK), `Type` (In/Out/Sale/Refund), `Amount`, `Reason` |
| `SuspendedInvoices` | فواتير معلّقة (Park/Hold). | `ShiftId` (FK), `SnapshotJson` (JSON), `SuspendedAt`, `Label` |

---

## 9) Promotions — العروض

| الجدول | الوصف | أهم الأعمدة الخاصة |
|--------|-------|--------------------|
| `Offers` | العرض الترويجي. | `Name`, `Type` (Percentage/Fixed/BuyXGetY/Bundle), `Value`, `StartDate`, `EndDate`, `Priority`, `IsActive` |
| `OfferRules` | شروط تفعيل العرض. | `OfferId` (FK), `RuleType` (MinQty/MinAmount/CustomerGroup), `Operator`, `Value` |
| `OfferProducts` | **Junction** Offers↔Products/Categories (N:M) + ضبط تعارض العروض. | `OfferId` (FK), `ProductId`/`CategoryId` (FK), `Role`, `Priority`, `MaxDiscount`, `MaxQty` |
| `OfferUsage` | تتبّع استخدام العرض. | `OfferId` (FK), `SalesInvoiceId` (FK), `CustomerId` (FK), `DiscountGiven`, `UsedAt` |

---

## 10) Ops — العمليات المشتركة

| الجدول | الوصف | أهم الأعمدة الخاصة |
|--------|-------|--------------------|
| `Notifications` | إشعارات لحظية (SignalR). | `UserId` (FK), `Title`, `Body`, `Type`, `IsRead`, `Link` |
| `AuditLogs` | سجل التدقيق (من/ماذا/متى). | `UserId` (FK), `EntityName`, `EntityId`, `Action` (Create/Update/Delete), `OldValues` (JSON), `NewValues` (JSON), `IpAddress` |
| `Settings` | إعدادات مفتاح/قيمة عامة. | `Key`, `Value` (JSON), `Scope` (Tenant/Store/User), `DataType` |
| `Sequences` | ترقيم المستندات الذرّي (لكل مستأجر/فرع/نوع). | `DocType`, `Prefix`, `NextValue`, `Padding` — انظر [04-Database-Design.md](../04-Database-Design.md#10) |

> `AuditLogs` قد يُستثنى من Soft Delete (Append-Only محض) — يُبرّر في [27-Security.md](../27-Security.md).

---

## 11) الجداول المرجعية العالمية (Reference Data — بلا TenantId)

| الجدول | الوصف |
|--------|-------|
| `Countries` | الدول (ISO). بلا `TenantId` — مشتركة عالمياً. |
| `Currencies` | العملات (ISO 4217) وأسعار الصرف المرجعية. |
| `TaxTypes` | أنواع الضرائب المرجعية (VAT/Excise). |

---

## 12) الجدول الملخّص (Summary Matrix)

| الوحدة | عدد الجداول | جداول Junction (N:M) | جداول Append-Only |
|--------|:-----------:|:--------------------:|:-----------------:|
| Tenancy | 4 | — | — |
| Identity | 6 | `RolePermissions`, `UserRoles` | — |
| Catalog | 9 | — | `ProductPriceHistory` |
| Partners | 4 | — | — |
| Inventory | 5 | — | `StockMovements` |
| Purchasing | 4 | — | — |
| Sales | 5 | — | — |
| POS | 4 | — | `CashDrawerMovements` |
| Promotions | 4 | `OfferProducts` | `OfferUsage` |
| Ops | 4 | — | `AuditLogs` |
| Reference | 3 | — | — |
| Platform (Subscriptions) | 2 | — | — |
| **الإجمالي** | **54** | **3** | **5** |

> كل الجداول (عدا Reference و Sequences) تلتزم بالأعمدة المشتركة والـ Soft Delete وفهرس العزل `(TenantId, StoreId)`. تفاصيل الفهارس في [Indexes.md](Indexes.md) والعلاقات في [Relationships.md](Relationships.md).
