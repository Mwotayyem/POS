# 09 — Multi-Tenant Architecture (العزل والتفعيل اليدوي)

> **الملف المحوري.** يشرح كيف يخدم نشر واحد عدّة عملاء بعزل تامّ، وكيف يتحكّم مالك النظام **يدوياً** بتفعيل/تعطيل العملاء **دون أي اشتراك أو فوترة**. يلتزم بـ [05-Database-Design.md](05-Database-Design.md) ويكمّله [11-Security-Architecture.md](11-Security-Architecture.md).

---

## 1) المفهوم الجوهري (Core Concept)

```
                         نشر واحد (Single Deployment)
                                    │
                     ┌──────────────┴──────────────┐
                     │       SmartApp API           │
                     │   (كود واحد · قاعدة واحدة)    │
                     └──────────────┬──────────────┘
                                    │
     ┌───────────────┬─────────────┼─────────────┬───────────────┐
     ▼               ▼             ▼             ▼               ▼
 Tenant 1001    Tenant 1002   Tenant 1003   Tenant 1004   ... عملاء آخرون
 (شركة أ)       (محل ب)       (شركة ج)       (متجر د)
 Status=Active  Status=Active Status=Suspended Status=Active
 بياناته وحده   بياناته وحده  ⚠ يُرفض دخوله  بياناته وحده
```

- **Tenant** = العميل (الشركة/المحل). له `TenantId`.
- كل صفّ بيانات أعمال يحمل `TenantId` (إلزامي).
- **إضافة عميل جديد = صفّ في `Tenants`** — لا كود، لا نشر.
- **لا `StoreId`** — `Tenant` هو الحدّ الوحيد الآن (انظر [05-Database-Design.md §7](05-Database-Design.md) لإضافة الفروع مستقبلاً).

**نموذج العزل المعتمد:** *Single Database, Shared Schema, Row-Level Isolation via `TenantId`* — مع تصميم يسمح لاحقاً بقاعدة/شارد منفصل لعميل كبير دون إعادة كتابة.

---

## 2) القاعدة الذهبية للعزل

> **`TenantId` لا يأتي أبداً من المستخدم/العميل.** يُستخرج من سياق موثوق (JWT claim) خادم-جانبياً. أي endpoint يقبل `tenantId` من الجسم/الاستعلام/المسار = **ثغرة عزل (Sev-1)**.

---

## 3) استخراج المستأجر الحالي (Tenant Resolution)

عند كل طلب، يُحدَّد المستأجر **قبل** أي استعلام. المصدر بترتيب الأولوية:

| المصدر | متى | كيف |
|--------|-----|-----|
| **JWT Claim `tenant_id`** | بعد تسجيل الدخول (الحالة الغالبة) | مضمَّن ومُوقَّع داخل التوكن — لا يُزوَّر بلا كسر التوقيع |
| **Header `X-Tenant`** | M2M / اختبارات فقط | يُقبل **فقط** مع API Key موثوق — لا من متصفّح عام |

> SmartApp (Web API، بلا subdomains مبدئياً) يعتمد أساساً على **JWT claim**. آلية الـ subdomain/custom-domain من التصميم القديم **ليست ضمن النطاق الآن** (تُضاف مع واجهة الويب لاحقاً إن لزم).

### واجهة `ITenantProvider` — نقطة الحقيقة الوحيدة

```csharp
// SmartApp.Application/Common/Interfaces/ITenantProvider.cs
public interface ITenantProvider
{
    long CurrentTenantId { get; }   // يُرمى استثناء إن لم يُحلّ لطلب يتطلّب مستأجراً
    bool IsResolved { get; }
    bool IsSystemOwner { get; }     // true لطلبات مالك النظام (لا مستأجر)
}
```

### التنفيذ (Infrastructure) يقرأ من `HttpContext`

```csharp
// SmartApp.Infrastructure/MultiTenancy/TenantProvider.cs
public sealed class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _http;
    public TenantProvider(IHttpContextAccessor http) => _http = http;

    public bool IsSystemOwner =>
        _http.HttpContext?.User.FindFirst("is_system_owner")?.Value == "true";

    public bool IsResolved =>
        IsSystemOwner || _http.HttpContext?.User.FindFirst("tenant_id") is not null;

    public long CurrentTenantId =>
        long.TryParse(_http.HttpContext?.User.FindFirst("tenant_id")?.Value, out var id)
            ? id
            : throw new TenantNotResolvedException();
}
```

---

## 4) Tenant Resolution Middleware

يُسجَّل **بعد `UseAuthentication`** (ليقرأ الـ claims) و**قبل `UseAuthorization`**:

```csharp
// SmartApp.API/Middleware/TenantResolutionMiddleware.cs
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, ITenantStore tenants)
    {
        // طلبات مالك النظام (إدارة المستأجرين) تتخطّى فحص حالة المستأجر
        if (ctx.User.FindFirst("is_system_owner")?.Value == "true")
        {
            await _next(ctx);
            return;
        }

        var claim = ctx.User.FindFirst("tenant_id")?.Value;
        if (long.TryParse(claim, out var tenantId))
        {
            // فحص حالة المستأجر (Cache-first) — البوّابة اليدوية
            var status = await tenants.GetStatusAsync(tenantId);
            if (status is null) { ctx.Response.StatusCode = 404; return; }        // غير موجود
            if (status != TenantStatus.Active)                                    // Suspended/Disabled
            {
                ctx.Response.StatusCode = 403;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    error = new { code = "TENANT_INACTIVE", message = "الحساب غير مُفعَّل. راجع مزوّد الخدمة." }
                });
                return;
            }
        }
        await _next(ctx);
    }
}
```

- `ITenantStore.GetStatusAsync` يخدم من **Cache** (`t:{tenantId}:status`) لتفادي ضربة DB لكل طلب، مع إبطال عند تغيير الحالة.
- مستأجر غير `Active` يُرفض **مبكراً (403)** قبل أي منطق أعمال.

---

## 5) التفعيل اليدوي — بديل SaaS بالكامل (Manual Activation)

**لا اشتراك · لا فوترة · لا دفع.** بدلاً من ذلك: حقل `Tenants.Status` يتحكّم به **مالك النظام** يدوياً.

### 5.1 حالات المستأجر (Tenant Lifecycle)

```
      ┌─────────── مالك النظام ينشئ المستأجر ───────────┐
      ▼                                                 │
  ┌────────┐   Suspend    ┌───────────┐   Disable   ┌──────────┐
  │ Active │ ───────────► │ Suspended │ ──────────► │ Disabled │
  │  (1)   │ ◄─────────── │    (2)    │             │   (3)    │
  └────────┘   Activate   └───────────┘             └──────────┘
      ▲                         │                         │
      │      Activate           │                         │
      └─────────────────────────┘        (نهائي — لا عودة تلقائية)
```

| الحالة | القيمة | السلوك |
|--------|:------:|--------|
| **Active** | 1 | يعمل بالكامل — الدخول وكل العمليات مسموحة |
| **Suspended** | 2 | إيقاف مؤقت — **يُرفض الدخول** ويُرفض كل طلب API (403). البيانات محفوظة. قابل للإرجاع لـ Active |
| **Disabled** | 3 | تعطيل نهائي — نفس رفض Suspended، لكن يُقصد به الإنهاء الدائم (يبقى للأرشفة) |

### 5.2 أثر التعطيل (When Tenant is NOT Active)

```
Tenant.Status != Active  ⟹
   ① المستخدمون لا يستطيعون تسجيل الدخول  (يُرفض في AuthController)
   ② أي طلب API بتوكن قائم يُرفض 403        (TenantResolutionMiddleware)
   ③ كل العمليات التجارية تتوقّف            (لا وصول للـ Handlers أصلاً)
   ④ Refresh Token يُرفض تجديده             (لا استمرار للجلسة)
```

### 5.3 من يدير الحالة؟ مالك النظام فقط

```
POST   /api/v1/tenants                  → إنشاء مستأجر (Status=Active افتراضياً)
PUT    /api/v1/tenants/{id}/suspend     → تعليق (Status=2)
PUT    /api/v1/tenants/{id}/activate    → تفعيل (Status=1)
PUT    /api/v1/tenants/{id}/disable     → تعطيل نهائي (Status=3)
GET    /api/v1/tenants                  → قائمة المستأجرين وحالاتهم
```

- هذه الـ endpoints محميّة بصلاحية خاصّة `system.tenants.manage` **وبعلامة `is_system_owner`** في التوكن.
- **خارج سياق العزل العادي** — مالك النظام فوق المستأجرين، لا ينتمي لأحدهم.
- كل تغيير حالة يُسجَّل في `AuditLogs` (`Action='StatusChange'`) ويُبطِل cache الحالة فوراً.

---

## 6) عزل البيانات — EF Core Global Query Filter (الطبقة المعتمدة)

كل كيان أعمال يرث `BaseEntity` (فيه `TenantId` و`IsDeleted`). يُطبَّق فلتر عالمي تلقائي على **كل** استعلام:

```csharp
// SmartApp.Persistence/Context/AppDbContext.cs
public sealed class AppDbContext : DbContext, IApplicationDbContext
{
    private readonly ITenantProvider _tenant;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        foreach (var et in mb.Model.GetEntityTypes()
                 .Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)))
        {
            // e => e.TenantId == _tenant.CurrentTenantId && !e.IsDeleted
            mb.Entity(et.ClrType).HasQueryFilter(BuildTenantFilter(et.ClrType));
        }
    }

    // ختم TenantId تلقائياً عند الإضافة — لا يُترك للمطوّر
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added && !_tenant.IsSystemOwner)
                entry.Entity.TenantId = _tenant.CurrentTenantId;   // ختم خادم-جانبي

            if (entry.State == EntityState.Deleted)                // تحويل الحذف لـ Soft Delete
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedDate = DateTime.UtcNow;
            }
        }
        return await base.SaveChangesAsync(ct);
    }
}
```

**ما يضمنه هذا:**

1. أي `_db.Products.ToList()` يُترجَم تلقائياً إلى `... WHERE TenantId = @t AND IsDeleted = 0` — المطوّر **لا يكتب** شرط العزل يدوياً (فلا ينساه).
2. `TenantId` يُختَم تلقائياً عند الإضافة — لا يمكن حقن قيمة مستأجر آخر.
3. الحذف يتحوّل تلقائياً لـ Soft Delete (حفظ السجل المحاسبي).

> **تحذير حاسم:** `FromSqlRaw` أو `IgnoreQueryFilters()` **يتخطّيان** الحماية. يُمنعان إلا بمراجعة أمنية، ويجب إضافة شرط `TenantId` يدوياً. هذه أخطر نقطة تسرّب محتملة (§8).

---

## 7) تدفّق طلب كامل مع فحص الحالة (Login → Filtering)

```
┌────────────────────────────────────────────────────────────────┐
│  Client → GET /api/v1/products   (Authorization: Bearer …)     │
└────────────────────────────────┬───────────────────────────────┘
                                 │ (1)
                                 ▼
        ┌──────────────────────────────────────────────┐
        │  Authentication (JWT)                         │
        │  التوقيع صالح؟  tenant_id = 1001               │
        └──────────────────────┬───────────────────────┘
                               │ (2)
                               ▼
        ┌──────────────────────────────────────────────┐
        │  TenantResolutionMiddleware                   │
        │  GetStatus(1001) (cache) = Active?            │
        │   ── لا (Suspended/Disabled) ─► 403 مبكراً     │
        └──────────────────────┬───────────────────────┘
                               │ (3) Active ✓
                               ▼
        ┌──────────────────────────────────────────────┐
        │  Authorization: صلاحية products.view موجودة؟   │
        └──────────────────────┬───────────────────────┘
                               │ (4)
                               ▼
        ┌──────────────────────────────────────────────┐
        │  Controller → MediatR → GetProductsQueryHandler│
        │  _db.Products.Where(...)                       │
        └──────────────────────┬───────────────────────┘
                               │ (5) EF يضيف الفلتر العالمي
                               ▼
        ┌──────────────────────────────────────────────┐
        │  ... WHERE TenantId = 1001 AND IsDeleted = 0  │
        └──────────────────────┬───────────────────────┘
                               │ (6) صفوف Tenant 1001 فقط
                               ▼
                  [ نتائج معزولة → DTO → للمستخدم ]
```

**بوّابات العزل المتتالية:** (أ) استخراج موثوق من JWT، (ب) فحص حالة المستأجر اليدوية، (ج) فلتر EF Core التلقائي.

---

## 8) مخاطر التسرّب وكيف نمنعها (Leakage Risks)

| الخطر | السيناريو | المنع |
|------|-----------|-------|
| **`TenantId` من المستخدم** | endpoint يقرأ `tenantId` من body/query | يُتجاهل دائماً؛ المصدر الوحيد `ITenantProvider` (JWT) |
| **`IgnoreQueryFilters()`** | تخطّي الفلتر العالمي | ممنوع بلا مراجعة؛ Roslyn Analyzer يرصده في CI |
| **`FromSqlRaw` بلا شرط** | استعلام خام ينسى `TenantId` | مراجعة إلزامية + إضافة الشرط يدوياً؛ RLS المستقبلي يمسكه |
| **جدول بلا `TenantId`** | جدول أعمال جديد نُسي عموده | اختبار آلي يفحص أن كل `BaseEntity` عليه فلتر |
| **Background Jobs** | وظيفة بلا سياق مستأجر | كل Job يضبط `TenantId` صراحةً قبل أي استعلام |
| **Caching مشترك** | مفتاح cache بلا `TenantId` | كل مفتاح يبدأ بـ `t:{TenantId}:` إجبارياً |
| **مالك النظام يكتب بيانات مستأجر** | ختم `TenantId` تلقائي يفشل لمالك النظام | عمليات مالك النظام تمرّر `TenantId` صراحةً للكيان المستهدف |

### اختبارات العزل (Isolation Tests) — إلزامية في CI

```csharp
[Fact]
public async Task Tenant_A_Cannot_See_Tenant_B_Data()
{
    SetTenant(tenantB);
    var p = await CreateProduct("سرّي");        // منتج للمستأجر B

    SetTenant(tenantA);                          // بدّل السياق
    var visible = await _db.Products.FindAsync(p.Id);

    Assert.Null(visible);                        // الفلتر العالمي يخفيه
}

[Fact]
public async Task Suspended_Tenant_Is_Rejected_At_Middleware()
{
    SetTenantStatus(tenantA, TenantStatus.Suspended);
    var res = await _client.GetAsync("/api/v1/products");
    Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);   // 403
}
```

---

## 9) التخصيص لكل مستأجر (Per-Tenant Config)

كل مستأجر يُخصّص سلوكه عبر `TenantSettings` (Data-driven، لا كود):

| العنصر | المصدر |
|--------|--------|
| العملة | `TenantSettings.Currency` |
| نسبة الضريبة الافتراضية | `TenantSettings.DefaultTaxRate` |
| المنطقة الزمنية | `TenantSettings.TimeZone` |
| اللغة/اللوكال | `TenantSettings.Locale` |
| الثيم (شعار/ألوان) | `TenantSettings.ThemeJson` (يُطبَّق في الواجهة لاحقاً) |

---

## 10) قابلية التوسّع المستقبلية (Scalability)

- النموذج الحالي **Shared Schema** يخدم عملاء كثراً على قاعدة واحدة بكفاءة (فهرسة `(TenantId)` على كل جدول + RCSI).
- **ترحيل تدريجي** لعميل كبير لقاعدة منفصلة: بما أن كل صف يحمل `TenantId`، يُنقَل بمرشّح `WHERE TenantId=@x` دون تغيير الكود — فقط توجيه اتصاله عبر `ITenantStore` (خريطة `TenantId → ConnectionString`).
- **RLS** كطبقة تقوية جاهزة للتفعيل (تصميمها في [11-Security-Architecture.md](11-Security-Architecture.md)).
- **الفروع (`StoreId`)** تُضاف انتقائياً للكيانات المعنية دون لمس بقية النظام.

---

## 11) قائمة تحقّق العزل (Isolation Checklist)

عند إضافة أي جدول/ميزة أعمال:

- [ ] الكيان يرث `BaseEntity` (وبالتالي عليه `TenantId` والفلتر العالمي).
- [ ] لا `IgnoreQueryFilters()` ولا `FromSqlRaw` بلا شرط `TenantId` مراجَع.
- [ ] كل مفتاح Cache يبدأ بـ `t:{TenantId}:`.
- [ ] أي Background Job يضبط سياق المستأجر قبل الاستعلام.
- [ ] اختبار عزل (قراءة + كتابة) مُضاف ويمرّ في CI.
- [ ] عمليات مالك النظام لا تختم `TenantId` تلقائياً بالخطأ.

---

## 12) خلاصة القرارات

| القرار | الاختيار | لماذا |
|--------|----------|-------|
| نموذج العزل | Shared DB, Shared Schema, Row-Level | أفضل تكلفة/كثافة مع مسار ترقية |
| مفتاح العزل | `TenantId` في كل جدول أعمال | بساطة، أداء، قابلية sharding لاحقاً |
| الفروع | لا `StoreId` الآن | `Tenant` حدّ كافٍ؛ يُضاف انتقائياً لاحقاً |
| آلية العزل | EF Core Global Query Filter | حماية تلقائية بلا كتابة يدوية |
| RLS | مؤجَّل (تصميم جاهز) | بساطة النشر الآن، تقوية عند الحاجة |
| **التحكّم بالعملاء** | **`Tenants.Status` يدوي — لا SaaS** | لا اشتراك/فوترة/دفع؛ مالك النظام يتحكّم |
| استخراج المستأجر | JWT claim | موثوق، لا يأتي من المستخدم |

---

_يلتزم بالمرجع الحاكم [05-Database-Design.md](05-Database-Design.md). العزل قرار معماري أساسي ("Tenant Isolation First"). يُكمّله [10-Identity-RBAC.md](10-Identity-RBAC.md) و[11-Security-Architecture.md](11-Security-Architecture.md)._
