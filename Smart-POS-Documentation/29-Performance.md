# 29 — Performance (الأداء والتوسّع)

> **Smart ERP POS** يخدم آلاف المستأجرين وملايين المعاملات من نشر واحد. الأداء ليس ترفاً: هدف **زمن استجابة POS < 200ms** للبيع (T4 في [01-Project-Overview.md](01-Project-Overview.md)). هذا الملف يحكم قرارات الأداء عبر الطبقات: قاعدة البيانات، الوصول للبيانات، الكاش، التزامن، والتوسّع.

---

## 1) المبادئ الحاكمة (Performance Principles)

| المبدأ | القرار |
|--------|--------|
| القياس قبل التحسين | لا تحسين بلا Benchmark/Profiling — "Measure, don't guess" |
| Async في كل I/O | كل استدعاء DB/شبكة/ملف بـ `async/await` |
| Read vs Write | مسارات القراءة تُحسَّن بـ `AsNoTracking` والكاش؛ الكتابة بمعاملات ذرّية دقيقة |
| Pagination إجبارية | لا استعلام يُعيد قائمة غير مقيّدة (سقف `pageSize`) |
| العزل لا يُكلّف | `TenantId` أول عمود في كل فهرس (انظر [04-Database-Design.md](04-Database-Design.md)) |
| كل مستأجر عادل | لا مستأجر يستهلك موارد الآخرين (Rate Limiting + عزل الوظائف) |

---

## 2) الفهرسة والاستعلام (Indexing & Query Tuning)

الأساس المعماري في [04-Database-Design.md](04-Database-Design.md). للأداء تحديداً:

- **`TenantId` أول عمود** في كل فهرس عزل `(TenantId, StoreId, ...)` — كل استعلام مفلتَر بالمستأجر يستفيد فوراً.
- **Filtered Indexes** `WHERE IsDeleted = 0` تصغّر الفهرس وتسرّع الاستعلامات (تتجاهل المحذوف soft).
- **Covering Indexes** عبر `INCLUDE` لاستعلامات التقارير الثقيلة — تُلبّى من الفهرس دون الرجوع للجدول (Key Lookup).
  ```sql
  CREATE NONCLUSTERED INDEX IX_SalesInvoices_Report
      ON dbo.SalesInvoices (TenantId, InvoiceDate)
      INCLUDE (Total, CustomerId, Status)
      WHERE IsDeleted = 0;
  ```
- **RCSI** (`READ COMMITTED SNAPSHOT`) مفعّل لتقليل الأقفال بين القرّاء والكتّاب (انظر [04-Database-Design.md](04-Database-Design.md) §7).
- مراقبة الاستعلامات البطيئة عبر **Query Store** في SQL Server 2022، وضبط الخطط المتراجعة (Plan Regression).
- تجنّب `SELECT *`؛ نُسقِط الأعمدة المطلوبة فقط (Projection) لتقليل I/O.

---

## 3) الوصول للبيانات مع EF Core

### 3.1 AsNoTracking للقراءة

كل استعلام قراءة (لا يتبعه تعديل) يستخدم `AsNoTracking` — يلغي تتبّع التغييرات فيقلّل الذاكرة ويسرّع.

```csharp
var products = await _db.Products
    .AsNoTracking()
    .Where(p => p.CategoryId == categoryId)
    .OrderBy(p => p.Name)
    .Skip((page - 1) * size).Take(size)
    .Select(p => new ProductListDto(p.PublicId, p.Name, p.SellPrice)) // Projection
    .ToListAsync(ct);
```

### 3.2 مشكلة N+1

**المشكلة:** تحميل قائمة فواتير ثم الوصول لبنودها في حلقة ⇒ استعلام لكل فاتورة.

**الحلّ:**
- **Eager Loading** بـ `Include`/`ThenInclude` عند الحاجة الفعلية للعلاقة.
- الأفضل: **Projection** مباشرة إلى DTO يجلب المطلوب فقط في استعلام واحد.
- **Split Queries** (`AsSplitQuery`) عند تعدّد المجموعات لتفادي الضرب الكارتيزي (Cartesian Explosion).

```csharp
// بدل Include ثقيل — Projection بمجموعة فرعية في استعلام واحد
var invoices = await _db.SalesInvoices.AsNoTracking()
    .Where(i => i.InvoiceDate >= from)
    .Select(i => new InvoiceDto {
        PublicId = i.PublicId,
        Total = i.Total,
        Items = i.Items.Select(x => new ItemDto(x.ProductId, x.Quantity)).ToList()
    })
    .ToListAsync(ct);
```

### 3.3 Lazy vs Eager

- **Lazy Loading معطّل افتراضياً** — يخفي N+1 ويُنتج استعلامات صامتة. نعتمد **Eager صريح** أو **Projection**.
- Explicit Loading عند الحاجة النادرة لعلاقة بعد التحميل.

### 3.4 Compiled Queries

للاستعلامات الساخنة المتكرّرة (مسار POS: جلب منتج بالباركود) نستخدم **Compiled Queries** لتفادي إعادة بناء الخطّة:

```csharp
private static readonly Func<AppDbContext, long, string, Task<Product?>> _byBarcode =
    EF.CompileAsyncQuery((AppDbContext db, long tenantId, string barcode) =>
        db.Products.AsNoTracking()
          .FirstOrDefault(p => p.TenantId == tenantId && p.Barcode == barcode && !p.IsDeleted));
```

### 3.5 Batching

- تحديثات/حذف جماعي عبر `ExecuteUpdateAsync` / `ExecuteDeleteAsync` (EF Core 7+) — عملية DB واحدة بدل تتبّع كل كيان.
- تجميع الكتابات في `SaveChangesAsync` واحد ضمن معاملة الوحدة (Unit of Work).

---

## 4) التزامن غير المتزامن (Async/Await & Connection Pooling)

- **Async شامل**: كل I/O (`ToListAsync`, `SaveChangesAsync`, استدعاءات HTTP) غير متزامن لتحرير خيوط الـ thread pool وزيادة الإنتاجية (Throughput) تحت الحمل.
- **`CancellationToken`** يُمرَّر عبر السلسلة لإلغاء العمليات المهجورة.
- **Connection Pooling**: مفعّل افتراضياً في ADO.NET/EF؛ نضبط `Max Pool Size` بما يناسب الحمل، ونتجنّب فتح/إغلاق يدوي.
  ```
  Server=...;Database=SmartPos;Max Pool Size=200;Min Pool Size=10;Pooling=true;
  ```
- **DbContext Pooling** (`AddDbContextPool`) لإعادة استخدام كائنات الـ context وتقليل ضغط GC:
  ```csharp
  builder.Services.AddDbContextPool<AppDbContext>(o => o.UseSqlServer(cs), poolSize: 128);
  ```
  > تنبيه: مع RLS يجب إعادة ضبط `SESSION_CONTEXT('TenantId')` عند كل استعارة اتّصال (انظر [27-Security.md](27-Security.md) §8.3).

---

## 5) التخزين المؤقت (Caching)

استراتيجية هجينة: **In-Memory** الآن، **Redis** لاحقاً للتوسّع الأفقي (انظر [README.md](README.md)).

| المستوى | التقنية | يُخزَّن فيه |
|---------|---------|-------------|
| L1 (داخل النسخة) | `IMemoryCache` | بيانات مرجعية نادرة التغيّر (عملات، وحدات، إعدادات المستأجر) |
| L2 (موزّع) | **Redis** | كاش مشترك عبر النسخ، Rate Limiting، جلسات مؤقتة |
| HTTP | `ResponseCaching` / ETags | استجابات GET القابلة للتخزين |

**قواعد حاسمة:**

- **مفتاح الكاش يتضمّن `TenantId`** دائماً: `tenant:{id}:product:{barcode}` — منع تسرّب الكاش بين المستأجرين (يتّسق مع [27-Security.md](27-Security.md)).
- **إبطال الكاش (Invalidation)** عند تعديل البيانات المصدرية (تعديل سعر ⇒ إبطال كاش المنتج).
- انتهاء صلاحية زمني (TTL) + Sliding expiration للبيانات المرجعية.
- **لا** نكاش البيانات المالية المتغيّرة لحظياً (أرصدة المخزون الحيّة، مجاميع الشفت المفتوح).

```csharp
var product = await _cache.GetOrCreateAsync(
    $"tenant:{tenantId}:product:{barcode}",
    async entry => {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
        return await _repo.GetByBarcodeAsync(tenantId, barcode, ct);
    });
```

---

## 6) الترقيم للقوائم الكبيرة (Pagination at Scale)

- **Offset Pagination** (`page/pageSize`) للقوائم العادية، بسقف `pageSize ≤ 100` (انظر [25-API-Design.md](25-API-Design.md)).
- **Keyset/Cursor Pagination** للقوائم الضخمة المتنامية (`StockMovements`, `AuditLogs`) — أداء ثابت لا يتدهور مع الصفحات البعيدة:
  ```sql
  SELECT TOP (@size) * FROM StockMovements
  WHERE TenantId = @t AND Id < @lastSeenId
  ORDER BY Id DESC;   -- ثابت O(log n) عبر الفهرس، لا OFFSET مكلف
  ```
- `COUNT(*)` منفصل ومكلف — يُسقَط بـ `?withCount=false` عند عدم الحاجة للإجمالي.

---

## 7) الوظائف الخلفية (Background Jobs — Hangfire)

العمليات الثقيلة/المؤجّلة تخرج من مسار الطلب إلى **Hangfire** (انظر [README.md](README.md)):

| المهمّة | النمط |
|---------|-------|
| توليد تقارير ثقيلة | Fire-and-forget / Batch |
| إغلاق يومي (EOD)، تجميعات | Recurring (cron) |
| إشعارات/بريد/رسائل | Queued |
| Backfill لهجرات Expand/Contract | Delayed (انظر [26-Deployment.md](26-Deployment.md)) |
| فهرسة/تنظيف/أرشفة | Recurring |

**قواعد multi-tenant للوظائف:**

- كل مهمّة تحمل `TenantId` وتفتح **نطاق المستأجر الصحيح** (`ITenantProvider`) قبل التنفيذ — وإلا تكسر العزل.
- طوابير منفصلة (Queues) لمنع مستأجر ثقيل من تجويع البقيّة (Fair Scheduling).
- Idempotency + إعادة محاولة (Retry) مع Backoff للمهام الفاشلة.

---

## 8) تحسين الصور والأصول (Image & Asset Optimization)

- الصور تُخزَّن خارج DB (Blob/S3)، وتُقدَّم عبر **CDN**، مع مسار معزول `tenants/{id}/...`.
- ضغط وتوليد أحجام متعدّدة (thumbnails) عند الرفع لا عند الطلب.
- تنسيقات حديثة (WebP/AVIF) + `Cache-Control` طويل + أسماء مبنيّة على hash للـ immutability.
- تقديم كسول (lazy `<img loading="lazy">`) في الواجهة.

---

## 9) التوسّع لآلاف المستأجرين (Scaling)

### 9.1 التوسّع الأفقي (Horizontal Scaling)

- التطبيق **Stateless** (انظر [25-API-Design.md](25-API-Design.md) و[26-Deployment.md](26-Deployment.md)) ⇒ نضيف نسخاً خلف موازِن الحمل بلا حالة مشتركة محلّية.
- الحالة المشتركة في **Redis** (كاش/معدّل)، والبيانات في SQL Server.

### 9.2 توسّع قاعدة البيانات

| الأسلوب | متى |
|---------|-----|
| **Read Replicas** | توجيه القراءات الثقيلة (تقارير) لنسخ قراءة، والكتابة للأساسي |
| **Partitioning** | تقسيم الجداول الضخمة (`StockMovements`, `AuditLogs`) بالتاريخ/`TenantId` |
| **Sharding** (مستقبلاً) | نقل المستأجرين الضخام لقواعد/شاردات منفصلة — التصميم يسمح به (T3 في [01-Project-Overview.md](01-Project-Overview.md), [24-MultiTenant.md](24-MultiTenant.md)) |

> **ميزة `TenantId`:** وجوده في كل صفّ يجعل التقسيم/التشارد لاحقاً ممكناً بلا إعادة تصميم — نُرحّل مستأجراً بمعيار `TenantId` واحد.

### 9.3 عدالة المستأجرين (Tenant Fairness)

- Rate Limiting لكل مستأجر يمنع "الجار الصاخب" (Noisy Neighbor).
- طوابير Hangfire وموارد الكاش معزولة بـ `TenantId`.

---

## 10) قياس الأداء والمراقبة (Benchmarks & APM)

- **Benchmarks دقيقة**: **BenchmarkDotNet** لقياس المسارات الساخنة (compiled query مقابل عادي، serialization) في CI.
- **APM**: Application Insights / OpenTelemetry يجمع: زمن الاستجابة (p50/p95/p99)، معدّل الأخطاء `5xx`، Throughput، وأبطأ الاستعلامات.
- **Serilog** يسجّل `ElapsedMs` لكل طلب مع `CorrelationId/TenantId` (انظر [25-API-Design.md](25-API-Design.md)).
- **Query Store** لمراقبة تراجع خطط الاستعلام في SQL Server.
- **تنبيهات**: تجاوز p95 عتبة (مثلاً POS > 200ms)، أو ارتفاع معدّل الأخطاء ⇒ تنبيه للفريق.

### أهداف الأداء (SLOs)

| المسار | الهدف |
|--------|-------|
| بيع POS (Create Sale) | p95 < 200ms |
| جلب منتج بالباركود | p95 < 50ms |
| قائمة مرقّمة (25 عنصراً) | p95 < 300ms |
| تقرير ثقيل | يُنقَل لوظيفة خلفية إن تجاوز 2s |
| توفّر الخدمة | 99.9% شهرياً |

---

## 11) قائمة تدقيق الأداء (Performance Checklist)

- [ ] كل قراءة بـ `AsNoTracking` + Projection لما يلزم فقط.
- [ ] لا N+1: Eager صريح/Projection/`AsSplitQuery`؛ Lazy Loading معطّل.
- [ ] Compiled Queries للمسارات الساخنة (POS).
- [ ] كل I/O `async` مع `CancellationToken`.
- [ ] Connection + DbContext Pooling مضبوط (مع إعادة ضبط SESSION_CONTEXT عند RLS).
- [ ] كاش L1/L2 بمفتاح يتضمّن `TenantId` + إبطال عند التعديل.
- [ ] Pagination إجبارية بسقف؛ Keyset للقوائم الضخمة.
- [ ] العمليات الثقيلة في Hangfire بنطاق مستأجر صحيح وطوابير معزولة.
- [ ] فهارس عزل/covering/filtered وفق [04-Database-Design.md](04-Database-Design.md)؛ Query Store مراقَب.
- [ ] APM + Serilog `ElapsedMs` + تنبيهات على SLOs.

---

_الأداء نتيجة قرارات مقاسة لا تخمينات. أي انحراف عن هذا المرجع يجب أن يُدعَم ببيانات قياس._
