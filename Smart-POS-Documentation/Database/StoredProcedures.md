# Database — Stored Procedures (الإجراءات المخزّنة)

> فلسفة استخدام Stored Procedures في **Smart ERP POS** وأمثلة فعلية للحالات المبرَّرة فقط. المبدأ الحاكم: **EF Core أولاً، وSP عند الحاجة الحقيقية**.

---

## 1) متى نستخدم SP ومتى لا (Decision Matrix)

| الحالة | القرار | السبب |
|--------|--------|-------|
| CRUD عادي | ❌ EF Core | الـ ORM أوضح، اختبارات أسهل، أقل صيانة |
| منطق أعمال | ❌ في طبقة Domain (C#) | يبقى المنطق قابلاً للاختبار والنسخ |
| **ترقيم مستند ذرّي** | ✅ SP | يتطلّب `UPDATE ... OUTPUT` ذرّي لمنع التعارض |
| **تقرير مجمّع ثقيل** | ✅ SP | تجميعات معقّدة + خطة تنفيذ مُحسَّنة + معاملات كبيرة |
| **إعادة حساب رصيد** | ✅ SP | حلقة على حركات كثيرة داخل معاملة واحدة، أداء حرج |
| Bulk operations | ✅ SP / TVP | تجنّب round-trips متعدّدة |
| عمليات عبر جداول كثيرة بأداء حرج | ✅ SP | تقليل حركة البيانات بين الطبقات |

> **القاعدة:** لا SP إلا إذا كان EF Core غير كافٍ لسبب **أداء** أو **ذرّية** محدَّد. كل SP يجب أن يبرَّر.

---

## 2) SP — توليد رقم مستند ذرّي (Atomic Document Numbering)

> يقرأ ويزيد `Sequences.NextValue` **ذرّياً** عبر `UPDATE ... OUTPUT` — يمنع منح رقمين متطابقين تحت التزامن العالي (سيناريو POS). يُستدعى داخل معاملة الفاتورة.

```sql
CREATE OR ALTER PROCEDURE [dbo].[sp_GetNextDocumentNumber]
    @TenantId   BIGINT,
    @StoreId    BIGINT       = NULL,
    @DocType    VARCHAR(30),                 -- 'SALES_INVOICE', 'PURCHASE_INVOICE'
    @FullNumber NVARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Next BIGINT, @Prefix VARCHAR(10), @Padding TINYINT;

    -- UPDATE ذرّي: يقرأ القيمة الحالية ويزيدها في عملية واحدة (بلا سباق)
    UPDATE s
        SET @Next    = s.[NextValue],
            @Prefix  = ISNULL(s.[Prefix], ''),
            @Padding = s.[Padding],
            s.[NextValue] = s.[NextValue] + 1
    FROM [dbo].[Sequences] s WITH (UPDLOCK, ROWLOCK)
    WHERE s.[TenantId] = @TenantId
      AND (s.[StoreId] = @StoreId OR (@StoreId IS NULL AND s.[StoreId] IS NULL))
      AND s.[DocType]  = @DocType;

    IF @Next IS NULL
    BEGIN
        -- إنشاء التسلسل تلقائياً عند أول استخدام
        INSERT INTO [dbo].[Sequences] ([TenantId], [StoreId], [DocType], [NextValue])
        VALUES (@TenantId, @StoreId, @DocType, 2);
        SET @Next = 1; SET @Prefix = ''; SET @Padding = 6;
    END

    -- تنسيق: INV-000123
    SET @FullNumber = @Prefix + RIGHT(REPLICATE('0', @Padding)
                      + CAST(@Next AS VARCHAR(20)), @Padding);
END
GO
```

**الاستدعاء من EF Core:**
```csharp
var p = new SqlParameter("@FullNumber", SqlDbType.NVarChar, 50)
        { Direction = ParameterDirection.Output };
await db.Database.ExecuteSqlRawAsync(
    "EXEC sp_GetNextDocumentNumber @TenantId, @StoreId, @DocType, @FullNumber OUTPUT",
    tenantId, storeId, "SALES_INVOICE", p);
var invoiceNumber = (string)p.Value;
```

---

## 3) SP — تقرير مبيعات مجمّع (Aggregated Sales Report)

> تجميع ثقيل عبر ملايين الصفوف لفترة زمنية. يُنفَّذ كـ SP لخطة تنفيذ ثابتة ومُحسَّنة وتقليل حركة البيانات.

```sql
CREATE OR ALTER PROCEDURE [dbo].[sp_SalesReport_Summary]
    @TenantId  BIGINT,
    @StoreId   BIGINT       = NULL,
    @FromDate  DATETIME2(3),
    @ToDate    DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CAST(si.[CreatedDate] AS DATE)          AS [SaleDate],
        COUNT(DISTINCT si.[Id])                 AS [InvoiceCount],
        SUM(si.[SubTotal])                      AS [GrossSales],
        SUM(si.[DiscountAmount])                AS [TotalDiscount],
        SUM(si.[TaxAmount])                     AS [TotalTax],
        SUM(si.[Total])                         AS [NetSales],
        SUM(sii.[Qty] * (sii.[UnitPrice]
             - ISNULL(p.[CostPrice], 0)))       AS [GrossProfit]
    FROM [dbo].[SalesInvoices]     si
    JOIN [dbo].[SalesInvoiceItems] sii ON sii.[SalesInvoiceId] = si.[Id]
                                       AND sii.[IsDeleted] = 0
    LEFT JOIN [dbo].[Products]     p   ON p.[Id] = sii.[ProductId]
    WHERE si.[TenantId]    = @TenantId
      AND (@StoreId IS NULL OR si.[StoreId] = @StoreId)
      AND si.[IsDeleted]   = 0
      AND si.[Status]      = 'Completed'
      AND si.[CreatedDate] >= @FromDate
      AND si.[CreatedDate] <  @ToDate
    GROUP BY CAST(si.[CreatedDate] AS DATE)
    ORDER BY [SaleDate];
END
GO
```

> يعتمد على الفهرس `IX_SalesInvoices_Tenant_Store_Date` (Covering) الموثّق في [Indexes.md](Indexes.md#5).

---

## 4) SP — إعادة حساب رصيد المخزون (Stock Recalculation)

> يُستخدم للتصحيح/الجرد: يعيد بناء `Stock.QtyOnHand` من مجموع `StockMovements` (المصدر الحقيقي). عملية ثقيلة تُغلَّف في معاملة واحدة ذرّية.

```sql
CREATE OR ALTER PROCEDURE [dbo].[sp_RecalculateStock]
    @TenantId    BIGINT,
    @WarehouseId BIGINT = NULL,
    @ProductId   BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    ;WITH MovementTotals AS (
        SELECT sm.[StockId],
               SUM(CASE WHEN sm.[MovementType] IN ('In','Adjust','TransferIn')
                        THEN sm.[Qty]
                        WHEN sm.[MovementType] IN ('Out','TransferOut')
                        THEN -sm.[Qty] ELSE 0 END) AS [Balance]
        FROM [dbo].[StockMovements] sm
        WHERE sm.[TenantId] = @TenantId
        GROUP BY sm.[StockId]
    )
    UPDATE s
        SET s.[QtyOnHand]    = ISNULL(mt.[Balance], 0),
            s.[ModifiedDate] = SYSUTCDATETIME()
    FROM [dbo].[Stock] s
    LEFT JOIN MovementTotals mt ON mt.[StockId] = s.[Id]
    WHERE s.[TenantId] = @TenantId
      AND s.[IsDeleted] = 0
      AND (@WarehouseId IS NULL OR s.[WarehouseId] = @WarehouseId)
      AND (@ProductId   IS NULL OR s.[ProductId]   = @ProductId);

    COMMIT TRANSACTION;

    SELECT @@ROWCOUNT AS [RowsRecalculated];
END
GO
```

---

## 5) قواعد كتابة SP (SP Coding Standards)

| القاعدة | التفصيل |
|---------|---------|
| `SET NOCOUNT ON;` | في بداية كل SP (يقلّل حركة الشبكة) |
| `SET XACT_ABORT ON;` | مع كل SP يستخدم معاملة (تراجع تلقائي عند الخطأ) |
| `@TenantId` أول باراميتر | العزل إجباري — لا SP بلا فلترة مستأجر |
| `CREATE OR ALTER` | للنشر التكراري (Idempotent migrations) |
| بادئة `sp_` مخصّصة | مقبولة داخلياً (ليست `sp_` النظامية في master) |
| المعاملات | `TRY...CATCH` + `ROLLBACK` عند الخطأ |
| بلا منطق أعمال معقّد | المنطق يبقى في C# قدر الإمكان |

---

## 6) النشر والإصدار (Deployment & Versioning)

- كل SP يُخزَّن في ملف SQL منفصل داخل `/Database/Procedures/` في مستودع الكود.
- تُطبَّق عبر **EF Core Migrations** (`migrationBuilder.Sql(...)`) أو أداة `DbUp`.
- `CREATE OR ALTER` يجعل النشر آمناً للتكرار (Idempotent).
- تُختبَر عبر Integration Tests على قاعدة بيانات LocalDB/Testcontainers.

---

## 7) ما لا يُكتب كـ SP أبداً (Anti-Patterns)

| ممنوع | البديل |
|-------|--------|
| منطق التسعير والخصومات | Domain Service في C# |
| التحقق من الصلاحيات (Authorization) | Middleware + Policy في .NET |
| منطق تطبيق العروض | Domain / MediatR Handler |
| CRUD بسيط | EF Core مباشرةً |
| إرسال إشعارات/بريد | Hangfire Background Job |

> **الخلاصة:** SP أداة أداء وذرّية للحالات الثقيلة فقط — لا مستودع لمنطق الأعمال. المنطق يبقى في الكود ليكون قابلاً للاختبار والصيانة والنسخ عبر البيئات.
