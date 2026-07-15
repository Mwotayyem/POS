# Database — Relationships (العلاقات والمفاتيح الخارجية)

> توثيق كامل لكل علاقات المفاتيح الخارجية (Foreign Keys) في **Smart ERP POS**. كل علاقة تلتزم بقاعدة الحذف `ON DELETE NO ACTION` لأن النظام يعتمد **Soft Delete** حصراً (انظر [04-Database-Design.md](../04-Database-Design.md#4)).

---

## 1) القاعدة الحاكمة لقواعد الحذف (Delete Rules)

| القاعدة | القرار | السبب |
|---------|--------|-------|
| `ON DELETE` | **NO ACTION** لكل FK | لا حذف فعلي — الحذف soft عبر `IsDeleted=1` |
| `ON UPDATE` | **NO ACTION** | المفاتيح `IDENTITY` لا تتغيّر |
| Cascade | **ممنوع** | يمنع الحذف التسلسلي للبيانات المحاسبية |
| السلامة المرجعية عند Soft Delete | تُدار في طبقة التطبيق (لا تُحذف أب له أبناء نشطون) | EF Core + Domain Rules |

```sql
-- النمط الموحّد لكل FK
CONSTRAINT [FK_{Child}_{Parent}]
    FOREIGN KEY ([{Parent}Id]) REFERENCES [dbo].[{Parent}]([Id])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
```

---

## 2) علاقات العزل (Tenancy FKs) — على كل جدول

| الجدول الابن | العمود | يشير إلى | النوع |
|--------------|--------|----------|-------|
| *كل جدول أعمال* | `TenantId` | `Tenants(Id)` | N:1 (إجباري) |
| *كل جدول أعمال* | `StoreId` | `Stores(Id)` | N:1 (اختياري/NULL) |
| *كل جدول أعمال* | `CreatedBy` / `ModifiedBy` / `DeletedBy` | `Users(Id)` | N:1 (تدقيق) |

> هذه العلاقات موروثة من القالب القياسي ولا تُكرّر في الجداول أدناه.

---

## 3) Identity — العلاقات

| العلاقة | النوع | Delete Rule |
|---------|-------|-------------|
| `UserRoles.UserId → Users.Id` | N:1 | NO ACTION |
| `UserRoles.RoleId → Roles.Id` | N:1 | NO ACTION |
| `RolePermissions.RoleId → Roles.Id` | N:1 | NO ACTION |
| `RolePermissions.PermissionId → Permissions.Id` | N:1 | NO ACTION |
| `RefreshTokens.UserId → Users.Id` | N:1 | NO ACTION |

**العلاقات المركّبة (N:M):**
- **Users ↔ Roles** عبر `UserRoles` — مستخدم له عدّة أدوار، والدور يُسنَد لعدّة مستخدمين. مفتاح فريد مركّب `UNIQUE(TenantId, UserId, RoleId)`.
- **Roles ↔ Permissions** عبر `RolePermissions` — مفتاح فريد `UNIQUE(TenantId, RoleId, PermissionId)`.

---

## 4) Catalog — العلاقات

| العلاقة | النوع | Delete Rule |
|---------|-------|-------------|
| `Products.CategoryId → Categories.Id` | N:1 | NO ACTION |
| `Products.BrandId → Brands.Id` | N:1 (NULL) | NO ACTION |
| `Products.BaseUnitId → Units.Id` | N:1 | NO ACTION |
| `Categories.ParentId → Categories.Id` | N:1 (**self**, NULL) | NO ACTION |
| `ProductBarcodes.ProductId → Products.Id` | N:1 | NO ACTION |
| `ProductBarcodes.UnitId → Units.Id` | N:1 | NO ACTION |
| `ProductUnits.ProductId → Products.Id` | N:1 | NO ACTION |
| `ProductUnits.UnitId → Units.Id` | N:1 | NO ACTION |
| `ProductVariants.ProductId → Products.Id` | N:1 | NO ACTION |

**علاقة الشجرة (Self-Reference):** `Categories.ParentId` يشير إلى `Categories.Id`. الجذر يحمل `ParentId = NULL`. تُدار العمق عبر `Level` و`Path` لتجنّب استعلامات تكرارية مكلفة.

---

## 5) Partners — العلاقات

| العلاقة | النوع | Delete Rule |
|---------|-------|-------------|
| `SupplierPayments.SupplierId → Suppliers.Id` | N:1 | NO ACTION |
| `CustomerPayments.CustomerId → Customers.Id` | N:1 | NO ACTION |

---

## 6) Inventory — العلاقات (القلب)

| العلاقة | النوع | Delete Rule |
|---------|-------|-------------|
| `Warehouses.StoreId → Stores.Id` | N:1 | NO ACTION |
| `Stock.ProductId → Products.Id` | N:1 | NO ACTION |
| `Stock.WarehouseId → Warehouses.Id` | N:1 | NO ACTION |
| `StockMovements.StockId → Stock.Id` | N:1 | NO ACTION |
| `StockAdjustments.WarehouseId → Warehouses.Id` | N:1 | NO ACTION |
| `StockTransfers.FromWarehouseId → Warehouses.Id` | N:1 | NO ACTION |
| `StockTransfers.ToWarehouseId → Warehouses.Id` | N:1 | NO ACTION |

**تفرّد الرصيد:** `UNIQUE(TenantId, WarehouseId, ProductId)` على `Stock` — صف واحد فقط لكل منتج في كل مستودع.

**الربط اللين للحركة (Soft Reference):** `StockMovements` تحمل `(RefDocType, RefDocId)` بدل FK صريح — لأن مصدر الحركة قد يكون فاتورة بيع/شراء/تسوية/تحويل. هذا **مقصود** لتجنّب FKs متعدّدة اختيارية، ويُفهرس `(RefDocType, RefDocId)` للاستعلام العكسي.

---

## 7) Purchasing — العلاقات

| العلاقة | النوع | Delete Rule |
|---------|-------|-------------|
| `PurchaseOrders.SupplierId → Suppliers.Id` | N:1 | NO ACTION |
| `PurchaseInvoices.SupplierId → Suppliers.Id` | N:1 | NO ACTION |
| `PurchaseInvoices.PurchaseOrderId → PurchaseOrders.Id` | N:1 (NULL) | NO ACTION |
| `PurchaseInvoiceItems.PurchaseInvoiceId → PurchaseInvoices.Id` | N:1 | NO ACTION |
| `PurchaseInvoiceItems.ProductId → Products.Id` | N:1 | NO ACTION |
| `PurchaseReturns.PurchaseInvoiceId → PurchaseInvoices.Id` | N:1 | NO ACTION |

> `PurchaseOrder 1:N PurchaseInvoice` — أمر شراء قد يُفوتر على دفعات (استلام جزئي).

---

## 8) Sales — العلاقات

| العلاقة | النوع | Delete Rule |
|---------|-------|-------------|
| `SalesInvoices.CustomerId → Customers.Id` | N:1 (NULL — بيع نقدي بلا عميل) | NO ACTION |
| `SalesInvoiceItems.SalesInvoiceId → SalesInvoices.Id` | N:1 | NO ACTION |
| `SalesInvoiceItems.ProductId → Products.Id` | N:1 | NO ACTION |
| `SalesInvoiceItems.OfferId → Offers.Id` | N:1 (NULL) | NO ACTION |
| `Payments.SalesInvoiceId → SalesInvoices.Id` | N:1 | NO ACTION |
| `SalesReturns.SalesInvoiceId → SalesInvoices.Id` | N:1 | NO ACTION |
| `SalesReturnItems.SalesReturnId → SalesReturns.Id` | N:1 | NO ACTION |
| `SalesReturnItems.SalesInvoiceItemId → SalesInvoiceItems.Id` | N:1 | NO ACTION |

> **`SalesInvoice 1:N Payments`** — فاتورة واحدة قد تُدفع بعدّة طرق (نقد + بطاقة). مجموع `Payments.Amount` يساوي `SalesInvoice.PaidAmount` (يُتحقّق في طبقة الأعمال).

---

## 9) POS — العلاقات

| العلاقة | النوع | Delete Rule |
|---------|-------|-------------|
| `PosShifts.UserId → Users.Id` | N:1 | NO ACTION |
| `PosSessions.ShiftId → PosShifts.Id` | N:1 | NO ACTION |
| `CashDrawerMovements.ShiftId → PosShifts.Id` | N:1 | NO ACTION |
| `SuspendedInvoices.ShiftId → PosShifts.Id` | N:1 | NO ACTION |

---

## 10) Promotions — العلاقات (تتضمّن N:M)

| العلاقة | النوع | Delete Rule |
|---------|-------|-------------|
| `OfferRules.OfferId → Offers.Id` | N:1 | NO ACTION |
| `OfferProducts.OfferId → Offers.Id` | N:1 | NO ACTION |
| `OfferProducts.ProductId → Products.Id` | N:1 | NO ACTION |
| `OfferUsage.OfferId → Offers.Id` | N:1 | NO ACTION |
| `OfferUsage.SalesInvoiceId → SalesInvoices.Id` | N:1 | NO ACTION |
| `OfferUsage.CustomerId → Customers.Id` | N:1 (NULL) | NO ACTION |

**العلاقة المركّبة (N:M):** **Offers ↔ Products** عبر `OfferProducts`. العرض يشمل عدّة منتجات، والمنتج قد يدخل في عدّة عروض. الجدول يحمل حقولاً إضافية (`IsRequired`, `IsReward`) لدعم عروض "اشترِ X احصل على Y". مفتاح فريد `UNIQUE(TenantId, OfferId, ProductId)`.

---

## 11) ملخّص العلاقات المركّبة (N:M Junction Tables)

| Junction Table | يربط | حقول إضافية | المفتاح الفريد |
|----------------|------|-------------|-----------------|
| `UserRoles` | Users ↔ Roles | — | `(TenantId, UserId, RoleId)` |
| `RolePermissions` | Roles ↔ Permissions | — | `(TenantId, RoleId, PermissionId)` |
| `OfferProducts` | Offers ↔ Products | `IsRequired`, `IsReward` | `(TenantId, OfferId, ProductId)` |

---

## 12) قواعد السلامة على مستوى التطبيق (App-Level Integrity)

بما أن الحذف soft و `NO ACTION`، تُطبَّق هذه القواعد في طبقة الأعمال (Domain / EF Core):

1. **منع حذف أب له أبناء نشطون:** لا يُحذف Category له Products نشطة، ولا Supplier له فواتير غير مسدّدة.
2. **التكامل عبر Global Query Filter:** كل استعلام يُفلتر بـ `TenantId` و`!IsDeleted` تلقائياً — يمنع ربط سجلات من مستأجرين مختلفين.
3. **`StockMovements` و`AuditLogs`:** Append-Only — لا تُعدَّل ولا تُحذف نهائياً.
4. **معاملة ذرّية:** إنشاء فاتورة + بنودها + دفعاتها + حركات مخزونها + تحديث الرصيد يتم في **Transaction واحد** (Unit of Work) — انظر [04-Database-Design.md](../04-Database-Design.md#7).
