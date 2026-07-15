# 24 — Multi-Tenant (تعدّد المستأجرين وعزل البيانات والتخصيص)

> **الملف المحوري للمنصّة.** يشرح كيف يخدم **مشروع واحد + رابط واحد + نشر واحد** آلاف المحلات بعزل تام للبيانات وتخصيص كامل للمظهر — دون أي فرع كود لكل عميل. هذا هو القرار المعماري الذي يميّز *Smart ERP POS* عن الأنظمة On-Premise التقليدية.

> يلتزم بالمرجع الحاكم [04-Database-Design.md](04-Database-Design.md) (`TenantId` في كل جدول أعمال، Global Query Filter، Soft Delete) ويكمّله [03-Database-Strategy.md](03-Database-Strategy.md), [27-Security.md](27-Security.md), [07-Store-Management.md](07-Store-Management.md).

---

## 1) المفهوم الجوهري (Core Concept)

```
                         نشر واحد (Single Deployment)
                                    │
                     ┌──────────────┴──────────────┐
                     │      app.smartpos.app        │
                     │   (كود واحد، قاعدة واحدة)     │
                     └──────────────┬──────────────┘
                                    │
     ┌───────────────┬─────────────┼─────────────┬───────────────┐
     ▼               ▼             ▼             ▼               ▼
 Tenant 1001    Tenant 1002   Tenant 1003   Tenant 1004   ... آلاف
 alnoor.*       hayat.*       superx.*      cafe.*
 (سوبرماركت)    (صيدلية)      (سوبرماركت)   (مطعم)
 شعار/ألوان     شعار/ألوان    شعار/ألوان    شعار/ألوان
 بياناته وحده   بياناته وحده  بياناته وحده  بياناته وحده
```

- **Tenant** = الشركة/الحساب التجاري (المستأجر). له `TenantId`.
- **Store** = فرع تابع للمستأجر. له `StoreId` (ينتمي دائماً لـ `TenantId`).
- كل صفّ بيانات أعمال يحمل `TenantId` (إلزامي) و`StoreId` (اختياري حسب النطاق).
- إضافة عميل جديد = صفّ في `Tenants` (انظر [07-Store-Management.md](07-Store-Management.md)) — **لا كود، لا نشر**.

نموذج العزل المعتمد: **Single Database, Shared Schema, Row-Level Isolation via `TenantId`** — مع تصميم يسمح لاحقاً بالانتقال إلى قاعدة/شارد منفصل للعملاء الكبار دون إعادة كتابة.

---

## 2) الفرق بين TenantId و StoreId

| البُعد | `TenantId` | `StoreId` |
|-------|-----------|-----------|
| المعنى | الشركة (حدود العزل الأمني) | الفرع (تقسيم تشغيلي داخل الشركة) |
| الإلزام | **NOT NULL** في كل جدول أعمال | `NULL` للمشترك على مستوى الشركة، `NOT NULL` للخاص بفرع |
| العزل | **حدّ أمني صارم** — لا تسرّب بين مستأجرين | تصفية تشغيلية داخل نفس المستأجر (يراها المدير) |
| مصدره | من سياق الطلب (subdomain/JWT) | من اختيار المستخدم أو فرعه الافتراضي |

> القاعدة الذهبية: **`TenantId` لا يأتي أبداً من المستخدم/العميل** — يُستخرج من سياق موثوق (JWT claim/subdomain) خادم-جانبي. أي endpoint يقبل `tenantId` من الجسم = ثغرة عزل.

---

## 3) استخراج المستأجر الحالي (Tenant Resolution)

عند وصول كل طلب، يجب تحديد المستأجر **قبل** أي استعلام. مصادر الاستخراج بترتيب الأولوية:

| المصدر | متى يُستخدم | كيف |
|--------|-------------|-----|
| **JWT Claim `tenant_id`** | بعد تسجيل الدخول (الحالة الغالبة) | مضمَّن ومُوقَّع داخل التوكن — لا يُزوَّر بدون كسر التوقيع |
| **Subdomain** | صفحة الدخول، الطلبات قبل المصادقة | `alnoor.smartpos.app` → lookup في `Tenants.Subdomain` |
| **Custom Domain** | العملاء ذوو الدومين الخاص | `pos.alnoor.com` → lookup في `Tenants.CustomDomain` |
| **Header `X-Tenant`** | استدعاءات آلة-لآلة (M2M) / اختبارات | يُقبل فقط مع API Key موثوق — لا من متصفّح عام |

**واجهة `ITenantProvider`** — نقطة الحقيقة الوحيدة لهوية المستأجر في الطلب:

```csharp
public interface ITenantProvider
{
    long   CurrentTenantId { get; }   // يُرمى استثناء إن لم يُحلّ
    long?  CurrentStoreId  { get; }   // اختياري (فرع مختار)
    string CurrentSubdomain { get; }
    bool   IsResolved { get; }
}
```

**Middleware للاستخراج (يُسجَّل مبكراً في الـ pipeline):**

```csharp
public class TenantResolutionMiddleware
{
    public async Task Invoke(HttpContext ctx, ITenantStore store, TenantContext tc)
    {
        // 1) الأولوية للـ JWT بعد المصادقة
        var claim = ctx.User.FindFirst("tenant_id")?.Value;
        if (long.TryParse(claim, out var tid))
            tc.Set(tid, storeId: ParseStore(ctx));
        else
        {
            // 2) قبل المصادقة: استخرج من الـ host (subdomain / custom domain)
            var host = ctx.Request.Host.Host;                 // alnoor.smartpos.app
            var tenant = await store.ResolveByHostAsync(host); // Cache-first lookup
            if (tenant is null) { ctx.Response.StatusCode = 404; return; }
            if (!tenant.IsActive) { ctx.Response.StatusCode = 403; return; }
            tc.Set(tenant.Id, storeId: null);
        }
        await _next(ctx);
    }
}
```

- `ITenantStore.ResolveByHostAsync` يخدم من **Cache** (خريطة `host → TenantId`) لتفادي ضربة DB لكل طلب.
- مستأجر معطّل (`IsActive=0`) يُرفض هنا مبكراً (403) قبل أي منطق أعمال.

---

## 4) عزل البيانات — الطبقة الأولى: EF Core Global Query Filter

كل كيان أعمال يرث `BaseEntity` (فيه `TenantId` و`IsDeleted`). يُطبَّق فلتر عالمي تلقائي على **كل** استعلام:

```csharp
public class AppDbContext : DbContext
{
    private readonly ITenantProvider _tenant;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        foreach (var et in mb.Model.GetEntityTypes()
                                   .Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)))
        {
            mb.Entity(et.ClrType).HasQueryFilter(
                BuildTenantFilter(et.ClrType));   // e => e.TenantId == _tenant.CurrentTenantId && !e.IsDeleted
        }
    }

    // ختم TenantId تلقائياً عند الإضافة — لا يُترك للمطوّر
    public override int SaveChanges()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.TenantId = _tenant.CurrentTenantId;
            if (entry.State == EntityState.Deleted)          // تحويل الحذف إلى Soft Delete
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedDate = DateTime.UtcNow;
            }
        }
        return base.SaveChanges();
    }
}
```

**ما الذي يضمنه هذا:**

1. أي `context.Products.ToList()` يترجَم تلقائياً إلى `... WHERE TenantId = @currentTenant AND IsDeleted = 0` — المطوّر **لا يكتب** شرط العزل يدوياً (وبالتالي لا ينساه).
2. `TenantId` يُختَم تلقائياً عند الإضافة — لا يمكن حقن قيمة مستأجر آخر.
3. الحذف يتحوّل تلقائياً لـ Soft Delete (حفاظاً على السجل المحاسبي).

> **تحذير حاسم:** أي استعلام خام (`FromSqlRaw`) أو `IgnoreQueryFilters()` **يتخطّى** هذه الحماية. يُمنع استخدامهما إلا بمراجعة أمنية، ويجب أن يضيفا شرط `TenantId` يدوياً. هذه هي أخطر نقطة تسرّب محتملة (§8).

---

## 5) عزل البيانات — الطبقة الثانية: SQL Server RLS (Defense-in-Depth)

الفلتر في EF Core يحمي مسار التطبيق فقط. **RLS على مستوى قاعدة البيانات** يحمي حتى لو أخطأ الكود أو نُفِّذ استعلام خام — دفاع في العمق.

```sql
-- 1) دالة تنبؤية: تُرجع صفاً واحداً فقط إن كان TenantId مطابقاً لسياق الجلسة
CREATE FUNCTION dbo.fn_TenantPredicate(@TenantId BIGINT)
    RETURNS TABLE WITH SCHEMABINDING
AS
    RETURN SELECT 1 AS ok
           WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS BIGINT);
GO

-- 2) سياسة الأمان: تُطبَّق كـ FILTER (قراءة) و BLOCK (كتابة) على كل جدول أعمال
CREATE SECURITY POLICY dbo.TenantSecurityPolicy
    ADD FILTER PREDICATE dbo.fn_TenantPredicate([TenantId]) ON dbo.Products,
    ADD BLOCK  PREDICATE dbo.fn_TenantPredicate([TenantId]) ON dbo.Products AFTER INSERT,
    ADD FILTER PREDICATE dbo.fn_TenantPredicate([TenantId]) ON dbo.SalesInvoices,
    ADD BLOCK  PREDICATE dbo.fn_TenantPredicate([TenantId]) ON dbo.SalesInvoices AFTER INSERT
    -- ... يُضاف كل جدول أعمال
    WITH (STATE = ON);
GO
```

**ضبط سياق الجلسة** — يجب أن يمرّر التطبيق `TenantId` لكل اتصال قبل أي استعلام:

```csharp
// عند فتح الاتصال (DbConnection interceptor)
await using var cmd = connection.CreateCommand();
cmd.CommandText = "EXEC sys.sp_set_session_context @key=N'TenantId', @value=@tid;";
cmd.Parameters.AddWithValue("@tid", _tenant.CurrentTenantId);
await cmd.ExecuteNonQueryAsync();
```

- **FILTER PREDICATE:** يخفي صفوف المستأجرين الآخرين من كل `SELECT` تلقائياً على مستوى المحرّك.
- **BLOCK PREDICATE:** يمنع `INSERT`/`UPDATE` بقيمة `TenantId` مخالفة لسياق الجلسة — يفشل بخطأ.
- الناتج: حتى لو تسرّب استعلام خام بلا شرط `TenantId`، **RLS يمنع رؤية/كتابة بيانات مستأجر آخر**.

---

## 6) مخطط ASCII — تدفّق طلب كامل (Login → TenantId Filtering)

```
┌────────────────────────────────────────────────────────────────────────┐
│  المتصفّح: https://alnoor.smartpos.app/products                          │
└───────────────────────────────┬────────────────────────────────────────┘
                                 │  (1) الطلب يصل للخادم
                                 ▼
        ┌────────────────────────────────────────────────┐
        │  TenantResolutionMiddleware                    │
        │  host = alnoor.smartpos.app                    │
        │  ── JWT؟ ── نعم ─► tenant_id claim = 1001       │
        │        └─ لا  ─► lookup Subdomain 'alnoor'→1001 │
        │  IsActive? ── لا ─► 403 Suspended               │
        └───────────────────────────────┬────────────────┘
                                        │ (2) TenantContext = {1001, storeId}
                                        ▼
        ┌────────────────────────────────────────────────┐
        │  Authentication / Authorization                │
        │  JWT صالح؟  الصلاحية Products.View موجودة؟       │
        └───────────────────────────────┬────────────────┘
                                        │ (3)
                                        ▼
        ┌────────────────────────────────────────────────┐
        │  Controller / MediatR Handler                  │
        │  _db.Products.Where(p => p.Name.Contains(q))   │
        └───────────────────────────────┬────────────────┘
                                        │ (4) EF يضيف الفلتر العالمي
                                        ▼
        ┌────────────────────────────────────────────────┐
        │  EF Core Global Query Filter                   │
        │  ... AND TenantId = 1001 AND IsDeleted = 0     │
        └───────────────────────────────┬────────────────┘
                                        │ (5) قبل تنفيذ SQL: ضبط سياق الجلسة
                                        ▼
        ┌────────────────────────────────────────────────┐
        │  Connection Interceptor                        │
        │  sp_set_session_context 'TenantId' = 1001      │
        └───────────────────────────────┬────────────────┘
                                        │ (6)
                                        ▼
        ┌────────────────────────────────────────────────┐
        │  SQL Server + RLS Security Policy               │
        │  FILTER PREDICATE يطبّق TenantId=1001 مجدداً      │
        │  (طبقة دفاع ثانية — حتى لو غاب فلتر التطبيق)     │
        └───────────────────────────────┬────────────────┘
                                        │ (7) صفوف Tenant 1001 فقط
                                        ▼
                        [ نتائج معزولة تماماً → للمستخدم ]
```

**ثلاث بوّابات عزل متتالية:** (أ) استخراج موثوق للمستأجر، (ب) فلتر EF Core التلقائي، (ج) RLS على مستوى المحرّك. تسرّب البيانات يتطلّب فشل الثلاثة معاً.

---

## 7) التخصيص لكل مستأجر (Per-Tenant Branding / White-Label)

كل مستأجر يرى النظام بهويته البصرية — دون فرع كود، عبر قراءة إعداداته من `TenantSettings` (فئة `THEME`, انظر [23-Settings.md](23-Settings.md)):

| العنصر | المصدر | كيف يُطبَّق |
|--------|--------|-------------|
| **الشعار (Logo)** | `theme.logoUrl` | يُحقَن في الترويسة وصفحة الدخول والإيصالات |
| **الألوان (Primary/Sidebar)** | `theme.primaryColor` | تُحقَن كـ CSS Custom Properties (`--primary`) في `<head>` |
| **صفحة الدخول (Login)** | `theme.loginBackgroundUrl` | خلفية مخصّصة لكل مستأجر عند `subdomain.*` |
| **اسم النظام** | `theme.systemName` | عنوان الصفحة والترويسة |
| **الخلفية (Background)** | `theme.loginBackgroundUrl` | صورة/تدرّج مخصّص |

**آلية الحقن (لا إعادة نشر):**

```html
<!-- Layout: يُقرأ theme الحالي من TenantSettings (مُخزَّن مؤقتاً) ويُحقَن كمتغيّرات -->
<style>
  :root {
    --primary: @Model.Theme.PrimaryColor;      /* #0F766E */
    --sidebar: @Model.Theme.SidebarStyle;
  }
</style>
<img src="@Model.Theme.LogoUrl" alt="@Model.Theme.SystemName" class="brand-logo" />
```

**رفع Logo / Background:**

1. رفع عبر `POST /api/settings/logo` → تحقّق MIME + الحجم (≤2MB).
2. يُخزَّن في **Blob/File storage** بمسار معزول لكل مستأجر: `/assets/tenant/{tenantId}/logo.png`.
3. يُحدَّث المسار في `TenantSettings.THEME.logoUrl` (JSON) — لا يُخزَّن الملف في DB.
4. إبطال Cache السمة → يظهر التغيير فوراً لكل مستخدمي المستأجر.

> عزل الأصول: مسار الملفات مُشتَّت بـ `tenantId`، ويُخدَم عبر رابط موقّع/محقّق للصلاحية لمنع وصول مستأجر لأصول آخر.

---

## 8) Custom Domain لكل عميل (لاحقاً)

العميل الكبير قد يريد `pos.alnoor.com` بدل `alnoor.smartpos.app`:

| الخطوة | التفصيل |
|--------|---------|
| 1. التحقّق من الملكية | العميل يضيف سجل `TXT`/`CNAME` يثبت ملكيته للدومين |
| 2. تسجيل الدومين | يُخزَّن في `Tenants.CustomDomain` (فريد عالمياً — `UX_Tenants_CustomDomain`) |
| 3. شهادة TLS | إصدار تلقائي (ACME/Let's Encrypt) لكل دومين مخصّص |
| 4. الاستخراج | `TenantResolutionMiddleware` يبحث في `CustomDomain` كما يبحث في `Subdomain` |

الكود لا يتغيّر — فقط صف في `Tenants` وتوجيه DNS/TLS على مستوى البنية التحتية. مصمَّم من الآن (العمود موجود) وإن فُعِّل في مرحلة لاحقة.

---

## 9) مخاطر التسرّب (Leakage Risks) وكيف نمنعها

| الخطر | السيناريو | المنع |
|------|-----------|-------|
| **`TenantId` من المستخدم** | endpoint يقرأ `tenantId` من الـ body/query | يُتجاهل دائماً؛ المصدر الوحيد `ITenantProvider` (JWT/subdomain) |
| **`IgnoreQueryFilters()`** | مطوّر يتخطّى الفلتر العالمي | ممنوع بلا مراجعة أمنية؛ Roslyn Analyzer يرصده في الـ CI؛ RLS يمسكه |
| **`FromSqlRaw` بلا شرط** | استعلام خام ينسى `TenantId` | RLS (FILTER PREDICATE) يمنع رؤية غير المستأجر تلقائياً |
| **جداول بلا `TenantId`** | جدول أعمال جديد نُسي عموده | مراجعة Migration + اختبار آلي يفحص أن كل `BaseEntity` عليه فلتر |
| **Background Jobs** | وظيفة Hangfire بلا سياق مستأجر | كل Job يحمل `TenantId` صراحةً ويضبط `ITenantProvider` قبل أي استعلام |
| **Caching مشترك** | مفتاح Cache بلا `TenantId` | كل مفتاح Cache يبدأ بـ `t:{TenantId}:` إجبارياً |
| **الأصول (Logo/Files)** | وصول لمسار أصل مستأجر آخر | مسارات مُشتَّتة بـ `tenantId` + تحقّق صلاحية عند الخدمة |

### اختبارات العزل (Isolation Tests) — إلزامية في CI

```csharp
[Fact]
public async Task Tenant_A_Cannot_See_Tenant_B_Data()
{
    // Arrange: أنشئ منتجاً للمستأجر B
    SetTenant(tenantB); var p = await CreateProduct("سرّي");

    // Act: بدّل السياق للمستأجر A واستعلم
    SetTenant(tenantA);
    var visible = await _db.Products.FindAsync(p.Id);      // عبر الفلتر العالمي
    var rawVisible = await _db.Products
        .FromSqlRaw("SELECT * FROM Products WHERE Id={0}", p.Id) // يفعّل RLS
        .ToListAsync();

    // Assert: لا EF ولا RLS يسمح برؤيته
    Assert.Null(visible);
    Assert.Empty(rawVisible);
}
```

- **اختبار عزل القراءة:** مستأجر A لا يرى بيانات B (لا عبر EF ولا عبر SQL خام).
- **اختبار عزل الكتابة:** محاولة `INSERT` بـ `TenantId` مخالف تفشل بـ BLOCK PREDICATE.
- **اختبار الختم التلقائي:** إضافة كيان بلا `TenantId` صريح تُختَم بمستأجر السياق لا بغيره.
- تُشغَّل هذه الاختبارات في كل بناء (CI) — أي كسر للعزل يوقف الدمج.

---

## 10) قابلية التوسّع (Scalability & Future Sharding)

- النموذج الحالي **Shared Schema** يخدم آلاف المستأجرين على قاعدة واحدة بكفاءة (فهرسة `(TenantId, StoreId)` على كل جدول، RCSI لتقليل الأقفال).
- التصميم يسمح بـ **الترحيل التدريجي** للعملاء الكبار إلى قاعدة/شارد منفصل: بما أن كل صف يحمل `TenantId`، يمكن نقل بيانات مستأجر بالكامل بمرشّح `WHERE TenantId=@x` دون تغيير الكود — فقط توجيه اتصاله لقاعدة أخرى عبر `ITenantStore` (خريطة `TenantId → ConnectionString`).
- الـ Connection Routing يُبنى فوق `ITenantProvider` بحيث يختار سلسلة الاتصال حسب المستأجر (مصمَّم من الآن، يُفعَّل عند الحاجة).

---

## 11) قائمة تحقّق العزل (Isolation Checklist للمطوّر)

عند إضافة أي جدول/ميزة أعمال جديدة، تأكّد من:

- [ ] الكيان يرث `BaseEntity` (وبالتالي عليه `TenantId` والفلتر العالمي).
- [ ] الجدول مضاف إلى `TenantSecurityPolicy` (RLS FILTER + BLOCK).
- [ ] لا `IgnoreQueryFilters()` ولا `FromSqlRaw` بلا شرط `TenantId` مراجَع.
- [ ] كل مفتاح Cache يبدأ بـ `t:{TenantId}:`.
- [ ] أي Background Job يضبط سياق المستأجر قبل الاستعلام.
- [ ] اختبار عزل (قراءة + كتابة) مُضاف ويمرّ في CI.
- [ ] الأصول (ملفات) في مسار مُشتَّت بـ `tenantId` مع تحقّق صلاحية.

---

## 12) خلاصة القرارات المعمارية

| القرار | الاختيار | لماذا |
|--------|----------|-------|
| نموذج العزل | Shared DB, Shared Schema, Row-Level | أفضل تكلفة/كثافة لآلاف SMEs مع مسار ترقية |
| مفتاح العزل | `TenantId` في كل جدول أعمال | بساطة، أداء، قابلية Sharding لاحقاً |
| الطبقة 1 | EF Core Global Query Filter | حماية تلقائية بلا كتابة يدوية |
| الطبقة 2 | SQL Server RLS | دفاع في العمق حتى ضدّ استعلام خام |
| استخراج المستأجر | JWT claim ثم subdomain/custom domain | موثوق، لا يأتي من المستخدم |
| التخصيص | JSON في `TenantSettings` + حقن CSS/Assets | White-label بلا إعادة نشر |
| الأمان | اختبارات عزل آلية في CI | إثبات صفر تسرّب باستمرار |

---

_يلتزم بالمرجع الحاكم [04-Database-Design.md](04-Database-Design.md). العزل قرار معماري أساسي لا ميزة لاحقة (المبدأ الحاكم "Tenant Isolation First")._
