# 20 — Reports (التقارير)

> وحدة التقارير هي **العقل التحليلي** للنظام: تحوّل الحركات التشغيلية (بيع، شراء، مخزون، دفعات) إلى معلومة قابلة للقرار. كل تقرير هنا يلتزم بمعايير [04-Database-Design.md](04-Database-Design.md): فلترة `TenantId` إجبارية، استبعاد `IsDeleted = 1`، أموال `DECIMAL(18,4)`، وتواريخ UTC تُحوَّل لتوقيت المستأجر عند العرض فقط.

---

## 1) الهدف (Purpose)

توفير مجموعة تقارير **جاهزة (Pre-built)** و**قابلة للإعداد (Configurable)** تغطّي كل دورة العمل التجاري: المبيعات، المشتريات، المخزون، الموردين، العملاء، التدفّق النقدي، الأرباح والخسائر، الضرائب، العروض، المرتجعات، والمستودعات — مع تصدير PDF/Excel وطباعة، وأداء مضبوط للتقارير الثقيلة عبر الفهرسة، التجميع المسبق، والترقيم (Pagination).

**المبدأ الحاكم:** التقارير **للقراءة فقط (Read-Only)**؛ لا يعدّل تقرير أي بيانات تشغيلية إطلاقاً. جميع الاستعلامات تعمل على عزل `READ COMMITTED SNAPSHOT` دون فرض أقفال على جداول العمليات.

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية (Permission) | الوصف | الأدوار الافتراضية |
|------------------------|-------|--------------------|
| `Reports.View` | فتح وحدة التقارير | Owner, StoreManager, Accountant |
| `Reports.Sales` | تقارير المبيعات والمرتجعات | Owner, StoreManager, Accountant, Cashier(فرعه فقط) |
| `Reports.Purchase` | تقارير المشتريات والموردين | Owner, StoreManager, Purchasing |
| `Reports.Inventory` | تقارير المخزون والمستودعات | Owner, StoreManager |
| `Reports.Financial` | الأرباح، الخسائر، التدفّق النقدي، الضرائب | Owner, Accountant |
| `Reports.Export` | تصدير PDF/Excel | حسب الحاجة (قد تُقيَّد للمالك) |
| `Reports.AllStores` | رؤية كل الفروع (لا فرع المستخدم فقط) | Owner, Accountant |

**قاعدة العزل الحاكمة:** المستخدم الذي لا يملك `Reports.AllStores` يُحقن `StoreId` الخاص به في كل استعلام قسراً على مستوى الخادم — لا يُعتمد على الواجهة. أي محاولة تمرير `storeId` مختلف تُرفض (403).

---

## 3) تصميم الصفحة (Page Layout)

```
┌────────────────────────────────────────────────────────────────────┐
│  التقارير   [شريط بحث عن تقرير]              👤 المستخدم | الفرع     │
├────────────┬───────────────────────────────────────────────────────┤
│  الأقسام    │   ┌─── لوحة الفلاتر (Filters Bar) ──────────────────┐  │
│            │   │ [من تاريخ] [إلى تاريخ] [الفرع▼] [المستودع▼]     │  │
│ • المبيعات  │   │ [التصنيف▼] [المنتج🔍] [العميل🔍] [المورد🔍]      │  │
│ • المشتريات │   │ [تجميع حسب▼]           [عرض] [مسح] [حفظ قالب]    │  │
│ • المخزون   │   └─────────────────────────────────────────────────┘  │
│ • الموردون  │   ┌─── بطاقات ملخّص (KPI Cards) ────────────────────┐  │
│ • العملاء   │   │  إجمالي │ عدد │ متوسط │ صافي                   │  │
│ • النقدية   │   └─────────────────────────────────────────────────┘  │
│ • الأرباح   │   ┌─── الجدول (DataTable) ──────────────────────────┐  │
│ • الضرائب   │   │  #  التاريخ  الفاتورة  ...  الإجمالي            │  │
│ • العروض    │   │  ... صفوف مرقّمة server-side ...                │  │
│ • المرتجعات │   │                                    [◀ 1 2 3 ▶]  │  │
│            │   └─────────────────────────────────────────────────┘  │
│            │   [🖨 طباعة] [📄 PDF] [📊 Excel] [📈 رسم بياني]        │
└────────────┴───────────────────────────────────────────────────────┘
```

**مبادئ التصميم:** RTL افتراضي، DataTables بوضع `serverSide: true` (لا يُحمَّل كل شيء في المتصفح)، والفلاتر تُطبَّق عبر AJAX دون إعادة تحميل الصفحة.

---

## 4) الأزرار (Buttons)

| الزر | الوظيفة | الصلاحية |
|------|---------|----------|
| **عرض (Run)** | تنفيذ التقرير بالفلاتر الحالية | `Reports.<Module>` |
| **مسح (Reset)** | إعادة الفلاتر للافتراضي (اليوم / الشهر) | — |
| **حفظ قالب (Save Template)** | حفظ مجموعة الفلاتر كقالب مسمّى | `Reports.View` |
| **طباعة (Print)** | فتح نسخة الطباعة (قالب المستأجر) | `Reports.View` |
| **PDF** | توليد PDF على الخادم (QuestPDF) | `Reports.Export` |
| **Excel** | توليد XLSX (ClosedXML) | `Reports.Export` |
| **رسم بياني (Chart)** | تبديل لعرض Chart.js | `Reports.View` |
| **جدولة (Schedule)** | إرسال دوري بالبريد (Hangfire) | `Reports.Export` |

---

## 5) الحقول والفلاتر المشتركة (Common Fields & Filters)

| الفلتر | النوع | ملاحظات |
|--------|-------|---------|
| `DateFrom` / `DateTo` | تاريخ | إلزامي؛ الافتراضي = بداية/نهاية الشهر الحالي بتوقيت المستأجر، يُحوَّل لـ UTC قبل الاستعلام |
| `StoreId` | قائمة | يُقيَّد بفرع المستخدم إن لم يملك `Reports.AllStores` |
| `WarehouseId` | قائمة | لتقارير المخزون |
| `CategoryId` | شجرة | يشمل التصنيفات الفرعية |
| `ProductId` | بحث AJAX | باركود أو اسم |
| `CustomerId` / `SupplierId` | بحث AJAX | — |
| `GroupBy` | قائمة | يوم/أسبوع/شهر/فرع/منتج/كاشير |
| `Status` | قائمة | مسدّدة/آجلة/ملغاة... حسب التقرير |
| `PageNumber` / `PageSize` | ترقيم | server-side؛ الافتراضي 50، الأقصى 500 |

---

## 6) التحقق (Validation)

| القاعدة | الرسالة |
|---------|---------|
| `DateFrom <= DateTo` | «تاريخ البداية يجب أن يسبق تاريخ النهاية» |
| المدى الأقصى للتقارير التفصيلية ≤ 366 يوماً | «المدى الزمني كبير جداً؛ استخدم التقرير المجمّع» |
| `PageSize <= 500` | يُقصّ للحدّ الأقصى تلقائياً |
| `StoreId` ضمن فروع المستأجر | «فرع غير صالح» (403 إن كان خارج نطاق الصلاحية) |
| التصدير يتطلّب نتائج > 0 | «لا توجد بيانات للتصدير» |

---

## 7) قواعد العمل (Business Rules)

1. **العزل أولاً:** كل استعلام يبدأ بـ `WHERE TenantId = @TenantId AND IsDeleted = 0`. تُطبَّق هذه الشروط على مستوى الخادم حتى لو غابت من الواجهة.
2. **الفواتير الملغاة/المرتجعة:** لا تُحتسب في الإيراد الصافي؛ تظهر في تقاريرها الخاصة (المرتجعات) وتُطرح من الصافي.
3. **التكلفة:** تُقرأ من `CostAtSale` المخزّنة لحظة البيع (Snapshot)، لا من تكلفة المنتج الحالية — لضمان دقّة الربح التاريخي.
4. **الضريبة:** تُحتسب من حقول الضريبة المخزّنة في بنود الفاتورة، لا يُعاد حسابها من النسبة الحالية.
5. **التوقيت:** التخزين UTC؛ التجميع «حسب اليوم» يستخدم `AT TIME ZONE` لتوقيت المستأجر لتفادي انزياح اليوم.
6. **الاتّساق:** التقارير المالية تُقرأ من لقطة متّسقة (RCSI) دون حجب العمليات.

---

## 8) كتالوج التقارير (Reports Catalog)

فيما يلي كل تقرير مع الغرض، الفلاتر، الأعمدة، واستعلام SQL نموذجي فعلي. **كل استعلام يفترض** المعاملات: `@TenantId`, `@StoreId` (أو `NULL` = كل الفروع المصرّح بها), `@From`, `@To`.

---

### 8.1 تقرير المبيعات (Sales Report)

**الغرض:** إجمالي وتفصيل المبيعات خلال مدة، حسب فرع/منتج/كاشير/يوم.
**الفلاتر:** التاريخ، الفرع، الكاشير، التصنيف، المنتج، حالة الدفع، `GroupBy`.
**الأعمدة:** #، رقم الفاتورة، التاريخ، الفرع، الكاشير، العميل، الإجمالي قبل الضريبة، الخصم، الضريبة، الصافي، طريقة الدفع.

```sql
-- تفصيل المبيعات مع الترقيم (server-side)
SELECT
    si.[Id],
    si.[InvoiceNumber],
    si.[InvoiceDate] AT TIME ZONE 'UTC' AT TIME ZONE @TenantTz AS InvoiceLocalDate,
    st.[Name]                       AS StoreName,
    u.[FullName]                    AS Cashier,
    ISNULL(c.[Name], N'نقدي')       AS CustomerName,
    si.[SubTotal],
    si.[DiscountAmount],
    si.[TaxAmount],
    si.[NetTotal],
    si.[PaymentStatus]
FROM [dbo].[SalesInvoices] si
JOIN [dbo].[Stores] st ON st.[Id] = si.[StoreId]
LEFT JOIN [dbo].[Users] u   ON u.[Id] = si.[CashierId]
LEFT JOIN [dbo].[Customers] c ON c.[Id] = si.[CustomerId] AND c.[IsDeleted] = 0
WHERE si.[TenantId]  = @TenantId
  AND si.[IsDeleted] = 0
  AND si.[Status]    <> 'Cancelled'
  AND (@StoreId IS NULL OR si.[StoreId] = @StoreId)
  AND si.[InvoiceDate] >= @From
  AND si.[InvoiceDate] <  @To
ORDER BY si.[InvoiceDate] DESC
OFFSET (@PageNumber - 1) * @PageSize ROWS
FETCH NEXT @PageSize ROWS ONLY;

-- بطاقات الملخّص (KPI)
SELECT
    COUNT(*)                 AS InvoicesCount,
    SUM(si.[NetTotal])       AS TotalNet,
    SUM(si.[TaxAmount])      AS TotalTax,
    SUM(si.[DiscountAmount]) AS TotalDiscount,
    AVG(si.[NetTotal])       AS AvgInvoiceValue
FROM [dbo].[SalesInvoices] si
WHERE si.[TenantId] = @TenantId AND si.[IsDeleted] = 0
  AND si.[Status] <> 'Cancelled'
  AND (@StoreId IS NULL OR si.[StoreId] = @StoreId)
  AND si.[InvoiceDate] >= @From AND si.[InvoiceDate] < @To;
```

---

### 8.2 تقرير المشتريات (Purchase Report)

**الغرض:** مشتريات المستأجر حسب مورد/منتج/فرع خلال مدة.
**الفلاتر:** التاريخ، الفرع، المورد، التصنيف، حالة السداد.
**الأعمدة:** رقم فاتورة الشراء، التاريخ، المورد، عدد البنود، الإجمالي، الضريبة، الصافي، المدفوع، المتبقّي.

```sql
SELECT
    pi.[InvoiceNumber],
    pi.[InvoiceDate],
    sup.[Name]                          AS SupplierName,
    COUNT(pii.[Id])                     AS LinesCount,
    pi.[SubTotal],
    pi.[TaxAmount],
    pi.[NetTotal],
    pi.[PaidAmount],
    (pi.[NetTotal] - pi.[PaidAmount])   AS OutstandingAmount
FROM [dbo].[PurchaseInvoices] pi
JOIN [dbo].[Suppliers] sup ON sup.[Id] = pi.[SupplierId]
JOIN [dbo].[PurchaseInvoiceItems] pii
     ON pii.[PurchaseInvoiceId] = pi.[Id] AND pii.[IsDeleted] = 0
WHERE pi.[TenantId] = @TenantId AND pi.[IsDeleted] = 0
  AND (@StoreId IS NULL OR pi.[StoreId] = @StoreId)
  AND (@SupplierId IS NULL OR pi.[SupplierId] = @SupplierId)
  AND pi.[InvoiceDate] >= @From AND pi.[InvoiceDate] < @To
GROUP BY pi.[InvoiceNumber], pi.[InvoiceDate], sup.[Name],
         pi.[SubTotal], pi.[TaxAmount], pi.[NetTotal], pi.[PaidAmount]
ORDER BY pi.[InvoiceDate] DESC;
```

---

### 8.3 تقرير المخزون (Inventory / Stock Report)

**الغرض:** الرصيد الحالي وقيمته لكل منتج/مستودع، مع تنبيه الحدّ الأدنى.
**الفلاتر:** الفرع، المستودع، التصنيف، «تحت الحدّ الأدنى فقط».
**الأعمدة:** المنتج، الباركود، المستودع، الكمية، تكلفة الوحدة، قيمة المخزون، حدّ الطلب، الحالة.

```sql
SELECT
    p.[Name]                       AS ProductName,
    pb.[Barcode],
    w.[Name]                       AS WarehouseName,
    s.[Quantity],
    s.[AverageCost],
    (s.[Quantity] * s.[AverageCost]) AS StockValue,
    p.[ReorderLevel],
    CASE WHEN s.[Quantity] <= p.[ReorderLevel] THEN 'LOW'
         WHEN s.[Quantity] = 0 THEN 'OUT'
         ELSE 'OK' END            AS StockStatus
FROM [dbo].[Stock] s
JOIN [dbo].[Products] p   ON p.[Id] = s.[ProductId] AND p.[IsDeleted] = 0
JOIN [dbo].[Warehouses] w ON w.[Id] = s.[WarehouseId]
LEFT JOIN [dbo].[ProductBarcodes] pb
     ON pb.[ProductId] = p.[Id] AND pb.[IsPrimary] = 1 AND pb.[IsDeleted] = 0
WHERE s.[TenantId] = @TenantId AND s.[IsDeleted] = 0
  AND (@WarehouseId IS NULL OR s.[WarehouseId] = @WarehouseId)
  AND (@CategoryId  IS NULL OR p.[CategoryId]  = @CategoryId)
  AND (@LowOnly = 0 OR s.[Quantity] <= p.[ReorderLevel])
ORDER BY StockValue DESC;
```

---

### 8.4 تقرير الموردين (Supplier Ledger / Statement)

**الغرض:** كشف حساب مورد: المشتريات، الدفعات، والرصيد المدين/الدائن.
**الفلاتر:** المورد، التاريخ.
**الأعمدة:** التاريخ، نوع الحركة، المرجع، مدين، دائن، الرصيد التراكمي.

```sql
;WITH Ledger AS (
    SELECT pi.[InvoiceDate] AS TxnDate, 'PURCHASE' AS TxnType,
           pi.[InvoiceNumber] AS Reference, pi.[NetTotal] AS Debit, 0 AS Credit
    FROM [dbo].[PurchaseInvoices] pi
    WHERE pi.[TenantId] = @TenantId AND pi.[IsDeleted] = 0
      AND pi.[SupplierId] = @SupplierId
    UNION ALL
    SELECT sp.[PaymentDate], 'PAYMENT',
           sp.[ReferenceNo], 0, sp.[Amount]
    FROM [dbo].[SupplierPayments] sp
    WHERE sp.[TenantId] = @TenantId AND sp.[IsDeleted] = 0
      AND sp.[SupplierId] = @SupplierId
)
SELECT TxnDate, TxnType, Reference, Debit, Credit,
       SUM(Debit - Credit) OVER (ORDER BY TxnDate, TxnType
            ROWS UNBOUNDED PRECEDING) AS RunningBalance
FROM Ledger
WHERE TxnDate >= @From AND TxnDate < @To
ORDER BY TxnDate, TxnType;
```

---

### 8.5 تقرير العملاء (Customer Statement)

**الغرض:** كشف حساب عميل آجل: الفواتير، المرتجعات، الدفعات، الرصيد.
**الفلاتر:** العميل، التاريخ.
**الأعمدة:** التاريخ، النوع، المرجع، مدين، دائن، الرصيد.

```sql
;WITH Ledger AS (
    SELECT si.[InvoiceDate] AS TxnDate, 'SALE' AS TxnType,
           si.[InvoiceNumber] AS Reference, si.[NetTotal] AS Debit, 0 AS Credit
    FROM [dbo].[SalesInvoices] si
    WHERE si.[TenantId] = @TenantId AND si.[IsDeleted] = 0
      AND si.[Status] <> 'Cancelled' AND si.[CustomerId] = @CustomerId
    UNION ALL
    SELECT sr.[ReturnDate], 'RETURN', sr.[ReturnNumber], 0, sr.[NetTotal]
    FROM [dbo].[SalesReturns] sr
    WHERE sr.[TenantId] = @TenantId AND sr.[IsDeleted] = 0
      AND sr.[CustomerId] = @CustomerId
    UNION ALL
    SELECT cp.[PaymentDate], 'PAYMENT', cp.[ReferenceNo], 0, cp.[Amount]
    FROM [dbo].[CustomerPayments] cp
    WHERE cp.[TenantId] = @TenantId AND cp.[IsDeleted] = 0
      AND cp.[CustomerId] = @CustomerId
)
SELECT TxnDate, TxnType, Reference, Debit, Credit,
       SUM(Debit - Credit) OVER (ORDER BY TxnDate
            ROWS UNBOUNDED PRECEDING) AS RunningBalance
FROM Ledger
WHERE TxnDate >= @From AND TxnDate < @To
ORDER BY TxnDate;
```

---

### 8.6 تقرير التدفّق النقدي (Cash Flow Report)

**الغرض:** الداخل/الخارج نقدياً (مبيعات نقدية، تحصيل عملاء، سداد موردين، مصروفات، حركات الصندوق).
**الفلاتر:** التاريخ، الفرع، الصندوق/طريقة الدفع.
**الأعمدة:** التاريخ، المصدر، وارد (Inflow)، صادر (Outflow)، الصافي.

```sql
SELECT CAST(TxnDate AS DATE) AS [Day],
       SUM(Inflow)  AS TotalInflow,
       SUM(Outflow) AS TotalOutflow,
       SUM(Inflow - Outflow) AS NetCashFlow
FROM (
    SELECT p.[PaymentDate] AS TxnDate, p.[Amount] AS Inflow, 0 AS Outflow
    FROM [dbo].[Payments] p
    WHERE p.[TenantId] = @TenantId AND p.[IsDeleted] = 0
      AND p.[Method] = 'CASH' AND p.[Direction] = 'IN'
      AND (@StoreId IS NULL OR p.[StoreId] = @StoreId)
    UNION ALL
    SELECT sp.[PaymentDate], 0, sp.[Amount]
    FROM [dbo].[SupplierPayments] sp
    WHERE sp.[TenantId] = @TenantId AND sp.[IsDeleted] = 0
      AND sp.[Method] = 'CASH'
      AND (@StoreId IS NULL OR sp.[StoreId] = @StoreId)
    UNION ALL
    SELECT cdm.[MovementDate],
           CASE WHEN cdm.[Type] = 'IN'  THEN cdm.[Amount] ELSE 0 END,
           CASE WHEN cdm.[Type] = 'OUT' THEN cdm.[Amount] ELSE 0 END
    FROM [dbo].[CashDrawerMovements] cdm
    WHERE cdm.[TenantId] = @TenantId AND cdm.[IsDeleted] = 0
      AND (@StoreId IS NULL OR cdm.[StoreId] = @StoreId)
) x
WHERE TxnDate >= @From AND TxnDate < @To
GROUP BY CAST(TxnDate AS DATE)
ORDER BY [Day];
```

---

### 8.7 تقرير الأرباح (Profit / Gross Margin Report)

**الغرض:** الربح الإجمالي = الإيراد الصافي − تكلفة البضاعة المباعة (COGS)، حسب منتج/تصنيف/فرع.
**الفلاتر:** التاريخ، الفرع، التصنيف، المنتج، `GroupBy`.
**الأعمدة:** المنتج، الكمية المباعة، الإيراد، التكلفة (COGS)، الربح، هامش الربح %.

```sql
SELECT
    p.[Name]                                    AS ProductName,
    SUM(sii.[Quantity])                         AS QtySold,
    SUM(sii.[LineNet])                          AS Revenue,
    SUM(sii.[Quantity] * sii.[CostAtSale])      AS COGS,
    SUM(sii.[LineNet] - sii.[Quantity]*sii.[CostAtSale]) AS GrossProfit,
    CASE WHEN SUM(sii.[LineNet]) = 0 THEN 0
         ELSE CAST(SUM(sii.[LineNet] - sii.[Quantity]*sii.[CostAtSale]) * 100.0
                   / SUM(sii.[LineNet]) AS DECIMAL(9,2))
    END                                         AS MarginPct
FROM [dbo].[SalesInvoiceItems] sii
JOIN [dbo].[SalesInvoices] si
     ON si.[Id] = sii.[SalesInvoiceId] AND si.[IsDeleted] = 0
        AND si.[Status] <> 'Cancelled'
JOIN [dbo].[Products] p ON p.[Id] = sii.[ProductId]
WHERE sii.[TenantId] = @TenantId AND sii.[IsDeleted] = 0
  AND (@StoreId IS NULL OR si.[StoreId] = @StoreId)
  AND (@CategoryId IS NULL OR p.[CategoryId] = @CategoryId)
  AND si.[InvoiceDate] >= @From AND si.[InvoiceDate] < @To
GROUP BY p.[Name]
ORDER BY GrossProfit DESC;
```

> **ملاحظة مهمّة:** `CostAtSale` هو لقطة (snapshot) للتكلفة المتوسّطة لحظة البيع، تُخزَّن في بند الفاتورة — لضمان ثبات الربح التاريخي حتى لو تغيّرت تكلفة المنتج لاحقاً.

---

### 8.8 تقرير الخسائر / الهدر (Loss & Wastage Report)

**الغرض:** خسائر المخزون: تلف، انتهاء صلاحية، عجز جرد، تسويات سالبة.
**الفلاتر:** التاريخ، المستودع، سبب التسوية.
**الأعمدة:** التاريخ، المنتج، الكمية، التكلفة، القيمة، السبب.

```sql
SELECT sa.[AdjustmentDate], p.[Name] AS ProductName,
       sa.[Quantity], s.[AverageCost] AS UnitCost,
       (ABS(sa.[Quantity]) * s.[AverageCost]) AS LossValue,
       sa.[Reason]
FROM [dbo].[StockAdjustments] sa
JOIN [dbo].[Products] p ON p.[Id] = sa.[ProductId]
JOIN [dbo].[Stock] s
     ON s.[ProductId] = sa.[ProductId] AND s.[WarehouseId] = sa.[WarehouseId]
WHERE sa.[TenantId] = @TenantId AND sa.[IsDeleted] = 0
  AND sa.[Quantity] < 0            -- الخسائر فقط (تخفيض)
  AND (@WarehouseId IS NULL OR sa.[WarehouseId] = @WarehouseId)
  AND sa.[AdjustmentDate] >= @From AND sa.[AdjustmentDate] < @To
ORDER BY LossValue DESC;
```

---

### 8.9 تقرير الضرائب (Tax Report)

**الغرض:** إجمالي ضريبة المبيعات (Output) وضريبة المشتريات (Input) خلال المدة الضريبية.
**الفلاتر:** التاريخ (فترة ضريبية)، الفرع، نسبة الضريبة.
**الأعمدة:** نسبة الضريبة، الوعاء (Base)، ضريبة المخرجات، ضريبة المدخلات، الصافي المستحق.

```sql
SELECT
    tr.[Rate]                       AS TaxRate,
    SUM(CASE WHEN src = 'OUT' THEN Base ELSE 0 END) AS OutputBase,
    SUM(CASE WHEN src = 'OUT' THEN Tax  ELSE 0 END) AS OutputTax,
    SUM(CASE WHEN src = 'IN'  THEN Tax  ELSE 0 END) AS InputTax,
    SUM(CASE WHEN src = 'OUT' THEN Tax ELSE 0 END)
      - SUM(CASE WHEN src = 'IN' THEN Tax ELSE 0 END) AS NetTaxPayable
FROM (
    SELECT sii.[TaxRateId] AS TaxRateId, 'OUT' AS src,
           sii.[LineNet] AS Base, sii.[TaxAmount] AS Tax
    FROM [dbo].[SalesInvoiceItems] sii
    JOIN [dbo].[SalesInvoices] si ON si.[Id] = sii.[SalesInvoiceId]
         AND si.[IsDeleted] = 0 AND si.[Status] <> 'Cancelled'
    WHERE sii.[TenantId] = @TenantId AND sii.[IsDeleted] = 0
      AND si.[InvoiceDate] >= @From AND si.[InvoiceDate] < @To
    UNION ALL
    SELECT pii.[TaxRateId], 'IN', pii.[LineNet], pii.[TaxAmount]
    FROM [dbo].[PurchaseInvoiceItems] pii
    JOIN [dbo].[PurchaseInvoices] pi ON pi.[Id] = pii.[PurchaseInvoiceId]
         AND pi.[IsDeleted] = 0
    WHERE pii.[TenantId] = @TenantId AND pii.[IsDeleted] = 0
      AND pi.[InvoiceDate] >= @From AND pi.[InvoiceDate] < @To
) x
JOIN [dbo].[TaxRates] tr ON tr.[Id] = x.TaxRateId
GROUP BY tr.[Rate]
ORDER BY tr.[Rate];
```

---

### 8.10 تقرير العروض (Offers / Promotions Effectiveness)

**الغرض:** قياس فعالية كل عرض: عدد مرات الاستخدام، قيمة الخصم الممنوح، الإيراد المرتبط.
**الفلاتر:** التاريخ، العرض، الفرع.
**الأعمدة:** العرض، عدد الاستخدامات، إجمالي الخصم، الإيراد المرتبط، متوسط الخصم للفاتورة.

```sql
SELECT o.[Name] AS OfferName,
       COUNT(ou.[Id])                 AS TimesUsed,
       SUM(ou.[DiscountApplied])      AS TotalDiscountGiven,
       SUM(si.[NetTotal])             AS AttributedRevenue,
       AVG(ou.[DiscountApplied])      AS AvgDiscountPerUse
FROM [dbo].[OfferUsage] ou
JOIN [dbo].[Offers] o ON o.[Id] = ou.[OfferId]
JOIN [dbo].[SalesInvoices] si
     ON si.[Id] = ou.[SalesInvoiceId] AND si.[IsDeleted] = 0
WHERE ou.[TenantId] = @TenantId AND ou.[IsDeleted] = 0
  AND (@StoreId IS NULL OR si.[StoreId] = @StoreId)
  AND ou.[UsedDate] >= @From AND ou.[UsedDate] < @To
GROUP BY o.[Name]
ORDER BY TotalDiscountGiven DESC;
```

---

### 8.11 تقرير المرتجعات (Returns Report)

**الغرض:** مرتجعات البيع والشراء، النسبة إلى إجمالي الحركة، وأكثر المنتجات إرجاعاً.
**الفلاتر:** التاريخ، الفرع، النوع (بيع/شراء)، المنتج، السبب.
**الأعمدة:** التاريخ، رقم المرتجع، الفاتورة الأصلية، المنتج، الكمية، القيمة، السبب.

```sql
SELECT sr.[ReturnDate], sr.[ReturnNumber], si.[InvoiceNumber] AS OriginalInvoice,
       p.[Name] AS ProductName, sri.[Quantity], sri.[LineNet] AS ReturnValue,
       sr.[Reason]
FROM [dbo].[SalesReturns] sr
JOIN [dbo].[SalesReturnItems] sri
     ON sri.[SalesReturnId] = sr.[Id] AND sri.[IsDeleted] = 0
JOIN [dbo].[Products] p ON p.[Id] = sri.[ProductId]
LEFT JOIN [dbo].[SalesInvoices] si ON si.[Id] = sr.[OriginalInvoiceId]
WHERE sr.[TenantId] = @TenantId AND sr.[IsDeleted] = 0
  AND (@StoreId IS NULL OR sr.[StoreId] = @StoreId)
  AND sr.[ReturnDate] >= @From AND sr.[ReturnDate] < @To
ORDER BY sr.[ReturnDate] DESC;
```

---

### 8.12 تقرير المستودعات (Warehouse Movement / Transfer Report)

**الغرض:** حركات المستودع (وارد، صادر، تحويلات بين الفروع) ورصيد كل مستودع.
**الفلاتر:** التاريخ، المستودع (المصدر/الوجهة)، المنتج، نوع الحركة.
**الأعمدة:** التاريخ، النوع، المنتج، من مستودع، إلى مستودع، الكمية، المرجع.

```sql
SELECT sm.[MovementDate], sm.[MovementType],
       p.[Name] AS ProductName,
       wf.[Name] AS FromWarehouse, wt.[Name] AS ToWarehouse,
       sm.[Quantity], sm.[ReferenceType], sm.[ReferenceId]
FROM [dbo].[StockMovements] sm
JOIN [dbo].[Products] p     ON p.[Id] = sm.[ProductId]
LEFT JOIN [dbo].[Warehouses] wf ON wf.[Id] = sm.[FromWarehouseId]
LEFT JOIN [dbo].[Warehouses] wt ON wt.[Id] = sm.[ToWarehouseId]
WHERE sm.[TenantId] = @TenantId AND sm.[IsDeleted] = 0
  AND (@WarehouseId IS NULL
       OR sm.[FromWarehouseId] = @WarehouseId
       OR sm.[ToWarehouseId]   = @WarehouseId)
  AND sm.[MovementDate] >= @From AND sm.[MovementDate] < @To
ORDER BY sm.[MovementDate] DESC;
```

---

### 8.13 أفضل/أسوأ المنتجات (Top / Worst Products)

**الغرض:** ترتيب المنتجات حسب الكمية أو الإيراد أو الربح، تصاعدياً أو تنازلياً.
**الفلاتر:** التاريخ، الفرع، التصنيف، المعيار (كمية/إيراد/ربح)، `TopN`.
**الأعمدة:** الترتيب، المنتج، الكمية، الإيراد، الربح.

```sql
SELECT TOP (@TopN)
    p.[Name] AS ProductName,
    SUM(sii.[Quantity]) AS QtySold,
    SUM(sii.[LineNet])  AS Revenue,
    SUM(sii.[LineNet] - sii.[Quantity]*sii.[CostAtSale]) AS Profit
FROM [dbo].[SalesInvoiceItems] sii
JOIN [dbo].[SalesInvoices] si ON si.[Id] = sii.[SalesInvoiceId]
     AND si.[IsDeleted] = 0 AND si.[Status] <> 'Cancelled'
JOIN [dbo].[Products] p ON p.[Id] = sii.[ProductId]
WHERE sii.[TenantId] = @TenantId AND sii.[IsDeleted] = 0
  AND (@StoreId IS NULL OR si.[StoreId] = @StoreId)
  AND si.[InvoiceDate] >= @From AND si.[InvoiceDate] < @To
GROUP BY p.[Name]
ORDER BY
  CASE WHEN @SortBy = 'QTY'     THEN SUM(sii.[Quantity]) END DESC,
  CASE WHEN @SortBy = 'REVENUE' THEN SUM(sii.[LineNet])  END DESC,
  CASE WHEN @SortBy = 'PROFIT'  THEN SUM(sii.[LineNet] - sii.[Quantity]*sii.[CostAtSale]) END DESC;
-- «الأسوأ» = نفس الاستعلام بـ ASC بدل DESC
```

---

### 8.14 أفضل العملاء (Best Customers)

**الغرض:** ترتيب العملاء حسب حجم الشراء أو الربح المحقّق منهم.
**الفلاتر:** التاريخ، الفرع، `TopN`.
**الأعمدة:** الترتيب، العميل، عدد الفواتير، إجمالي الشراء، متوسط الفاتورة، آخر شراء.

```sql
SELECT TOP (@TopN)
    c.[Name] AS CustomerName,
    COUNT(si.[Id])       AS InvoicesCount,
    SUM(si.[NetTotal])   AS TotalPurchases,
    AVG(si.[NetTotal])   AS AvgInvoiceValue,
    MAX(si.[InvoiceDate]) AS LastPurchase
FROM [dbo].[SalesInvoices] si
JOIN [dbo].[Customers] c ON c.[Id] = si.[CustomerId] AND c.[IsDeleted] = 0
WHERE si.[TenantId] = @TenantId AND si.[IsDeleted] = 0
  AND si.[Status] <> 'Cancelled'
  AND (@StoreId IS NULL OR si.[StoreId] = @StoreId)
  AND si.[InvoiceDate] >= @From AND si.[InvoiceDate] < @To
GROUP BY c.[Name]
ORDER BY TotalPurchases DESC;
```

---

## 9) جدول تعريف التقارير القابلة للإعداد (Report Definitions)

بعض التقارير قابلة للتخصيص (أعمدة، فلاتر افتراضية، صلاحية) لكل مستأجر دون تعديل الكود.

```sql
CREATE TABLE [dbo].[ReportDefinitions]
(
    [Code]           VARCHAR(50)   NOT NULL,   -- 'SALES_DETAIL', 'PROFIT_BY_PRODUCT'
    [Name]           NVARCHAR(150) NOT NULL,
    [Module]         VARCHAR(30)   NOT NULL,   -- 'Sales','Inventory','Financial'
    [RequiredPermission] VARCHAR(60) NOT NULL, -- 'Reports.Sales'
    [DefaultFilters] NVARCHAR(MAX) NULL
        CONSTRAINT CK_ReportDefinitions_Filters_Json
        CHECK ([DefaultFilters] IS NULL OR ISJSON([DefaultFilters]) = 1),
    [ColumnsConfig]  NVARCHAR(MAX) NULL         -- ترتيب/إظهار الأعمدة
        CONSTRAINT CK_ReportDefinitions_Columns_Json
        CHECK ([ColumnsConfig] IS NULL OR ISJSON([ColumnsConfig]) = 1),
    [IsEnabled]      BIT           NOT NULL CONSTRAINT DF_ReportDefinitions_Enabled DEFAULT (1),
    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_ReportDefinitions_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_ReportDefinitions_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,
    CONSTRAINT [PK_ReportDefinitions] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ReportDefinitions_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [UX_ReportDefinitions] UNIQUE ([TenantId], [Code])
);
GO
```

**جدول رفيق: قوالب الفلاتر المحفوظة للمستخدم** — `SavedReportTemplates` (TenantId, UserId, ReportCode, FiltersJson).

---

## 10) الـ API

جميع المسارات تحت `/api/v1/reports`، تتطلّب JWT صالح ويُستخرَج `TenantId` من الـ claims (لا يُمرَّر من العميل أبداً).

| Method | Endpoint | الوصف |
|--------|----------|-------|
| `POST` | `/reports/sales` | تقرير المبيعات (Body = الفلاتر + الترقيم) |
| `POST` | `/reports/purchase` | تقرير المشتريات |
| `POST` | `/reports/inventory` | تقرير المخزون |
| `POST` | `/reports/supplier/{id}/statement` | كشف حساب مورد |
| `POST` | `/reports/customer/{id}/statement` | كشف حساب عميل |
| `POST` | `/reports/cashflow` | التدفّق النقدي |
| `POST` | `/reports/profit` | تقرير الأرباح |
| `POST` | `/reports/tax` | تقرير الضرائب |
| `POST` | `/reports/returns` | المرتجعات |
| `POST` | `/reports/top-products` | أفضل/أسوأ المنتجات |
| `POST` | `/reports/best-customers` | أفضل العملاء |
| `POST` | `/reports/{code}/export?format=pdf\|xlsx` | تصدير |
| `GET`  | `/reports/definitions` | قائمة التقارير المتاحة للمستأجر |

**نموذج طلب (Request Body):**

```json
{
  "storeId": 12,
  "warehouseId": null,
  "dateFrom": "2026-07-01",
  "dateTo": "2026-07-31",
  "categoryId": null,
  "groupBy": "Day",
  "sortBy": "REVENUE",
  "topN": 20,
  "pageNumber": 1,
  "pageSize": 50
}
```

**نموذج استجابة (Response):**

```json
{
  "summary": { "invoicesCount": 842, "totalNet": 154320.5000, "totalTax": 20040.0000 },
  "rows": [ { "invoiceNumber": "INV-000842", "netTotal": 210.7500 } ],
  "pagination": { "pageNumber": 1, "pageSize": 50, "totalRows": 842 }
}
```

---

## 11) التصدير والطباعة (Export & Print)

| المخرَج | المكتبة | ملاحظات |
|---------|---------|---------|
| **PDF** | **QuestPDF** | يُولَّد على الخادم؛ ترويسة المستأجر (شعار/عنوان/ضريبة) من الإعدادات؛ يدعم RTL |
| **Excel** | **ClosedXML** | ورقة بيانات خام + ورقة ملخّص؛ صيغ مالية `#,##0.0000` |
| **الطباعة** | نسخة HTML مخصّصة | `@media print` تُخفي الفلاتر والأزرار؛ قالب طباعة المستأجر |

- التصدير يعيد استخدام **نفس منطق الاستعلام** (لا تكرار)؛ يُنفَّذ عبر Hangfire للتقارير الكبيرة ثم يُرسَل رابط تنزيل.
- كل تصدير يُسجَّل في `AuditLogs` (من، ماذا، متى، عدد الصفوف).

---

## 12) مخطّط التدفّق (Flow Chart)

```
[المستخدم يختار تقريراً]
        │
        ▼
[يضبط الفلاتر ويضغط «عرض»]
        │
        ▼
[Controller: التحقق من الصلاحية] ──(مرفوض)──► 403
        │ (مصرّح)
        ▼
[حقن TenantId + StoreId من الـ Claims قسراً]
        │
        ▼
[Validation: التواريخ، المدى، PageSize] ──(خطأ)──► 400 + رسالة
        │ (صالح)
        ▼
[تنفيذ الاستعلام (RCSI, بارامترات، فهرس التغطية)]
        │
        ├──► [Summary KPIs]
        └──► [Rows (OFFSET/FETCH)]
        │
        ▼
[إرجاع JSON] ──► [DataTables/Chart.js يعرض]
        │
        ▼
[تصدير؟] ──(نعم)──► [QuestPDF/ClosedXML] ──► [Audit Log] ──► [تنزيل]
```

---

## 13) سجل التدقيق (Audit Log)

| الحدث | ما يُسجَّل |
|-------|-----------|
| فتح تقرير مالي حسّاس (أرباح/ضرائب) | UserId, ReportCode, Filters, TenantId, IP, Timestamp |
| تصدير PDF/Excel | نفس ما سبق + Format + RowCount |
| حفظ/تعديل قالب | القالب قبل/بعد |
| محاولة وصول مرفوضة (403) | المستخدم، التقرير، الفرع المطلوب |

تُخزَّن في `AuditLogs` (Append-only). التقارير لا تعدّل بيانات، لذا لا Audit على «تعديل/حذف بيانات» — فقط على **الوصول والتصدير**.

---

## 14) الأخطاء المحتملة (Possible Errors)

| الخطأ | السبب | المعالجة |
|-------|-------|----------|
| `400 InvalidDateRange` | البداية بعد النهاية أو المدى ضخم | رسالة واضحة + قصّ تلقائي حيث يمكن |
| `403 StoreForbidden` | طلب فرع خارج نطاق الصلاحية | يُحقن فرع المستخدم قسراً، ويُسجَّل |
| `413 TooManyRows` | نتائج ضخمة للتصدير المباشر | تحويل لتصدير خلفي (Hangfire) |
| `504 QueryTimeout` | استعلام تفصيلي على مدى كبير | اقتراح تقرير مجمّع أو تضييق المدى |
| `409 StaleTemplate` | تعارض تعديل قالب (ConcurrencyStamp) | إعادة تحميل القالب |

---

## 15) الأداء (Performance)

استراتيجيات إلزامية للتقارير الثقيلة (وفق [29-Performance.md](29-Performance.md)):

1. **الترقيم من الخادم (Server-Side Pagination):** `OFFSET/FETCH` دائماً؛ لا يُعاد أكثر من `PageSize` صفّاً للواجهة.
2. **فهارس التغطية (Covering Indexes):** لكل تقرير مسار حرج، فهرس يبدأ بـ `TenantId` ويشمل أعمدة الفلترة والإخراج عبر `INCLUDE`.
   ```sql
   CREATE NONCLUSTERED INDEX IX_SalesInvoices_Report
     ON dbo.SalesInvoices (TenantId, StoreId, InvoiceDate)
     INCLUDE (NetTotal, TaxAmount, DiscountAmount, CashierId, CustomerId, Status)
     WHERE IsDeleted = 0;
   ```
3. **التجميع المسبق (Pre-Aggregation):** جدول ملخّص يومي `DailySalesSummary` (TenantId, StoreId, Day, ProductId, Qty, Revenue, COGS) يُحدَّث عبر Hangfire ليلاً؛ تقارير الاتجاهات الطويلة تقرأ منه لا من الحركات الخام.
4. **Indexed Views** حيث يسمح المنطق (مثل إجماليات المبيعات لكل يوم/فرع) لتسريع التجميعات المتكرّرة.
5. **`OPTION (RECOMPILE)`** للاستعلامات ذات الفلاتر الاختيارية الكثيرة (تفادي Parameter Sniffing).
6. **`READ COMMITTED SNAPSHOT`** لتفادي حجب عمليات البيع أثناء التقارير الثقيلة.
7. **Caching** لنتائج التقارير المجمّعة قليلة التغيّر (Redis لاحقاً، مفتاح يتضمّن TenantId + hash الفلاتر).
8. **تحديد المدى:** التقارير التفصيلية مقيّدة بمدى أقصى (≤ 366 يوماً)؛ ما فوق ذلك يُوجَّه للتقارير المجمّعة.

---

## 16) الأمان (Security)

1. **العزل القسري:** `TenantId` من JWT claims حصراً، لا من جسم الطلب؛ `StoreId` يُقيَّد بصلاحية المستخدم.
2. **بارامترات مُعامَلة (Parameterized):** لا تركيب SQL نصّي — كل الفلاتر بارامترات؛ حماية من SQL Injection.
3. **RLS (اختياري):** طبقة دفاع ثانية على مستوى SQL Server تفرض `TenantId` حتى لو أخطأ الكود.
4. **صلاحيات دقيقة:** كل تقرير محمي بـ Permission مستقلّ؛ التقارير المالية مفصولة عن التشغيلية.
5. **تدقيق الوصول:** فتح وتصدير التقارير المالية يُسجَّل بالكامل.
6. **حدّ المعدّل (Rate Limiting):** على التصدير الثقيل لمنع استنزاف الخادم.
7. **إخفاء المعرّفات الحسّاسة:** تُعرَض أرقام المستندات (InvoiceNumber) لا `Id` الداخلي في المخرجات القابلة للتصدير.

---

_يلتزم هذا الملف بمعايير [04-Database-Design.md](04-Database-Design.md). لوحة التحكّم التي تلخّص هذه التقارير لحظياً موثّقة في [21-Dashboard.md](21-Dashboard.md)._
