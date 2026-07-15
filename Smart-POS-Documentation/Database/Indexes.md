# Database — Indexes (معايير الفهرسة والفهارس الفعلية)

> استراتيجية الفهرسة الكاملة لـ **Smart ERP POS** على **SQL Server 2022**. تلتزم بالمعايير في [04-Database-Design.md](../04-Database-Design.md#6). كل الفهارس المُرشَّحة تستبعد المحذوف soft عبر `WHERE IsDeleted = 0`.

---

## 1) المبادئ الحاكمة (Indexing Standards)

| القاعدة | التفصيل | السبب |
|---------|---------|-------|
| **Clustered Key** | `Id BIGINT IDENTITY` (تصاعدي) | يتجنّب تجزئة الصفحات (Page Splits) |
| **فهرس العزل** | `(TenantId, StoreId)` على كل جدول أعمال | كل استعلام يبدأ بـ `TenantId` |
| **فهرسة كل FK** | إجبارية — SQL Server لا يفهرس FK تلقائياً | تسريع الـ JOINs ومنع Table Scans |
| **Filtered Indexes** | `WHERE IsDeleted = 0` | يستبعد المحذوف، يقلّل حجم الفهرس |
| **Covering Indexes** | `INCLUDE (...)` لأعمدة SELECT الثقيلة | تجنّب Key Lookups في التقارير |
| **Unique داخل المستأجر** | `UNIQUE (TenantId, <NaturalKey>) WHERE IsDeleted=0` | الباركود فريد داخل المستأجر فقط |
| **ترتيب الأعمدة** | الأكثر انتقائية أولاً، ثم أعمدة النطاق | كفاءة Seek |

---

## 2) الفهرس الإلزامي على كل جدول (Isolation Index)

```sql
-- يُطبَّق على كل جدول أعمال (من القالب القياسي)
CREATE NONCLUSTERED INDEX [IX_{Table}_Tenant_Store]
    ON [dbo].[{Table}] ([TenantId], [StoreId])
    WHERE [IsDeleted] = 0;
GO
```

> هذا الفهرس يخدم الـ Global Query Filter الذي يضيف `TenantId = @tenant AND IsDeleted = 0` لكل استعلام.

---

## 3) Catalog — فهارس المنتجات والباركود

```sql
-- تفرّد الباركود داخل المستأجر (بحث POS الأساسي)
CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductBarcodes_Tenant_Barcode]
    ON [dbo].[ProductBarcodes] ([TenantId], [Barcode])
    WHERE [IsDeleted] = 0;
GO

-- بحث المنتج بالاسم/SKU (شاشة البيع + الكتالوج)
CREATE NONCLUSTERED INDEX [IX_Products_Tenant_Name]
    ON [dbo].[Products] ([TenantId], [Name])
    INCLUDE ([Sku], [SellPrice], [CategoryId], [BaseUnitId])   -- Covering
    WHERE [IsDeleted] = 0;
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Products_Tenant_Sku]
    ON [dbo].[Products] ([TenantId], [Sku])
    WHERE [IsDeleted] = 0 AND [Sku] IS NOT NULL;
GO

-- فهرس FK للتصنيف والعلامة
CREATE NONCLUSTERED INDEX [IX_Products_CategoryId]
    ON [dbo].[Products] ([TenantId], [CategoryId]) WHERE [IsDeleted] = 0;
GO

-- شجرة التصنيفات (استعلام الأبناء)
CREATE NONCLUSTERED INDEX [IX_Categories_Parent]
    ON [dbo].[Categories] ([TenantId], [ParentId])
    INCLUDE ([Name], [Level], [SortOrder]) WHERE [IsDeleted] = 0;
GO
```

---

## 4) Inventory — فهارس المخزون (حرجة للأداء)

```sql
-- تفرّد رصيد المنتج في المستودع + بحث الرصيد السريع
CREATE UNIQUE NONCLUSTERED INDEX [UX_Stock_Tenant_WH_Product]
    ON [dbo].[Stock] ([TenantId], [WarehouseId], [ProductId])
    INCLUDE ([QtyOnHand], [QtyReserved], [AvgCost])   -- Covering لشاشة البيع
    WHERE [IsDeleted] = 0;
GO

-- سجل حركات المخزون: استعلام كارت الصنف (زمني)
CREATE NONCLUSTERED INDEX [IX_StockMovements_Stock_Date]
    ON [dbo].[StockMovements] ([TenantId], [StockId], [CreatedDate] DESC)
    INCLUDE ([MovementType], [Qty], [BalanceAfter]);
GO

-- الاستعلام العكسي: أي حركات ولّدتها فاتورة معيّنة
CREATE NONCLUSTERED INDEX [IX_StockMovements_RefDoc]
    ON [dbo].[StockMovements] ([TenantId], [RefDocType], [RefDocId]);
GO
```

---

## 5) Sales — فهارس الفواتير (بحث + تقارير)

```sql
-- رقم الفاتورة فريد داخل المستأجر/الفرع
CREATE UNIQUE NONCLUSTERED INDEX [UX_SalesInvoices_Number]
    ON [dbo].[SalesInvoices] ([TenantId], [StoreId], [InvoiceNumber])
    WHERE [IsDeleted] = 0;
GO

-- تقرير المبيعات اليومي/الفترة (Covering — يتجنّب Lookups)
CREATE NONCLUSTERED INDEX [IX_SalesInvoices_Tenant_Store_Date]
    ON [dbo].[SalesInvoices] ([TenantId], [StoreId], [CreatedDate] DESC)
    INCLUDE ([Total], [PaidAmount], [Status], [CustomerId])
    WHERE [IsDeleted] = 0;
GO

-- بنود الفاتورة (تحميل الفاتورة)
CREATE NONCLUSTERED INDEX [IX_SalesInvoiceItems_Invoice]
    ON [dbo].[SalesInvoiceItems] ([TenantId], [SalesInvoiceId])
    INCLUDE ([ProductId], [Qty], [UnitPrice], [LineTotal])
    WHERE [IsDeleted] = 0;
GO

-- الأكثر مبيعاً (تقرير المنتجات) — Covering للتجميع
CREATE NONCLUSTERED INDEX [IX_SalesInvoiceItems_Product]
    ON [dbo].[SalesInvoiceItems] ([TenantId], [ProductId])
    INCLUDE ([Qty], [LineTotal]) WHERE [IsDeleted] = 0;
GO

-- دفعات الفاتورة
CREATE NONCLUSTERED INDEX [IX_Payments_Invoice]
    ON [dbo].[Payments] ([TenantId], [SalesInvoiceId]) WHERE [IsDeleted] = 0;
GO
```

---

## 6) Purchasing — فهارس الشراء

```sql
CREATE UNIQUE NONCLUSTERED INDEX [UX_PurchaseInvoices_Number]
    ON [dbo].[PurchaseInvoices] ([TenantId], [StoreId], [InvoiceNumber])
    WHERE [IsDeleted] = 0;
GO

CREATE NONCLUSTERED INDEX [IX_PurchaseInvoices_Supplier_Date]
    ON [dbo].[PurchaseInvoices] ([TenantId], [SupplierId], [CreatedDate] DESC)
    INCLUDE ([Total], [PaidAmount], [Status]) WHERE [IsDeleted] = 0;
GO

CREATE NONCLUSTERED INDEX [IX_PurchaseInvoiceItems_Invoice]
    ON [dbo].[PurchaseInvoiceItems] ([TenantId], [PurchaseInvoiceId])
    INCLUDE ([ProductId], [Qty], [UnitCost]) WHERE [IsDeleted] = 0;
GO
```

---

## 7) Identity — فهارس المصادقة

```sql
-- تسجيل الدخول: بحث البريد داخل المستأجر
CREATE UNIQUE NONCLUSTERED INDEX [UX_Users_Tenant_Email]
    ON [dbo].[Users] ([TenantId], [Email])
    WHERE [IsDeleted] = 0;
GO

-- التحقق من Refresh Token (بحث مباشر بالقيمة)
CREATE UNIQUE NONCLUSTERED INDEX [UX_RefreshTokens_Token]
    ON [dbo].[RefreshTokens] ([Token])
    WHERE [IsDeleted] = 0;
GO

CREATE NONCLUSTERED INDEX [IX_RefreshTokens_User]
    ON [dbo].[RefreshTokens] ([TenantId], [UserId], [ExpiresAt]);
GO

-- Junction tables
CREATE UNIQUE NONCLUSTERED INDEX [UX_UserRoles]
    ON [dbo].[UserRoles] ([TenantId], [UserId], [RoleId]) WHERE [IsDeleted] = 0;
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_RolePermissions]
    ON [dbo].[RolePermissions] ([TenantId], [RoleId], [PermissionId]) WHERE [IsDeleted] = 0;
GO
```

---

## 8) POS — فهارس الشفتات

```sql
-- الشفت المفتوح للكاشير (بحث الشفت النشط)
CREATE NONCLUSTERED INDEX [IX_PosShifts_User_Status]
    ON [dbo].[PosShifts] ([TenantId], [StoreId], [UserId], [Status])
    INCLUDE ([OpenedAt], [OpeningCash]) WHERE [IsDeleted] = 0;
GO

CREATE NONCLUSTERED INDEX [IX_CashDrawer_Shift]
    ON [dbo].[CashDrawerMovements] ([TenantId], [ShiftId]) WHERE [IsDeleted] = 0;
GO
```

---

## 9) Promotions — فهارس العروض

```sql
-- العروض النشطة الآن (Filtered على التاريخ + النشاط)
CREATE NONCLUSTERED INDEX [IX_Offers_Active]
    ON [dbo].[Offers] ([TenantId], [StoreId], [StartDate], [EndDate])
    INCLUDE ([Type], [Value], [Priority])
    WHERE [IsDeleted] = 0 AND [IsActive] = 1;
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_OfferProducts]
    ON [dbo].[OfferProducts] ([TenantId], [OfferId], [ProductId]) WHERE [IsDeleted] = 0;
GO
```

---

## 10) Ops — فهارس التدقيق والإشعارات

```sql
-- سجل التدقيق: بحث بالكيان
CREATE NONCLUSTERED INDEX [IX_AuditLogs_Entity]
    ON [dbo].[AuditLogs] ([TenantId], [EntityName], [EntityId], [CreatedDate] DESC);
GO

-- إشعارات المستخدم غير المقروءة
CREATE NONCLUSTERED INDEX [IX_Notifications_User_Unread]
    ON [dbo].[Notifications] ([TenantId], [UserId], [CreatedDate] DESC)
    INCLUDE ([Title], [Type])
    WHERE [IsDeleted] = 0 AND [IsRead] = 0;
GO

-- تسلسل الترقيم (بحث ذرّي)
CREATE UNIQUE NONCLUSTERED INDEX [UX_Sequences_Lookup]
    ON [dbo].[Sequences] ([TenantId], [StoreId], [DocType]);
GO
```

---

## 11) قواعد الصيانة (Maintenance)

| البند | القرار |
|-------|--------|
| Fill Factor | 90% للجداول عالية الكتابة (فواتير، حركات) |
| إعادة البناء (Rebuild) | عند تجزئة > 30% |
| إعادة التنظيم (Reorganize) | عند تجزئة 5%–30% |
| الإحصائيات (Statistics) | `AUTO_UPDATE_STATISTICS ON` + تحديث يدوي للجداول الضخمة عبر Hangfire |
| فهارس مفقودة | مراجعة `sys.dm_db_missing_index_details` دورياً |
| فهارس غير مستخدمة | مراجعة `sys.dm_db_index_usage_stats` وحذف الزائد |

---

## 12) ملخّص أنواع الفهارس المستخدمة

| النوع | الاستخدام | مثال |
|-------|-----------|------|
| Clustered | PK على `Id` | كل جدول |
| Isolation NC | `(TenantId, StoreId)` | كل جدول أعمال |
| FK NC | كل عمود FK | `IX_Products_CategoryId` |
| Unique Filtered | مفاتيح طبيعية داخل المستأجر | `UX_ProductBarcodes_Tenant_Barcode` |
| Covering (INCLUDE) | تقارير ثقيلة | `IX_SalesInvoices_Tenant_Store_Date` |
| Filtered (WHERE) | استبعاد soft-deleted / حالات | `IX_Offers_Active`, `IX_Notifications_User_Unread` |
