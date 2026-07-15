# 25 — API Design (تصميم واجهة برمجة التطبيقات)

> هذا الملف هو **المرجع الحاكم** لكل endpoint في النظام. أي وحدة (Products, Sales, POS...) توثِّق endpoints خاصة بها **يجب** أن تلتزم بالمعايير هنا: شكل الاستجابة الموحّد، التنسيق (Versioning)، التحقّق (Validation)، معالجة الأخطاء، التسجيل (Logging)، الترقيم/الفلترة/الفرز/البحث. الهدف: API متّسق (Consistent)، متوقَّع (Predictable)، آمن، ومتعدّد المستأجرين.

---

## 1) المبادئ الحاكمة (Guiding Principles)

| المبدأ | القرار | السبب |
|--------|--------|-------|
| النمط | **REST** فوق HTTP/JSON | البساطة والتوافق الواسع مع العملاء |
| الاتّساق | **كل** استجابة تتبع Envelope موحّداً | العميل يكتب معالجة واحدة لكل الطلبات |
| الحالة | **Stateless** — لا جلسات على الخادم | التوسّع الأفقي وموازنة الحمل |
| العزل | `TenantId` يُستخرَج من الـ JWT **لا** من الطلب | منع تسرّب البيانات بين المستأجرين (IDOR) |
| الأسماء | جمع أسماء الموارد بصيغة kebab-case | `/api/v1/sales-invoices` |
| الأفعال | أفعال HTTP القياسية فقط | `GET/POST/PUT/PATCH/DELETE` |
| الأوقات | UTC حصراً في كل الحقول الزمنية (ISO 8601) | التوحيد عبر المناطق الزمنية |
| المعرّفات الخارجية | `PublicId (GUID)` في الـ URL لا `Id (BIGINT)` | عدم كشف تسلسل السجلات |

> **قاعدة ذهبية:** لا يستقبل الـ API قيمة `TenantId` من العميل إطلاقاً. تُشتَق حصراً من الـ Claims داخل الـ JWT عبر `ITenantProvider`، وتُطبَّق تلقائياً عبر EF Core Global Query Filter.

---

## 2) تنسيق الموارد وأفعال HTTP (Resource Naming & Verbs)

| الفعل | المسار | الدلالة | رمز النجاح |
|-------|--------|---------|-----------|
| `GET` | `/api/v1/products` | قائمة (مع ترقيم) | `200 OK` |
| `GET` | `/api/v1/products/{publicId}` | عنصر واحد | `200 OK` |
| `POST` | `/api/v1/products` | إنشاء | `201 Created` + `Location` |
| `PUT` | `/api/v1/products/{publicId}` | استبدال كامل | `200 OK` |
| `PATCH` | `/api/v1/products/{publicId}` | تعديل جزئي | `200 OK` |
| `DELETE` | `/api/v1/products/{publicId}` | حذف soft | `204 No Content` |
| `POST` | `/api/v1/sales-invoices/{publicId}/void` | فعل مجالي (Action) | `200 OK` |

**قواعد التسمية:**

- الموارد **أسماء جمع** (`products` لا `product`، `sales-invoices` لا `sales-invoice`).
- الأفعال المجالية التي لا تُمثَّل بـ CRUD تُعبَّر كـ sub-resource action: `POST /sales-invoices/{id}/void`, `POST /pos-shifts/{id}/close`.
- التداخل يقتصر على مستوى واحد منطقي: `/purchase-invoices/{id}/items` مقبول، أما التعشيش العميق فيُستبدَل بفلترة: `/stock-movements?warehouseId=...`.

---

## 3) الاستجابة الموحّدة (Unified Response Envelope)

**كل** استجابة — نجاح أو فشل — تُغلَّف في المظروف التالي. يُطبَّق مركزياً عبر `ResultFilter` + Middleware، فلا يكتبه المطوّر يدوياً.

```jsonc
// نجاح — عنصر واحد
{
  "success": true,
  "data": {
    "publicId": "b1e6...c9",
    "name": "Coca-Cola 330ml",
    "sellPrice": 0.5000,
    "createdDate": "2026-07-13T09:41:22.113Z"
  },
  "errors": [],
  "meta": {
    "traceId": "0HN2K...:00000007",
    "timestamp": "2026-07-13T09:41:22.500Z"
  }
}
```

```jsonc
// نجاح — قائمة مرقّمة
{
  "success": true,
  "data": [ { "publicId": "...", "name": "..." } ],
  "errors": [],
  "meta": {
    "traceId": "0HN2K...:0000000A",
    "timestamp": "2026-07-13T09:41:23.010Z",
    "pagination": {
      "page": 1,
      "pageSize": 25,
      "totalItems": 1342,
      "totalPages": 54,
      "hasNext": true,
      "hasPrevious": false
    }
  }
}
```

```jsonc
// فشل — أخطاء تحقّق
{
  "success": false,
  "data": null,
  "errors": [
    { "code": "VAL_REQUIRED", "field": "name", "message": "اسم المنتج مطلوب" },
    { "code": "VAL_RANGE", "field": "sellPrice", "message": "السعر يجب أن يكون أكبر من صفر" }
  ],
  "meta": {
    "traceId": "0HN2K...:0000000C",
    "timestamp": "2026-07-13T09:41:24.220Z"
  }
}
```

**عقد المظروف (Envelope Contract):**

| الحقل | النوع | الوصف |
|-------|-------|-------|
| `success` | `bool` | `true` إذا كان `2xx`، وإلا `false` |
| `data` | `object` \| `array` \| `null` | الحمولة عند النجاح، و`null` عند الفشل |
| `errors` | `array<Error>` | فارغ عند النجاح، وإلا قائمة أخطاء منظَّمة |
| `meta.traceId` | `string` | معرّف الارتباط (Correlation Id) لتتبّع الطلب في السجلّات |
| `meta.timestamp` | `string (ISO8601 UTC)` | لحظة توليد الاستجابة |
| `meta.pagination` | `object?` | يظهر فقط في استجابات القوائم |

**كائن الخطأ (Error Object):**

```csharp
public sealed record ApiError(
    string Code,        // رمز موحّد قابل للترجمة برمجياً (انظر §11)
    string Message,     // رسالة للمستخدم (مترجمة حسب Accept-Language)
    string? Field = null // اسم الحقل المخالف في أخطاء التحقّق
);
```

> **مبدأ:** العميل يعتمد على `code` (ثابت، غير مترجَم) في المنطق، و`message` (مترجَم) في العرض فقط.

---

## 4) الإصدارات (Versioning)

**النمط المعتمد: URL Path Versioning** — `/api/v{n}/...`

```
https://api.smartpos.com/api/v1/sales-invoices
https://api.smartpos.com/api/v2/sales-invoices
```

| القرار | التفصيل |
|--------|---------|
| الموقع | في المسار (`/api/v1`) لوضوحه وقابلية تخزينه المؤقت |
| الحزمة | `Asp.Versioning.Http` (Microsoft) |
| الافتراضي | `v1` عند غياب الإصدار، مع `AssumeDefaultVersionWhenUnspecified` |
| الإهمال | رأس `Sunset` + `Deprecation` وفق RFC 8594 قبل إزالة إصدار بـ 6 أشهر |
| كسر التوافق | تغيير حقل موجود أو حذفه ⇒ إصدار جديد؛ إضافة حقل اختياري ⇒ لا يكسر |

```csharp
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.ReportApiVersions = true; // يضيف api-supported-versions في الرأس
}).AddApiExplorer(o =>
{
    o.GroupNameFormat = "'v'VVV";
    o.SubstituteApiVersionInUrl = true;
});
```

```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/products")]
public sealed class ProductsController : ControllerBase { /* ... */ }
```

---

## 5) التحقّق (Validation — FluentValidation)

التحقّق **يُنفَّذ في طبقة Application** عبر **FluentValidation**، ويُشغَّل تلقائياً في خطّ MediatR عبر `ValidationBehavior<TRequest, TResponse>` قبل وصول الأمر إلى المعالج.

```csharp
public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("VAL_REQUIRED")
            .MaximumLength(200).WithErrorCode("VAL_MAXLENGTH");

        RuleFor(x => x.SellPrice)
            .GreaterThan(0).WithErrorCode("VAL_RANGE");

        RuleFor(x => x.Barcode)
            .MustAsync(BeUniqueBarcodeWithinTenant)
            .WithErrorCode("VAL_DUPLICATE")
            .WithMessage("الباركود مستخدَم مسبقاً في هذا المستأجر");
    }
}
```

```csharp
// يعمل قبل كل Command/Query في MediatR
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(request, ct))))
            .SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures); // يلتقطها الـ Middleware ويحوّلها لمظروف الأخطاء
        return await next();
    }
}
```

**مبادئ:**

- التحقّق البنيوي (required/length/range) + التحقّق المجالي (تفرّد الباركود ضمن المستأجر، رصيد كافٍ) كلاهما في FluentValidation/المعالج.
- كل قاعدة تحمل `WithErrorCode` من جدول الرموز الموحّد (§11) لضمان اتّساق العميل.
- **لا** يُستخدم `[Required]` وحده على الـ DTO كضمانة أمان — التحقّق النهائي دائماً في الخادم.

---

## 6) معالجة الاستثناءات (Exception Handling — Middleware + ProblemDetails)

معالجة **مركزية** عبر `IExceptionHandler` (.NET 8+). لا `try/catch` متناثر في الـ Controllers. كل استثناء يُترجَم إلى مظروف الأخطاء برمز HTTP مناسب.

```csharp
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, code, errors) = ex switch
        {
            ValidationException v   => (StatusCodes.Status422UnprocessableEntity, "VAL_FAILED", v.ToApiErrors()),
            NotFoundException       => (StatusCodes.Status404NotFound,   "RES_NOT_FOUND",  null),
            ForbiddenException      => (StatusCodes.Status403Forbidden,  "AUTH_FORBIDDEN", null),
            ConcurrencyException    => (StatusCodes.Status409Conflict,   "RES_CONFLICT",   null),
            BusinessRuleException b => (StatusCodes.Status400BadRequest, b.Code,           null),
            _                       => (StatusCodes.Status500InternalServerError, "SRV_UNEXPECTED", null)
        };

        // لا نُسرّب تفاصيل الاستثناء الداخلي للعميل في 5xx
        logger.LogError(ex, "Unhandled {Code} for {Path} traceId={TraceId}",
            code, ctx.Request.Path, ctx.TraceIdentifier);

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(ApiEnvelope.Fail(code, errors, ctx.TraceIdentifier), ct);
        return true;
    }
}
```

**سياسة أمان الأخطاء:**

- أخطاء `5xx` **لا** تكشف رسالة الاستثناء الأصلية أو الـ stack trace للعميل — فقط رمز عام و`traceId` للمتابعة.
- التفاصيل الكاملة تُسجَّل في Serilog مقرونة بـ `traceId` نفسه الظاهر للعميل.
- الاستجابات متوافقة مع **RFC 7807 ProblemDetails** عند الحاجة (بوابات/مُوحِّدات خارجية) لكن الافتراض هو المظروف الداخلي الموحّد.

| الاستثناء | HTTP | مثال |
|-----------|------|------|
| `ValidationException` | `422` | حقل ناقص، قيمة خارج المدى |
| `NotFoundException` | `404` | مورد غير موجود ضمن المستأجر |
| `ForbiddenException` | `403` | صلاحية ناقصة |
| `ConcurrencyException` | `409` | `ConcurrencyStamp` قديم |
| `BusinessRuleException` | `400` | رصيد مخزون غير كافٍ، شفت مغلق |
| غير متوقّع | `500` | خطأ داخلي |

---

## 7) التسجيل (Logging — Serilog + Correlation Id)

- **Serilog** كمزوّد وحيد، مع structured logging (JSON) في الإنتاج.
- **Correlation Id** يُنشأ لكل طلب في أول Middleware، ويُمرَّر عبر السلسلة كاملة حتى يظهر في `meta.traceId` والسجلّات معاً.

```csharp
app.Use(async (ctx, next) =>
{
    var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                        ?? ctx.TraceIdentifier;
    ctx.Response.Headers["X-Correlation-Id"] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    using (LogContext.PushProperty("TenantId", ctx.User.FindFirst("tenant_id")?.Value))
    using (LogContext.PushProperty("UserId", ctx.User.FindFirst("sub")?.Value))
    {
        await next();
    }
});
```

**قواعد التسجيل:**

- كل سطر سجلّ يحمل: `CorrelationId`, `TenantId`, `UserId`, `Path`, `StatusCode`, `ElapsedMs`.
- **ممنوع** تسجيل بيانات حسّاسة (كلمات مرور، رموز JWT، أرقام بطاقات) — تُخفَّى عبر Serilog `Destructure`/masking.
- المستويات: `Information` للطلبات الناجحة، `Warning` لأخطاء العميل (`4xx`)، `Error` لأخطاء الخادم (`5xx`).
- الوجهات (Sinks): Console (Dev)، File rolling + Seq/Elasticsearch (Prod). التفاصيل في [26-Deployment.md](26-Deployment.md).

---

## 8) الترقيم والفلترة والفرز والبحث (Pagination / Filtering / Sorting / Searching)

**نمط query params موحّد** عبر كل الموارد القابلة للاستعلام، ممثَّل بكائن قاعدي `PagedQuery`.

```
GET /api/v1/products
    ?page=1
    &pageSize=25
    &sort=name,-createdDate         // تصاعدي name، تنازلي createdDate
    &search=cola                    // بحث نصّي على الحقول المفهرسة
    &categoryId=b1e6...             // فلترة بالمساواة
    &sellPrice_gte=0.5              // فلترة نطاقية
    &sellPrice_lte=2.0
    &isActive=true
```

| المعامل | الافتراضي | القيود |
|---------|-----------|--------|
| `page` | `1` | `>= 1` |
| `pageSize` | `25` | `1..100` (سقف صارم لمنع الإرهاق) |
| `sort` | `-createdDate` | حقول مسموحة فقط (allow-list) |
| `search` | — | يُطبَّق على أعمدة مفهرسة محدّدة لكل مورد |
| `{field}_gte` / `_lte` | — | مقارنات نطاقية للأرقام والتواريخ |

```csharp
public abstract record PagedQuery
{
    private const int MaxPageSize = 100;
    public int Page { get; init; } = 1;
    private int _pageSize = 25;
    public int PageSize { get => _pageSize; init => _pageSize = Math.Clamp(value, 1, MaxPageSize); }
    public string? Sort { get; init; }
    public string? Search { get; init; }
}
```

**مبادئ:**

- الترقيم عبر **Offset** (`page/pageSize`) افتراضياً؛ وللقوائم الضخمة المتغيّرة (حركات المخزون، سجلّ التدقيق) يُدعَم **Keyset/Cursor** (`?cursor=...`) لأداء ثابت.
- الفرز يُقيَّد بـ **allow-list** لكل مورد — لا يُسمح بالفرز على عمود غير مفهرس لمنع فحص الجداول الكامل.
- `totalItems` يُحسَب بـ `COUNT` منفصل؛ ويمكن إسقاطه بـ `?withCount=false` للأداء.
- كل الاستعلامات تُطبَّق بعد Global Query Filter، فلا تتخطّى عزل المستأجر.

---

## 9) Swagger / OpenAPI

- توليد OpenAPI عبر **Swashbuckle** لكل إصدار (`v1`, `v2`) بمجموعات منفصلة.
- **JWT Bearer** مفعّل في واجهة Swagger لاختبار مباشر.
- كل DTO موثَّق بـ XML comments + أمثلة (`SwaggerExample`).
- في **الإنتاج**: واجهة Swagger UI محميّة خلف مصادقة أو معطّلة، مع إبقاء ملف `swagger.json` متاحاً للأدوات الداخلية فقط.

```csharp
builder.Services.AddSwaggerGen(o =>
{
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "أدخل رمز JWT فقط دون كلمة Bearer"
    });
    o.IncludeXmlComments(xmlPath);
});
```

---

## 10) تحديد المعدّل (Rate Limiting)

يُستخدَم **Rate Limiting** المدمج في .NET (`Microsoft.AspNetCore.RateLimiting`) لحماية النظام من الإساءة و DoS، **لكل مستأجر ولكل مستخدم** لا لكل IP فقط.

| السياسة | الحدّ | النطاق |
|---------|------|--------|
| `auth` (تسجيل الدخول) | 5 محاولات / دقيقة / IP+حساب | منع Brute Force (انظر [27-Security.md](27-Security.md)) |
| `per-tenant` | 600 طلب / دقيقة / مستأجر | عدالة بين المستأجرين |
| `per-user` | 120 طلب / دقيقة / مستخدم | حماية إضافية |
| `heavy-reports` | 10 طلبات / دقيقة | التقارير الثقيلة |

```csharp
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy("per-tenant", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.User.FindFirst("tenant_id")?.Value ?? "anon",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(1) }));

    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        ctx.HttpContext.Response.Headers.RetryAfter = "60";
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            ApiEnvelope.Fail("RATE_LIMITED", null, ctx.HttpContext.TraceIdentifier), ct);
    };
});
```

عند التجاوز يُعاد `429 Too Many Requests` بمظروف موحّد + رأس `Retry-After`.

---

## 11) جدول رموز الأخطاء الموحّدة (Unified Error Codes)

الرموز ثابتة، بادئتها تدلّ على الفئة، ويعتمد عليها العميل برمجياً (لا على النص).

| الرمز | HTTP | الفئة | المعنى |
|-------|------|-------|--------|
| `VAL_REQUIRED` | 422 | Validation | حقل مطلوب غير موجود |
| `VAL_MAXLENGTH` | 422 | Validation | تجاوز الطول الأقصى |
| `VAL_RANGE` | 422 | Validation | قيمة خارج المدى المسموح |
| `VAL_FORMAT` | 422 | Validation | تنسيق غير صالح (بريد، هاتف) |
| `VAL_DUPLICATE` | 422 | Validation | قيمة مكرّرة ضمن المستأجر (باركود مثلاً) |
| `VAL_FAILED` | 422 | Validation | فشل تحقّق عام (يحوي `errors` مفصّلة) |
| `AUTH_UNAUTHENTICATED` | 401 | Auth | لا رمز أو رمز منتهٍ |
| `AUTH_INVALID_TOKEN` | 401 | Auth | رمز غير صالح أو مُبطَل |
| `AUTH_FORBIDDEN` | 403 | Auth | مصادَق لكن بلا صلاحية |
| `AUTH_LOCKED` | 423 | Auth | الحساب مقفول (Brute Force) |
| `RES_NOT_FOUND` | 404 | Resource | المورد غير موجود ضمن المستأجر |
| `RES_CONFLICT` | 409 | Resource | تعارض تزامن (`ConcurrencyStamp`) |
| `RES_GONE` | 410 | Resource | المورد محذوف soft |
| `BR_INSUFFICIENT_STOCK` | 400 | Business | رصيد مخزون غير كافٍ |
| `BR_SHIFT_CLOSED` | 400 | Business | محاولة عملية على شفت مغلق |
| `BR_CREDIT_LIMIT` | 400 | Business | تجاوز حدّ الائتمان للعميل |
| `BR_PERIOD_LOCKED` | 400 | Business | فترة محاسبية مقفلة |
| `RATE_LIMITED` | 429 | Throttling | تجاوز حدّ الطلبات |
| `SRV_UNEXPECTED` | 500 | Server | خطأ داخلي غير متوقّع |
| `SRV_UNAVAILABLE` | 503 | Server | الخدمة غير متاحة مؤقتاً (صيانة) |

---

## 12) أمثلة Endpoints كاملة (Reference Examples)

**إنشاء منتج:**

```http
POST /api/v1/products HTTP/1.1
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6...
Content-Type: application/json
X-Correlation-Id: 6f1c9a2e-...

{
  "name": "Coca-Cola 330ml",
  "categoryId": "b1e6...c9",
  "barcode": "5449000000996",
  "sellPrice": 0.5000,
  "costPrice": 0.3200,
  "taxRate": 16.0
}
```

```http
HTTP/1.1 201 Created
Location: /api/v1/products/9d2f...ab
X-Correlation-Id: 6f1c9a2e-...

{ "success": true, "data": { "publicId": "9d2f...ab", ... }, "errors": [], "meta": { ... } }
```

**بيع (POS) — فعل مجالي داخل شفت:**

```http
POST /api/v1/pos-shifts/{shiftId}/sales HTTP/1.1
{
  "customerId": null,
  "items": [ { "productId": "9d2f...ab", "quantity": 3, "unitPrice": 0.5000 } ],
  "payments": [ { "method": "CASH", "amount": 1.5000 } ]
}
```

- `TenantId`/`StoreId` مستنتجان من الرمز — لا يُرسَلان.
- العملية بأكملها (فاتورة + بنودها + حركة مخزون + حركة صندوق) في **معاملة واحدة** (Unit of Work) — انظر [18-POS.md](18-POS.md).
- عند نقص الرصيد يُعاد `400` برمز `BR_INSUFFICIENT_STOCK`.

**قائمة مرقّمة مفلترة:**

```http
GET /api/v1/sales-invoices?page=2&pageSize=50&sort=-invoiceDate&status=POSTED&total_gte=100 HTTP/1.1
```

---

## 13) قائمة تدقيق تصميم الـ API (Design Checklist)

- [ ] المورد بصيغة جمع kebab-case، والأفعال المجالية كـ sub-resource actions.
- [ ] المسار يحمل الإصدار `/api/v1/...`.
- [ ] الاستجابة (نجاحاً وفشلاً) بمظروف `success/data/errors/meta`.
- [ ] `TenantId` من الرمز حصراً، والـ URL يستخدم `PublicId` لا `Id`.
- [ ] التحقّق عبر FluentValidation مع `ErrorCode` من الجدول الموحّد.
- [ ] الأخطاء عبر `GlobalExceptionHandler` — لا `try/catch` في الـ Controller.
- [ ] كل استجابة تحمل `traceId` مطابقاً لسطر السجلّ.
- [ ] الترقيم/الفرز/الفلترة وفق النمط الموحّد مع سقف `pageSize` و allow-list للفرز.
- [ ] السياسة المناسبة من Rate Limiting مطبَّقة.
- [ ] موثَّق في Swagger مع أمثلة.

---

_يلتزم كل ملف API لاحق بهذا المرجع. أي انحراف يجب تبريره صراحةً._
