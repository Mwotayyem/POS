# 08 — Indexing Strategy (استراتيجية الفهرسة)

> معايير الفهرسة الموحّدة و`CREATE INDEX` الفعلي لجداول النواة. يلتزم بـ [05-Database-Design.md](05-Database-Design.md). الهدف: أداء استعلام ممتاز مع احترام العزل و Soft Delete.

---

## 1) المبادئ الحاكمة للفهرسة (Indexing Principles)

| القاعدة | التفصيل | لماذا |
|---------|---------|-------|
| **فهرس العزل** | `(TenantId)` على كل جدول أعمال — مُرشَّح بـ `WHERE IsDeleted = 0` | كل استعلام يبدأ بـ `TenantId` |
| **كل FK يُفهرَس** | SQL Server لا يفهرس FK تلقائياً | تسريع الـ JOINs ومنع table scans |
| **فهارس مُرشَّحة** | `WHERE IsDeleted = 0` | تصغير الفهرس واستبعاد المحذوف soft |
| **فهارس التفرّد** | `UNIQUE` مع `TenantId` (باركود، اسم مستخدم، رقم فاتورة) | تفرّد داخل المستأجر لا عالمياً |
| **Covering Indexes** | `INCLUDE` للأعمدة المقروءة كثيراً في التقارير | تجنّب key lookups |
| **أعمدة البحث** | باركود، اسم منتج، رقم فاتورة، تاريخ | استعلامات المستخدم المتكرّرة |
| **لا إفراط** | كل فهرس له تكلفة على الكتابة | فهرس فقط عند حاجة استعلام حقيقية |

> **الأساس:** كل استعلام في SmartApp يُترجَم عبر الفلتر العالمي إلى `... WHERE TenantId = @t AND IsDeleted = 0 AND (بقية الشرط)`. لذا يجب أن يبدأ كل فهرس مفيد بـ `TenantId`.

---

## 2) فهرس العزل الإلزامي (Mandatory Tenant Index)

يُطبَّق على **كل** جدول أعمال:

```sql
CREATE NONCLUSTERED INDEX [IX_{Table}_Tenant]
    ON [dbo].[{Table}] ([TenantId])
    WHERE [IsDeleted] = 0;
```

> عند دعم الفروع مستقبلاً، يُوسَّع للكيانات المعنية إلى `([TenantId], [StoreId]) WHERE IsDeleted = 0`.

---

## 3) فهارس Identity

```sql
-- تفرّد اسم المستخدم داخل المستأجر (يخدم أيضاً استعلام تسجيل الدخول)
CREATE UNIQUE NONCLUSTERED INDEX [UX_Users_Tenant_UserName]
    ON [dbo].[Users]([TenantId],[NormalizedUserName]) WHERE [IsDeleted] = 0;

-- البحث بالبريد
CREATE NONCLUSTERED INDEX [IX_Users_Tenant_Email]
    ON [dbo].[Users]([TenantId],[NormalizedEmail]) WHERE [IsDeleted] = 0 AND [NormalizedEmail] IS NOT NULL;

-- تفرّد اسم الدور داخل المستأجر
CREATE UNIQUE NONCLUSTERED INDEX [UX_Roles_Tenant_Name]
    ON [dbo].[Roles]([TenantId],[NormalizedName]) WHERE [IsDeleted] = 0;

-- البحث السريع عن التوكن (rotation + validation)
CREATE NONCLUSTERED INDEX [IX_RefreshTokens_TokenHash]
    ON [dbo].[RefreshTokens]([TokenHash]);

-- تنظيف التوكنات المنتهية (وظيفة دورية)
CREATE NONCLUSTERED INDEX [IX_RefreshTokens_ExpiresUtc]
    ON [dbo].[RefreshTokens]([ExpiresUtc]) WHERE [RevokedUtc] IS NULL;
```

---

## 4) فهارس Catalog

```sql
-- تفرّد الباركود داخل المستأجر (أهمّ فهرس بحث في POS مستقبلاً)
CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductBarcodes_Tenant_Barcode]
    ON [dbo].[ProductBarcodes]([TenantId],[Barcode]) WHERE [IsDeleted] = 0;

-- تفرّد SKU داخل المستأجر
CREATE UNIQUE NONCLUSTERED INDEX [UX_Products_Tenant_Sku]
    ON [dbo].[Products]([TenantId],[Sku]) WHERE [IsDeleted] = 0 AND [Sku] IS NOT NULL;

-- البحث باسم المنتج (Covering لقوائم المنتجات)
CREATE NONCLUSTERED INDEX [IX_Products_Tenant_Name]
    ON [dbo].[Products]([TenantId],[Name])
    INCLUDE ([CategoryId],[SalePrice],[CostPrice],[IsActive])
    WHERE [IsDeleted] = 0;

-- منتجات تصنيف معيّن
CREATE NONCLUSTERED INDEX [IX_Products_Tenant_Category]
    ON [dbo].[Products]([TenantId],[CategoryId]) WHERE [IsDeleted] = 0;

-- التصنيف الشجري
CREATE NONCLUSTERED INDEX [IX_Categories_Tenant_Parent]
    ON [dbo].[Categories]([TenantId],[ParentId]) WHERE [IsDeleted] = 0;

-- FK: ProductUnits
CREATE NONCLUSTERED INDEX [IX_ProductUnits_Tenant_Product]
    ON [dbo].[ProductUnits]([TenantId],[ProductId]) WHERE [IsDeleted] = 0;
```

---

## 5) فهارس Inventory

```sql
-- صف رصيد واحد لكل منتج داخل المستأجر
CREATE UNIQUE NONCLUSTERED INDEX [UX_Stock_Tenant_Product]
    ON [dbo].[Stock]([TenantId],[ProductId]) WHERE [IsDeleted] = 0;

-- منتجات تحت حدّ إعادة الطلب (تقرير + إشعار مستقبلي)
CREATE NONCLUSTERED INDEX [IX_Stock_Tenant_LowQty]
    ON [dbo].[Stock]([TenantId])
    INCLUDE ([ProductId],[QuantityOnHand]) WHERE [IsDeleted] = 0;

-- سجل الحركات: بحث بالمنتج والتاريخ (كشف حركة صنف)
CREATE NONCLUSTERED INDEX [IX_StockMovements_Tenant_Product_Date]
    ON [dbo].[StockMovements]([TenantId],[ProductId],[CreatedDate]);

-- تتبّع المستند المصدر (من أي فاتورة جاءت الحركة)
CREATE NONCLUSTERED INDEX [IX_StockMovements_Source]
    ON [dbo].[StockMovements]([TenantId],[SourceDocType],[SourceDocId]);

-- بنود التسوية
CREATE NONCLUSTERED INDEX [IX_StockAdjItems_Adj]
    ON [dbo].[StockAdjustmentItems]([StockAdjustmentId]) WHERE [IsDeleted] = 0;
```

---

## 6) فهارس Partners

```sql
CREATE NONCLUSTERED INDEX [IX_Customers_Tenant_Name]
    ON [dbo].[Customers]([TenantId],[Name])
    INCLUDE ([Phone],[Balance],[CreditLimit],[IsActive]) WHERE [IsDeleted] = 0;

CREATE NONCLUSTERED INDEX [IX_Customers_Tenant_Phone]
    ON [dbo].[Customers]([TenantId],[Phone]) WHERE [IsDeleted] = 0 AND [Phone] IS NOT NULL;

CREATE NONCLUSTERED INDEX [IX_Suppliers_Tenant_Name]
    ON [dbo].[Suppliers]([TenantId],[Name])
    INCLUDE ([Phone],[Balance],[IsActive]) WHERE [IsDeleted] = 0;

CREATE NONCLUSTERED INDEX [IX_CustPayments_Tenant_Customer]
    ON [dbo].[CustomerPayments]([TenantId],[CustomerId],[PaymentDate]) WHERE [IsDeleted] = 0;

CREATE NONCLUSTERED INDEX [IX_SupPayments_Tenant_Supplier]
    ON [dbo].[SupplierPayments]([TenantId],[SupplierId],[PaymentDate]) WHERE [IsDeleted] = 0;
```

---

## 7) فهارس Sales

```sql
-- تفرّد رقم الفاتورة داخل المستأجر
CREATE UNIQUE NONCLUSTERED INDEX [UX_SalesInvoices_Tenant_Number]
    ON [dbo].[SalesInvoices]([TenantId],[InvoiceNumber]) WHERE [IsDeleted] = 0;

-- استعلامات لوحة/تقارير المبيعات (بالتاريخ + الحالة)
CREATE NONCLUSTERED INDEX [IX_SalesInvoices_Tenant_Date]
    ON [dbo].[SalesInvoices]([TenantId],[InvoiceDate],[Status])
    INCLUDE ([CustomerId],[GrandTotal],[PaidAmount]) WHERE [IsDeleted] = 0;

-- فواتير عميل معيّن
CREATE NONCLUSTERED INDEX [IX_SalesInvoices_Tenant_Customer]
    ON [dbo].[SalesInvoices]([TenantId],[CustomerId]) WHERE [IsDeleted] = 0 AND [CustomerId] IS NOT NULL;

-- بنود الفاتورة
CREATE NONCLUSTERED INDEX [IX_SalesItems_Invoice]
    ON [dbo].[SalesInvoiceItems]([SalesInvoiceId]) WHERE [IsDeleted] = 0;

-- تقرير مبيعات منتج (الأكثر مبيعاً)
CREATE NONCLUSTERED INDEX [IX_SalesItems_Tenant_Product]
    ON [dbo].[SalesInvoiceItems]([TenantId],[ProductId])
    INCLUDE ([Quantity],[LineTotal],[UnitCost]) WHERE [IsDeleted] = 0;

-- المرتجعات
CREATE NONCLUSTERED INDEX [IX_SalesReturns_Tenant_Invoice]
    ON [dbo].[SalesReturns]([TenantId],[SalesInvoiceId]) WHERE [IsDeleted] = 0;

CREATE NONCLUSTERED INDEX [IX_SalesReturnItems_Return]
    ON [dbo].[SalesReturnItems]([SalesReturnId]) WHERE [IsDeleted] = 0;

-- ربط المرتجع بالبند الأصلي (لحساب المتبقّي القابل للإرجاع)
CREATE NONCLUSTERED INDEX [IX_SalesReturnItems_InvItem]
    ON [dbo].[SalesReturnItems]([SalesInvoiceItemId]) WHERE [IsDeleted] = 0;
```

---

## 8) فهارس Purchases

بنية مماثلة لـ Sales:

```sql
CREATE UNIQUE NONCLUSTERED INDEX [UX_PurchaseInvoices_Tenant_Number]
    ON [dbo].[PurchaseInvoices]([TenantId],[InvoiceNumber]) WHERE [IsDeleted] = 0;

CREATE NONCLUSTERED INDEX [IX_PurchaseInvoices_Tenant_Date]
    ON [dbo].[PurchaseInvoices]([TenantId],[InvoiceDate],[Status])
    INCLUDE ([SupplierId],[GrandTotal],[PaidAmount]) WHERE [IsDeleted] = 0;

CREATE NONCLUSTERED INDEX [IX_PurchaseInvoices_Tenant_Supplier]
    ON [dbo].[PurchaseInvoices]([TenantId],[SupplierId]) WHERE [IsDeleted] = 0;

CREATE NONCLUSTERED INDEX [IX_PurchItems_Invoice]
    ON [dbo].[PurchaseInvoiceItems]([PurchaseInvoiceId]) WHERE [IsDeleted] = 0;

CREATE NONCLUSTERED INDEX [IX_PurchaseReturns_Tenant_Invoice]
    ON [dbo].[PurchaseReturns]([TenantId],[PurchaseInvoiceId]) WHERE [IsDeleted] = 0;
```

---

## 9) فهارس Ops & Settings

```sql
-- سجل التدقيق: بحث بالكيان والتاريخ
CREATE NONCLUSTERED INDEX [IX_AuditLogs_Tenant_Entity_Date]
    ON [dbo].[AuditLogs]([TenantId],[EntityName],[CreatedDate]);

-- تدقيق أفعال مستخدم معيّن
CREATE NONCLUSTERED INDEX [IX_AuditLogs_Tenant_User]
    ON [dbo].[AuditLogs]([TenantId],[UserId],[CreatedDate]);

-- الترقيم (فريد أصلاً عبر UX_Sequences)
-- CREATE UNIQUE INDEX UX_Sequences ([TenantId],[DocType]) — معرّف في تعريف الجدول

-- جدول Tenants (يُدار من مالك النظام — بحث بالكود والحالة)
CREATE NONCLUSTERED INDEX [IX_Tenants_Status]
    ON [dbo].[Tenants]([Status]) WHERE [IsDeleted] = 0;
```

---

## 10) استراتيجية الأداء العامّة (Performance Strategy)

| الأسلوب | التطبيق |
|---------|---------|
| **RCSI** | تفعيل `READ_COMMITTED_SNAPSHOT ON` على قاعدة البيانات لتقليل أقفال القراءة |
| **Filtered Indexes** | `WHERE IsDeleted = 0` على كل الفهارس غير المرجعية (تصغير + سرعة) |
| **Covering via INCLUDE** | للاستعلامات المتكرّرة (قوائم المنتجات، تقارير المبيعات) لتجنّب key lookups |
| **تجنّب N+1** | استخدام `Include`/projection مباشرة إلى DTO في الـ Queries |
| **Pagination** | كل قائمة عبر `Skip/Take` مع `OrderBy` مفهرَس — لا تحميل كامل |
| **مراجعة الخطط** | مراقبة Execution Plans للتقارير الثقيلة وإضافة فهارس عند إثبات الحاجة |

---

## 11) قائمة تحقّق الفهرسة للمطوّر (Indexing Checklist)

عند إضافة جدول/ميزة جديدة:

- [ ] فهرس العزل `IX_{Table}_Tenant` على `(TenantId) WHERE IsDeleted = 0` مضاف.
- [ ] كل FK في الجدول مُفهرَس.
- [ ] أعمدة التفرّد (باركود/اسم/رقم مستند) في `UNIQUE` index يتضمّن `TenantId`.
- [ ] أعمدة البحث المتكرّرة مفهرَسة (مع `INCLUDE` عند الحاجة).
- [ ] كل الفهارس غير المرجعية مُرشَّحة بـ `WHERE IsDeleted = 0`.
- [ ] لا فهرس زائد بلا استعلام يبرّره.

---

_يُكمّله [07-ERD-Relationships.md](07-ERD-Relationships.md) (العلاقات المُفهرسة) و[06-Tables-Definitions.md](06-Tables-Definitions.md)._
