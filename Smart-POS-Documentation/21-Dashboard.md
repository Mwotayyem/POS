# 21 — Dashboard (لوحة التحكّم)

> لوحة التحكّم هي **الشاشة الأولى** بعد تسجيل الدخول: تلخّص صحّة العمل في لمحة عبر مؤشّرات أداء (KPIs) ومخطّطات (Chart.js). التزم بمعايير [04-Database-Design.md](04-Database-Design.md): فلترة `TenantId` إجبارية، أموال `DECIMAL(18,4)`، تواريخ UTC. المبدأ الحاكم للأداء: **لا نحسب كل شيء لحظياً** — نعتمد التجميع المسبق والـ caching، ونستخدم SignalR للتحديث اللحظي المستهدف فقط.

---

## 1) الهدف (Purpose)

تقديم صورة تنفيذية فورية لمالك/مدير المتجر: مبيعات اليوم/الأمس/الشهر، صافي وإجمالي الربح، الأكثر مبيعاً، البطيء والراكد والمنتهي، تنبيهات المخزون المنخفض، والمستحقات المعلّقة للموردين والعملاء — مع مخطّطات اتجاه للمبيعات والمخزون والأرباح، وآلية تحديث فعّالة لا تُثقل قاعدة البيانات.

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية | المحتوى المرئي | الأدوار |
|----------|----------------|---------|
| `Dashboard.View` | فتح اللوحة | كل الأدوار الإدارية |
| `Dashboard.Financial` | بطاقات الربح، المستحقات، التدفّق | Owner, Accountant |
| `Dashboard.AllStores` | تجميع كل الفروع | Owner, Accountant |
| (بلا Financial) | يرى المبيعات وتنبيهات المخزون فقط | StoreManager, Cashier |

**قاعدة العزل:** غير مالك `Dashboard.AllStores` يرى فرعه فقط؛ `StoreId` يُحقن قسراً على الخادم. بطاقات الربح والمستحقات تُخفى تماماً لمن لا يملك `Dashboard.Financial`.

---

## 3) تصميم الصفحة (Widgets Grid Layout)

```
┌───────────────────────────────────────────────────────────────────────┐
│  لوحة التحكّم        [الفرع▼] [الفترة: اليوم/الأسبوع/الشهر▼]  🔄 آخر تحديث│
├──────────────┬──────────────┬──────────────┬───────────────────────────┤
│ 💰 مبيعات اليوم│ 📊 مبيعات الشهر│ 📈 صافي الربح │ ⚠️ مخزون منخفض           │
│  12,450.00    │  318,900.00   │  47,120.00   │  18 صنف                  │
│  ▲ 8% عن أمس  │  ▲ 3% عن سابقه│  هامش 14.8%  │  [عرض القائمة]           │
├──────────────┴──────────────┴──────────────┴───────────────────────────┤
│  📈 اتجاه المبيعات (آخر 30 يوماً)          │  🥧 المبيعات حسب التصنيف    │
│  [Line Chart — Chart.js]                    │  [Doughnut Chart]           │
├─────────────────────────────────────────────┼─────────────────────────────┤
│  🏆 الأكثر مبيعاً (Top 5)                    │  🐢 بطيء الحركة / راكد      │
│  [Bar/List]                                 │  [List]                     │
├─────────────────────────────────────────────┼─────────────────────────────┤
│  💵 مستحقات على العملاء   [إجمالي + قائمة]  │  🧾 مستحقات للموردين        │
├─────────────────────────────────────────────┴─────────────────────────────┤
│  ⏳ منتهي الصلاحية قريباً   |   ☠️ مخزون ميت (Dead Stock)                 │
└────────────────────────────────────────────────────────────────────────────┘
```

**المبادئ:** شبكة Widgets متجاوبة (Bootstrap grid)، RTL، كل بطاقة مكوّن مستقلّ يحمّل بياناته عبر AJAX، والمخطّطات Chart.js تُهيّأ بعد وصول البيانات.

---

## 4) الأزرار (Buttons)

| الزر | الوظيفة |
|------|---------|
| **تبديل الفرع** | إعادة تحميل كل الـ Widgets للفرع المختار (ضمن الصلاحية) |
| **الفترة (اليوم/الأسبوع/الشهر)** | تغيير مدى المخطّطات والبطاقات |
| **🔄 تحديث** | إعادة جلب فوري (يتجاوز الـ cache) |
| **عرض القائمة** (على كل تنبيه) | ينقل للتقرير التفصيلي في [20-Reports.md](20-Reports.md) |

---

## 5) الحقول (المؤشّرات المعروضة)

كل Widget يعرض: القيمة الحالية، مؤشّر التغيّر (▲/▼ + نسبة)، ورابط للتفصيل. الفلاتر الفعّالة: `StoreId`، الفترة، توقيت المستأجر للتجميع اليومي.

---

## 6) التحقق (Validation)

| القاعدة | السلوك |
|---------|--------|
| `StoreId` ضمن صلاحية المستخدم | خلاف ذلك يُحقن فرعه قسراً |
| الفترة ضمن قيم مسموحة (Today/Week/Month) | القيمة غير الصالحة تُرجَع للافتراضي (Today) |
| عرض بطاقات مالية | فقط عند وجود `Dashboard.Financial` |

---

## 7) قواعد العمل (Business Rules)

1. **مبيعات اليوم** تعتمد **توقيت المستأجر** لحدود اليوم (تخزين UTC + `AT TIME ZONE`)، لا توقيت الخادم.
2. **الفواتير الملغاة** لا تُحتسب في أي KPI للإيراد.
3. **صافي الربح** يستخدم `CostAtSale` (لقطة التكلفة لحظة البيع) لا التكلفة الحالية.
4. **البيانات المجمّعة** تُقرأ من جدول الملخّص اليومي؛ **بيانات اليوم الجاري فقط** تُحسب لحظياً من الحركات الخام (Hybrid).
5. **التحديث اللحظي** عبر SignalR مقتصر على مؤشّرات اليوم الجاري (بيع جديد → تحديث بطاقة «مبيعات اليوم») لا على المخطّطات التاريخية.

---

## 8) مؤشّرات الأداء الرئيسية (KPIs — التعريف والمعادلة والاستعلام)

كل استعلام يفترض `@TenantId`, `@StoreId` (أو `NULL`), وحدود اليوم بتوقيت المستأجر: `@TodayStartUtc`, `@TodayEndUtc`.

### 8.1 مبيعات اليوم / الأمس (Today's / Yesterday's Sales)

**التعريف:** إجمالي الإيراد الصافي للفواتير غير الملغاة اليوم مقابل الأمس.
**المعادلة:** `Σ NetTotal` ضمن حدود اليوم.

```sql
SELECT
  SUM(CASE WHEN si.[InvoiceDate] >= @TodayStartUtc THEN si.[NetTotal] ELSE 0 END) AS TodaySales,
  SUM(CASE WHEN si.[InvoiceDate] >= @YesterdayStartUtc
            AND si.[InvoiceDate] <  @TodayStartUtc THEN si.[NetTotal] ELSE 0 END) AS YesterdaySales
FROM [dbo].[SalesInvoices] si
WHERE si.[TenantId] = @TenantId AND si.[IsDeleted] = 0
  AND si.[Status] <> 'Cancelled'
  AND (@StoreId IS NULL OR si.[StoreId] = @StoreId)
  AND si.[InvoiceDate] >= @YesterdayStartUtc AND si.[InvoiceDate] < @TodayEndUtc;
```

### 8.2 مبيعات الشهر (Monthly Sales)

**التعريف:** إجمالي إيراد الشهر الحالي مقابل السابق (للاتجاه).
**المصدر:** جدول الملخّص اليومي `DailySalesSummary` (أداء أعلى).

```sql
SELECT SUM([Revenue]) AS MonthSales
FROM [dbo].[DailySalesSummary]
WHERE [TenantId] = @TenantId
  AND (@StoreId IS NULL OR [StoreId] = @StoreId)
  AND [SummaryDate] >= @MonthStart AND [SummaryDate] < @MonthEnd;
```

### 8.3 صافي / إجمالي الربح (Net / Gross Profit)

**التعريف:** إجمالي الربح = الإيراد − COGS. صافي الربح = إجمالي الربح − المصروفات التشغيلية.
**المعادلة:** `GrossProfit = Σ(LineNet − Qty×CostAtSale)`؛ `NetProfit = GrossProfit − Expenses`.

```sql
SELECT
  SUM(sii.[LineNet])                                   AS Revenue,
  SUM(sii.[Quantity] * sii.[CostAtSale])               AS COGS,
  SUM(sii.[LineNet] - sii.[Quantity]*sii.[CostAtSale]) AS GrossProfit
FROM [dbo].[SalesInvoiceItems] sii
JOIN [dbo].[SalesInvoices] si ON si.[Id] = sii.[SalesInvoiceId]
     AND si.[IsDeleted] = 0 AND si.[Status] <> 'Cancelled'
WHERE sii.[TenantId] = @TenantId AND sii.[IsDeleted] = 0
  AND (@StoreId IS NULL OR si.[StoreId] = @StoreId)
  AND si.[InvoiceDate] >= @MonthStartUtc AND si.[InvoiceDate] < @MonthEndUtc;
-- صافي الربح = GrossProfit - (SELECT SUM(Amount) FROM Expenses ضمن نفس الفترة)
```

### 8.4 الأكثر مبيعاً (Top Selling)

**التعريف:** أعلى 5 منتجات بالكمية أو الإيراد ضمن الفترة.

```sql
SELECT TOP (5) p.[Name] AS ProductName,
       SUM(s.[Qty]) AS QtySold, SUM(s.[Revenue]) AS Revenue
FROM [dbo].[DailySalesSummary] s
JOIN [dbo].[Products] p ON p.[Id] = s.[ProductId]
WHERE s.[TenantId] = @TenantId
  AND (@StoreId IS NULL OR s.[StoreId] = @StoreId)
  AND s.[SummaryDate] >= @MonthStart AND s.[SummaryDate] < @MonthEnd
GROUP BY p.[Name]
ORDER BY QtySold DESC;
```

### 8.5 بطيء الحركة (Slow Moving)

**التعريف:** منتجات لها رصيد لكن مبيعات ضعيفة خلال آخر N يوماً (مثلاً 60).

```sql
SELECT p.[Name], st.[Quantity] AS OnHand,
       ISNULL(sold.QtySold, 0) AS SoldLast60Days
FROM [dbo].[Stock] st
JOIN [dbo].[Products] p ON p.[Id] = st.[ProductId] AND p.[IsDeleted] = 0
LEFT JOIN (
    SELECT s.[ProductId], SUM(s.[Qty]) AS QtySold
    FROM [dbo].[DailySalesSummary] s
    WHERE s.[TenantId] = @TenantId AND s.[SummaryDate] >= DATEADD(DAY,-60,@Today)
    GROUP BY s.[ProductId]
) sold ON sold.[ProductId] = p.[Id]
WHERE st.[TenantId] = @TenantId AND st.[IsDeleted] = 0
  AND st.[Quantity] > 0
  AND ISNULL(sold.QtySold,0) < @SlowThreshold
ORDER BY SoldLast60Days ASC;
```

### 8.6 المخزون الميت (Dead Stock)

**التعريف:** منتجات لها رصيد ولم تُبَع إطلاقاً خلال آخر N يوماً (مثلاً 90).

```sql
SELECT p.[Name], st.[Quantity],
       (st.[Quantity]*st.[AverageCost]) AS TiedUpCapital
FROM [dbo].[Stock] st
JOIN [dbo].[Products] p ON p.[Id] = st.[ProductId] AND p.[IsDeleted] = 0
WHERE st.[TenantId] = @TenantId AND st.[IsDeleted] = 0 AND st.[Quantity] > 0
  AND NOT EXISTS (
      SELECT 1 FROM [dbo].[DailySalesSummary] s
      WHERE s.[TenantId] = @TenantId AND s.[ProductId] = p.[Id]
        AND s.[SummaryDate] >= DATEADD(DAY,-90,@Today)
  )
ORDER BY TiedUpCapital DESC;
```

### 8.7 منتهي الصلاحية قريباً (Expiring Soon)

**التعريف:** دفعات (Batches) تنتهي خلال N يوماً ولها رصيد.

```sql
SELECT p.[Name], b.[BatchNumber], b.[ExpiryDate], b.[Quantity],
       DATEDIFF(DAY, @Today, b.[ExpiryDate]) AS DaysToExpiry
FROM [dbo].[ProductBatches] b
JOIN [dbo].[Products] p ON p.[Id] = b.[ProductId]
WHERE b.[TenantId] = @TenantId AND b.[IsDeleted] = 0
  AND b.[Quantity] > 0
  AND b.[ExpiryDate] <= DATEADD(DAY, @ExpiryWindowDays, @Today)
  AND (@StoreId IS NULL OR b.[StoreId] = @StoreId)
ORDER BY b.[ExpiryDate] ASC;
```

### 8.8 مخزون منخفض (Low Stock)

**التعريف:** أصناف كميتها ≤ حدّ إعادة الطلب.

```sql
SELECT COUNT(*) AS LowStockCount
FROM [dbo].[Stock] st
JOIN [dbo].[Products] p ON p.[Id] = st.[ProductId] AND p.[IsDeleted] = 0
WHERE st.[TenantId] = @TenantId AND st.[IsDeleted] = 0
  AND (@StoreId IS NULL OR st.[WarehouseId] IN
       (SELECT [Id] FROM [dbo].[Warehouses] WHERE [StoreId] = @StoreId))
  AND st.[Quantity] <= p.[ReorderLevel];
```

### 8.9 مستحقات معلّقة (Pending Supplier / Customer Payments)

**التعريف:** إجمالي الرصيد الآجل غير المسدَّد للموردين (دائن) والعملاء (مدين).

```sql
-- مستحقات على العملاء (Accounts Receivable)
SELECT SUM(si.[NetTotal] - si.[PaidAmount]) AS TotalReceivable
FROM [dbo].[SalesInvoices] si
WHERE si.[TenantId] = @TenantId AND si.[IsDeleted] = 0
  AND si.[Status] <> 'Cancelled' AND si.[NetTotal] > si.[PaidAmount]
  AND (@StoreId IS NULL OR si.[StoreId] = @StoreId);

-- مستحقات للموردين (Accounts Payable)
SELECT SUM(pi.[NetTotal] - pi.[PaidAmount]) AS TotalPayable
FROM [dbo].[PurchaseInvoices] pi
WHERE pi.[TenantId] = @TenantId AND pi.[IsDeleted] = 0
  AND pi.[NetTotal] > pi.[PaidAmount]
  AND (@StoreId IS NULL OR pi.[StoreId] = @StoreId);
```

---

## 9) المخطّطات (Charts — Chart.js)

| المخطّط | النوع | البيانات | التحديث |
|---------|-------|----------|---------|
| اتجاه المبيعات (30 يوماً) | `line` | من `DailySalesSummary` | يومي (cache) + نقطة اليوم لحظياً |
| المبيعات حسب التصنيف | `doughnut` | تجميع الإيراد بالتصنيف | كل تحديث cache |
| اتجاه الربح | `bar` | Revenue vs COGS شهرياً | يومي |
| حركة المخزون | `line` | وارد/صادر أسبوعياً | يومي |

**نموذج تهيئة Chart.js (اتجاه المبيعات):**

```javascript
const salesTrend = new Chart(document.getElementById('salesTrendChart'), {
  type: 'line',
  data: {
    labels: model.days,                 // ['01', '02', ...] بتوقيت المستأجر
    datasets: [{
      label: 'المبيعات',
      data: model.revenue,              // DECIMAL(18,4) من الخادم
      borderColor: '#2563eb',
      backgroundColor: 'rgba(37,99,235,.12)',
      fill: true, tension: 0.3
    }]
  },
  options: {
    responsive: true,
    plugins: { legend: { position: 'top' } },
    scales: { y: { beginAtZero: true } }
  }
});
```

---

## 10) الـ API

| Method | Endpoint | الوصف |
|--------|----------|-------|
| `GET` | `/api/v1/dashboard/summary?storeId=&period=` | كل بطاقات الـ KPI دفعة واحدة |
| `GET` | `/api/v1/dashboard/sales-trend?days=30` | بيانات مخطّط المبيعات |
| `GET` | `/api/v1/dashboard/alerts` | منخفض/منتهي/راكد/ميت |
| `GET` | `/api/v1/dashboard/receivables-payables` | المستحقات (يتطلّب Financial) |
| Hub | `/hubs/dashboard` (SignalR) | دفع تحديثات اليوم اللحظية |

**نموذج استجابة الملخّص:**

```json
{
  "todaySales": 12450.0000, "yesterdaySales": 11520.0000, "salesDeltaPct": 8.07,
  "monthSales": 318900.0000, "grossProfit": 47120.0000, "grossMarginPct": 14.8,
  "lowStockCount": 18, "expiringSoonCount": 6,
  "receivable": 22400.0000, "payable": 31850.0000,
  "asOfUtc": "2026-07-13T09:15:00Z"
}
```

---

## 11) آلية التحديث (Refresh / Caching / SignalR)

استراتيجية هجينة توازن بين اللحظية والأداء:

| الطبقة | التقنية | الفترة |
|--------|---------|--------|
| **التجميع المسبق** | Hangfire Job يبني `DailySalesSummary` ليلاً + كل ساعة لليوم الجاري | ليلي/ساعي |
| **Caching** | نتائج الملخّص المجمّع تُخزَّن (MemoryCache الآن، Redis لاحقاً) بمفتاح `Tenant:{id}:Store:{id}:Dashboard` | TTL 60–300 ثانية |
| **التحديث اللحظي** | **SignalR**: عند اكتمال بيع/مرتجع، يُبثّ حدث `SalesUpdated` لمجموعة المستأجر/الفرع فيُحدَّث «مبيعات اليوم» وعدّاد الفواتير دون إعادة تحميل | فوري |
| **التحديث اليدوي** | زر 🔄 يتجاوز الـ cache | عند الطلب |

**قاعدة حاكمة:** المخطّطات التاريخية **لا** تُحدَّث لحظياً عبر SignalR (مكلف)؛ تُحدَّث نقطة «اليوم» فقط. باقي المخطّط من الـ cache.

```csharp
// عند اكتمال معاملة بيع (بعد Commit)
await _hub.Clients
    .Group($"tenant-{tenantId}-store-{storeId}")
    .SendAsync("SalesUpdated", new { newTodaySales, invoiceCount });
```

---

## 12) مخطّط التدفّق (Flow Chart)

```
[تسجيل الدخول ناجح]
        │
        ▼
[تحميل اللوحة] ──► [GET /dashboard/summary]
        │
        ▼
[الخادم: TenantId + StoreId من Claims قسراً]
        │
        ▼
[Cache موجود وحيّ؟] ──(نعم)──► [إرجاع من الـ Cache] ─┐
        │ (لا)                                        │
        ▼                                             │
[قراءة المجمّع من DailySalesSummary]                  │
[+ حساب اليوم الجاري لحظياً من الحركات]                │
        │                                             │
        ▼                                             ▼
[تخزين في الـ Cache] ──────────────────────► [عرض الـ Widgets + Chart.js]
        │
        ▼
[الاشتراك في SignalR hub للفرع]
        │
        ▼
[بيع جديد؟] ──► [حدث SalesUpdated] ──► [تحديث بطاقة «اليوم» فقط]
```

---

## 13) سجل التدقيق (Audit Log)

| الحدث | ما يُسجَّل |
|-------|-----------|
| عرض بطاقات مالية (ربح/مستحقات) | UserId, StoreId, TenantId, Timestamp |
| تبديل لعرض «كل الفروع» | المستخدم، الوقت |
| محاولة وصول لفرع خارج الصلاحية | تُرفض وتُسجَّل |

اللوحة قراءة فقط — لا Audit على تعديل بيانات؛ فقط على **الوصول للمعلومة المالية الحسّاسة**.

---

## 14) الأخطاء المحتملة (Possible Errors)

| الخطأ | السبب | المعالجة |
|-------|-------|----------|
| Widget فارغ / بلا بيانات | لا حركة في الفترة | عرض «لا توجد بيانات» بدل خطأ |
| بطء أول تحميل | Cache بارد + مدى كبير | التجميع المسبق يمنعه؛ مؤشّر تحميل لكل Widget |
| فرق بين اللوحة والتقرير | اللوحة مجمّعة/مخزّنة مؤقتاً | زر 🔄 لإعادة الحساب الفوري |
| انقطاع SignalR | الشبكة | العودة لـ polling كل 60 ثانية تلقائياً |
| `403` على بطاقة مالية | لا `Dashboard.Financial` | تُخفى البطاقة أصلاً على الخادم |

---

## 15) الأداء (Performance)

1. **لا حساب لحظي شامل:** كل ما هو تاريخي يُقرأ من `DailySalesSummary` (مُجمَّع مسبقاً)، لا من ملايين البنود الخام.
2. **حساب اليوم الجاري فقط لحظياً:** نطاق صغير (فواتير اليوم) بفهرس التغطية على `(TenantId, StoreId, InvoiceDate)`.
3. **Caching:** ملخّص اللوحة مخزَّن 60–300 ثانية لكل مستأجر/فرع؛ يقلّل ضربات DB بشكل كبير.
4. **طلب واحد مجمّع:** `/dashboard/summary` يعيد كل البطاقات في استدعاء واحد (تفادي N طلبات).
5. **SignalR مستهدف:** تحديث بطاقة اليوم فقط، لا إعادة بناء اللوحة كاملة.
6. **فهارس مُرشَّحة** `WHERE IsDeleted = 0` على جداول المصدر.
7. **Hangfire** يبني الملخّصات خارج مسار الطلب (لا يُبطئ المستخدم).

**بنية جدول الملخّص المسبق:**

```sql
CREATE TABLE [dbo].[DailySalesSummary]
(
    [SummaryDate] DATE          NOT NULL,     -- بتوقيت المستأجر
    [ProductId]   BIGINT        NULL,
    [Qty]         DECIMAL(18,4)  NOT NULL,
    [Revenue]     DECIMAL(18,4)  NOT NULL,
    [COGS]        DECIMAL(18,4)  NOT NULL,
    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_DailySalesSummary_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_DailySalesSummary_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,
    CONSTRAINT [PK_DailySalesSummary] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_DailySalesSummary_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO
CREATE NONCLUSTERED INDEX [IX_DailySalesSummary_Lookup]
    ON [dbo].[DailySalesSummary] ([TenantId], [StoreId], [SummaryDate])
    INCLUDE ([ProductId], [Qty], [Revenue], [COGS])
    WHERE [IsDeleted] = 0;
GO
```

---

## 16) الأمان (Security)

1. **العزل القسري:** `TenantId` من JWT claims؛ `StoreId` مقيّد بالصلاحية على الخادم.
2. **فصل المالي عن التشغيلي:** بطاقات الربح والمستحقات تتطلّب `Dashboard.Financial`؛ تُحذف من الاستجابة قبل الإرسال لمن لا يملكها (لا تُخفى في الواجهة فقط).
3. **SignalR مؤمَّن:** المستخدم يُشترَك حصراً في مجموعة مستأجره/فرعه؛ لا بثّ عابر للمستأجرين.
4. **بارامترات مُعامَلة:** لا SQL نصّي؛ حماية من الحقن.
5. **حدّ المعدّل** على زر التحديث اليدوي لمنع تجاوز الـ cache باستمرار.
6. **تدقيق الوصول المالي** كما في القسم 13.
7. **RLS اختياري** كطبقة دفاع ثانية على مستوى SQL Server.

---

_يلتزم هذا الملف بمعايير [04-Database-Design.md](04-Database-Design.md). التقارير التفصيلية خلف كل بطاقة موثّقة في [20-Reports.md](20-Reports.md)._
