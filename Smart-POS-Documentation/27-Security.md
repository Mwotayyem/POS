# 27 — Security (الأمان — تحليل عميق)

> الأمان في **Smart ERP POS** ليس ميزة بل **شرط بقاء**: نظام مالي متعدّد المستأجرين يخدم آلاف الشركات من قاعدة بيانات واحدة. تسريب مستأجر واحد لبيانات آخر = فشل وجودي. هذا الملف يعالج **OWASP Top 10 (2021)** واحدةً واحدة مع الحلّ **المطبَّق في هذا النظام تحديداً**، ثم أمان تعدّد المستأجرين (Multi-Tenant Security) بعمق.

---

## 1) نموذج التهديد (Threat Model — باختصار)

| الأصل المهدَّد | التهديد الرئيس | الأثر |
|----------------|----------------|-------|
| بيانات المستأجر (فواتير، عملاء) | تسرّب بين المستأجرين (Cross-Tenant) | كارثي — فقد ثقة وقانوني |
| الرموز والجلسات | سرقة JWT، تصعيد صلاحيات | انتحال هوية |
| بيانات الدفع/الحسّاسة | كشف at-rest / in-transit | خرق تنظيمي |
| توفّر الخدمة | Brute Force / DoS | تعطّل تشغيلي |
| سلامة السجلّ المحاسبي | تلاعب/حذف | تزوير مالي |

**المبادئ الحاكمة:** *Defense-in-Depth* (طبقات متعدّدة)، *Least Privilege*، *Secure by Default*، *Fail Closed* (عند الشكّ نمنع لا نسمح).

---

## 2) OWASP Top 10 (2021) — المعالجة الخاصّة بالنظام

### A01 — Broken Access Control (كسر التحكّم بالوصول)

**الأخطر في نظامنا** لأنه يشمل تسرّب المستأجرين و IDOR.

- **العزل الإجباري**: كل استعلام يمرّ عبر **EF Core Global Query Filter** على `TenantId` (انظر [04-Database-Design.md](04-Database-Design.md)). لا استعلام يخرج بلا فلتر المستأجر.
  ```csharp
  modelBuilder.Entity<TEntity>()
      .HasQueryFilter(e => e.TenantId == _tenantProvider.CurrentTenantId && !e.IsDeleted);
  ```
- **`TenantId` من الرمز حصراً** — لا يُقبَل من body/route/query إطلاقاً. يُستخرَج من Claim داخل JWT عبر `ITenantProvider`.
- **منع IDOR**: الـ URL يستخدم `PublicId (GUID)` لا `Id (BIGINT)` المتسلسل. حتى لو خمّن مهاجم GUID، فلتر المستأجر يمنع الوصول.
- **RBAC دقيق**: التفويض عبر Policy-based Authorization على الصلاحيات (Permissions) لا الأدوار فقط (انظر [06-Roles-And-Permissions.md](06-Roles-And-Permissions.md)).
  ```csharp
  [Authorize(Policy = "products.delete")]
  public async Task<IActionResult> Delete(Guid publicId) { ... }
  ```
- **Fail Closed**: غياب صلاحية = `403`؛ لا وصول افتراضي.
- **طبقة دفاع ثانية**: SQL Server **Row-Level Security** (§8) تمنع التسرّب حتى لو أُهمل الفلتر برمجياً.

### A02 — Cryptographic Failures (إخفاقات التشفير / كشف البيانات الحسّاسة)

- **In-Transit**: TLS 1.2+ إجباري (`UseHsts` + إعادة توجيه HTTPS). لا HTTP في الإنتاج.
- **At-Rest**:
  - كلمات المرور: **ASP.NET Identity** بـ `PasswordHasher` (PBKDF2/Argon2-grade) — لا تخزين نصّي أبداً.
  - البيانات الحسّاسة الحقلية (أرقام هوية، حسابات بنكية): تشفير عبر **Always Encrypted** في SQL Server أو تطبيق `IDataProtectionProvider`.
  - قاعدة البيانات كاملة: **TDE (Transparent Data Encryption)** لحماية الملفات والنسخ الاحتياطية.
- **الأسرار**: مفاتيح التوقيع والتشفير في Key Vault، مع تدوير دوري (Key Rotation) — انظر [26-Deployment.md](26-Deployment.md).
- **لا تسريب في السجلّات**: Serilog يُخفّي (mask) كلمات المرور والرموز وأرقام البطاقات.

### A03 — Injection (الحقن)

- **SQL Injection**: **ممنوع** بناء استعلامات بدمج نصّي. الوصول حصراً عبر:
  - **EF Core LINQ** (parameterized تلقائياً).
  - عند الحاجة لـ raw SQL: `FromSqlInterpolated` / `SqlParameter` **فقط** — لا `FromSqlRaw` مع concatenation.
  ```csharp
  // صحيح — مُعامَل
  db.Products.FromSql($"SELECT * FROM Products WHERE Barcode = {barcode}");
  // خطأ — ممنوع منعاً باتاً
  db.Products.FromSqlRaw("SELECT * FROM Products WHERE Barcode = '" + barcode + "'");
  ```
- الإجراءات المخزّنة تُستدعى ببارامترات مسمّاة فقط.
- **XSS** و**Command/LDAP Injection** مغطّاة تحت مبدأ: كل مدخل غير موثوق يُعامَل كبيانات لا كتعليمات.

### A04 — Insecure Design (تصميم غير آمن)

- التصميم يفرض الأمان بنيوياً: العزل، Soft Delete، Audit، والمعاملات الذرّية جزء من **قالب الجدول القياسي** لا خياراً.
- **Rate Limiting** و**تحقّق مركزي** مصمَّمان في الأساس (انظر [25-API-Design.md](25-API-Design.md)).
- نمذجة تهديدات (Threat Modeling) لكل ميزة مالية قبل التنفيذ.

### A05 — Security Misconfiguration (سوء التهيئة)

- **Secure by Default**: Swagger معطّل/محميّ في الإنتاج، رؤوس الخطأ لا تكشف stack trace، `AddServerHeader = false`.
- رؤوس أمان عبر Middleware: `Content-Security-Policy`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Strict-Transport-Security`.
  ```csharp
  app.Use(async (ctx, next) => {
      ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
      ctx.Response.Headers["X-Frame-Options"] = "DENY";
      ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
      await next();
  });
  app.UseHsts();
  ```
- `ValidateOnStart` يرفض الإقلاع عند نقص إعداد أمان حرج.
- الحاوية تعمل بمستخدم غير جذري (Least Privilege) — انظر Dockerfile في [26-Deployment.md](26-Deployment.md).

### A06 — Vulnerable & Outdated Components (مكوّنات معطوبة/قديمة)

- **Dependency Scanning** في CI (GitLab Dependency Scanning / `dotnet list package --vulnerable`).
- تحديث دوري للحزم و runtime؛ رفض دمج PR يُدخل حزمة ذات ثغرة معروفة (CVE).
- تثبيت إصدارات الحزم (lock) وإعادة بناء الصورة عند ترقيع أمني.

### A07 — Identification & Authentication Failures (إخفاقات المصادقة)

- **ASP.NET Identity + JWT** مع Refresh Tokens قصيرة العمر (انظر [05-Authentication.md](05-Authentication.md)).
- سياسة كلمات مرور قوية + **قفل الحساب** بعد محاولات فاشلة (Brute Force — §5).
- دعم **MFA/OTP**، وإبطال الرموز (Token Revocation) عبر جدول `RefreshTokens` مع حالة إبطال.
- الرموز موقّعة (HS256/RS256) ويُتحقَّق من `issuer/audience/expiry/tenant` في كل طلب.

### A08 — Software & Data Integrity Failures (سلامة البرمجيات والبيانات)

- بناء موقَّع، صور Docker من مصادر موثوقة (Digest pinning).
- **Optimistic Concurrency** عبر `ConcurrencyStamp ROWVERSION` يمنع الكتابة على بيانات قديمة (`409 RES_CONFLICT`).
- **Soft Delete** + سجلّ تدقيق يمنعان التلاعب الصامت بالسجلّ المحاسبي.

### A09 — Security Logging & Monitoring Failures (إخفاقات التسجيل والمراقبة)

- **Serilog** structured logging مع `CorrelationId/TenantId/UserId` لكل حدث (انظر [25-API-Design.md](25-API-Design.md)).
- أحداث الأمان (فشل دخول، رفض صلاحية، قفل حساب، تجاوز معدّل) تُسجَّل كأحداث مميّزة وتُنبَّه عليها.
- **AuditLogs** لكل عملية مالية حسّاسة (§7). مراقبة APM + تنبيهات على أنماط شاذّة.

### A10 — Server-Side Request Forgery (SSRF)

- أي طلب صادر من الخادم (webhooks، جلب صور، تكاملات) يمرّ عبر **allow-list** للنطاقات، مع منع الوصول لعناوين داخلية (169.254.x، 127.0.0.1، شبكات خاصّة).
- التحقّق من URL المُدخَل من المستأجر قبل أي fetch.

---

## 3) منع SQL Injection (تفصيل)

| المستوى | الضمانة |
|---------|---------|
| ORM | EF Core يُعامِل كل قيمة كبارامتر تلقائياً |
| Raw SQL | `FromSqlInterpolated` / `SqlParameter` فقط |
| Stored Procs | بارامترات مسمّاة، لا `EXEC(@dynamicSql)` غير مُعامَل |
| المراجعة | مراجعة كود إلزامية ترفض أي concatenation في SQL |
| الحدّ الأدنى للامتيازات | مستخدم DB للتطبيق يملك DML فقط، لا DDL في الإنتاج |

---

## 4) منع XSS و CSRF

**XSS:**
- API يُعيد JSON فقط (لا HTML) — سطح XSS منخفض.
- واجهة MVC (Back-office): Razor يُرمّز المخرجات تلقائياً (HTML-encoding)؛ لا `@Html.Raw` على مدخلات المستخدم.
- **CSP** صارمة تمنع تنفيذ سكربتات inline غير موثوقة.

**CSRF:**
- API عديم الحالة يعتمد **JWT في رأس `Authorization`** (لا كوكيز جلسة) ⇒ محصّن ضدّ CSRF الكلاسيكي.
- واجهة MVC التي تستخدم كوكيز: **Antiforgery Token** إجباري على كل POST.
  ```csharp
  builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
  ```
- كوكيز المصادقة (إن وُجدت): `HttpOnly` + `Secure` + `SameSite=Strict`.

---

## 5) منع Brute Force + Rate Limiting

طبقتان متكاملتان:

1. **قفل الحساب (Account Lockout)** عبr ASP.NET Identity:
   ```csharp
   options.Lockout.MaxFailedAccessAttempts = 5;
   options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
   options.Lockout.AllowedForNewUsers = true;
   ```
   بعد 5 محاولات فاشلة يُقفَل الحساب 15 دقيقة، ويُعاد `423 AUTH_LOCKED`.

2. **Rate Limiting** على مسار الدخول (5 محاولات/دقيقة لكل IP+حساب) — انظر سياسة `auth` في [25-API-Design.md](25-API-Design.md).

إضافات: تأخير تصاعدي (Backoff)، CAPTCHA بعد عتبة، وتنبيه أمني عند تكرار الفشل من IP واحد على عدّة حسابات (Credential Stuffing).

---

## 6) تشفير البيانات الحسّاسة (At-Rest / In-Transit)

| الطبقة | الآلية |
|--------|--------|
| النقل (In-Transit) | TLS 1.2+ إلزامي، HSTS، إعادة توجيه HTTPS |
| القرص كامل (DB) | **TDE** — يشمل ملفات البيانات والـ log والنسخ الاحتياطية |
| حقول حسّاسة محدّدة | **Always Encrypted** (SQL Server) أو `IDataProtection` — تشفير على مستوى العمود |
| كلمات المرور | تجزئة (Hash) قويّة عبر Identity — لا تشفير قابل للعكس |
| الأسرار التطبيقية | Key Vault + تدوير المفاتيح |
| النسخ الاحتياطية | مشفّرة ومخزّنة خارج الموقع (انظر [28-Backup-And-Restore.md](28-Backup-And-Restore.md)) |

**تدوير المفاتيح (Key Rotation):** مفتاح توقيع JWT ومفاتيح التشفير تُدوَّر دورياً، مع دعم مفتاحين (current + previous) أثناء الانتقال لتفادي إبطال الجلسات فجأة.

---

## 7) سجلّ التدقيق (Audit) لكل عملية مهمّة

كل عملية مالية/حسّاسة تُسجَّل في `AuditLogs` (غير قابل للتعديل — Append-only):

```sql
CREATE TABLE [dbo].[AuditLogs]
(
    [Id]          BIGINT IDENTITY(1,1) NOT NULL,
    [TenantId]    BIGINT       NOT NULL,
    [StoreId]     BIGINT       NULL,
    [UserId]      BIGINT       NULL,
    [Action]      VARCHAR(50)  NOT NULL,   -- 'SALE_CREATED','PRICE_CHANGED','INVOICE_VOIDED'
    [EntityType]  VARCHAR(50)  NOT NULL,   -- 'SalesInvoice','Product'
    [EntityId]    BIGINT       NULL,
    [OldValues]   NVARCHAR(MAX) NULL,      -- JSON snapshot قبل
    [NewValues]   NVARCHAR(MAX) NULL,      -- JSON snapshot بعد
    [IpAddress]   VARCHAR(45)  NULL,
    [CorrelationId] VARCHAR(64) NULL,      -- يربط بالسجلّ والاستجابة
    [CreatedDate] DATETIME2(3) NOT NULL CONSTRAINT DF_AuditLogs_CreatedDate DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([Id])
);
```

**قواعد:**

- يُلتقط تلقائياً عبر `SaveChangesInterceptor` في EF Core لكل كيان يُعلَّم بـ `[Auditable]`.
- يُسجَّل: **من** (UserId)، **ماذا** (Action)، **على ماذا** (Entity)، **القيم قبل/بعد**، **متى** (UTC)، **من أين** (IP)، **CorrelationId**.
- الجدول Append-only — لا `UPDATE`/`DELETE` عليه (يُفرَض بصلاحيات DB وTrigger مانع).
- عمليات إجبارية التدقيق: بيع، مرتجع، تعديل سعر، إبطال فاتورة، تسوية مخزون، تغيير صلاحية، تغيير إعداد ضريبي، تسجيل دخول/فشله.

---

## 8) أمان تعدّد المستأجرين (Multi-Tenant Security) — الأعمق

هذا القسم يعالج المخاطر الفريدة لنموذج **Single Database, Shared Schema**.

### 8.1 منع تسرّب TenantId

- **مصدر واحد للحقيقة**: `TenantId` يُشتَق حصراً من Claim موقّع داخل JWT. يُوضَع في `ITenantProvider` مرّة لكل طلب.
- **حقن تلقائي عند الكتابة**: `SaveChangesInterceptor` يضبط `TenantId` على كل كيان جديد من `ITenantProvider` — لا يعتمد على قيمة العميل.
  ```csharp
  foreach (var entry in ChangeTracker.Entries<ITenantEntity>().Where(e => e.State == EntityState.Added))
      entry.Entity.TenantId = _tenantProvider.CurrentTenantId;
  ```
- **منع التزوير**: أي محاولة لإرسال `tenantId` في الطلب تُتجاهَل تماماً (لا تُربَط أصلاً في الـ DTO).

### 8.2 منع IDOR بين المستأجرين

- Global Query Filter يعني أن استعلام مستأجر A عن سجلّ يخصّ B يُرجع "غير موجود" (`404 RES_NOT_FOUND`) لا `403` — فلا يكشف حتى وجود السجلّ.
- المعرّفات الخارجية `PublicId (GUID)` غير قابلة للتخمين التسلسلي.

### 8.3 Row-Level Security (RLS) — دفاع في العمق على مستوى DB

طبقة ثانية **مستقلّة عن الكود**: حتى لو تسرّب استعلام بلا فلتر (خطأ برمجي، raw SQL)، SQL Server نفسه يمنع رؤية صفوف مستأجر آخر.

```sql
-- 1) الدالة المسنِدة (Predicate)
CREATE FUNCTION dbo.fn_TenantAccessPredicate(@TenantId BIGINT)
RETURNS TABLE WITH SCHEMABINDING
AS
    RETURN SELECT 1 AS ok
    WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS BIGINT);
GO

-- 2) سياسة الأمان على جدول أعمال
CREATE SECURITY POLICY dbo.TenantIsolationPolicy
    ADD FILTER PREDICATE dbo.fn_TenantAccessPredicate(TenantId) ON dbo.SalesInvoices,
    ADD BLOCK  PREDICATE dbo.fn_TenantAccessPredicate(TenantId) ON dbo.SalesInvoices AFTER INSERT
    WITH (STATE = ON);
GO
```

- التطبيق يضبط `SESSION_CONTEXT('TenantId')` عند فتح كل اتّصال (عبر EF Core interceptor على `DbConnection`):
  ```csharp
  // بعد فتح الاتصال، قبل أي استعلام
  await using var cmd = connection.CreateCommand();
  cmd.CommandText = "EXEC sp_set_session_context @key=N'TenantId', @value=@t";
  cmd.Parameters.Add(new SqlParameter("@t", _tenantProvider.CurrentTenantId));
  await cmd.ExecuteNonQueryAsync();
  ```
- **FILTER PREDICATE** يخفي صفوف الغير عند القراءة؛ **BLOCK PREDICATE** يمنع كتابة صفّ بـ TenantId مغاير — حماية مزدوجة.
- **تحذير Connection Pooling**: يجب إعادة ضبط `SESSION_CONTEXT` عند كل استعارة اتّصال من المجمّع، لأن الاتّصالات تُعاد استخدامها عبر مستأجرين مختلفين.

### 8.4 عزل الموارد المشتركة

- Redis/الكاش: كل مفتاح مسبوق بـ `tenant:{id}:` لمنع خلط الكاش بين المستأجرين.
- الملفات/الصور: مسارات معزولة `tenants/{tenantId}/...` مع تحقّق ملكية قبل التقديم.
- الوظائف الخلفية (Hangfire): كل مهمّة تحمل `TenantId` وتفتح نطاق المستأجر الصحيح قبل التنفيذ (انظر [29-Performance.md](29-Performance.md)).

---

## 9) منع تصعيد الصلاحيات (Privilege Escalation)

- الصلاحيات تُقرأ من الرمز الموقّع فقط — لا يمكن للمستخدم رفع صلاحياته بتعديل الطلب.
- منع **Horizontal escalation** (وصول لبيانات مستخدم آخر في نفس المستأجر) عبر فحص الملكية + الصلاحية.
- منع **Vertical escalation** عبر Policy checks على كل عملية إدارية، ومنع مستخدم من منح نفسه دوراً أعلى (فحص على مستوى الخدمة).
- **SuperAdmin عبر المستأجرين** (فريق المنصّة) معزول بحساب/مسار منفصل مع تدقيق مشدّد.

---

## 10) قائمة تدقيق الأمان (Security Checklist)

- [ ] كل جدول أعمال محميّ بـ Global Query Filter **و** RLS على `TenantId`.
- [ ] `TenantId` من الرمز حصراً، محقون تلقائياً عند الكتابة، ومُتجاهَل من مدخلات العميل.
- [ ] الـ URL يستخدم `PublicId (GUID)` — لا `Id` متسلسل (منع IDOR).
- [ ] لا concatenation في SQL؛ EF/parameters فقط.
- [ ] TLS إلزامي + HSTS + رؤوس الأمان + CSP.
- [ ] كلمات المرور مجزّأة، البيانات الحسّاسة مشفّرة (TDE/Always Encrypted).
- [ ] قفل الحساب + Rate Limiting على الدخول ضدّ Brute Force.
- [ ] Antiforgery على واجهة MVC؛ API يعتمد JWT في الرأس (لا CSRF).
- [ ] AuditLogs (Append-only) لكل عملية مالية حسّاسة.
- [ ] `SESSION_CONTEXT('TenantId')` يُعاد ضبطه عند كل استعارة اتّصال (Pooling).
- [ ] فحص الحزم (CVE) في CI، والأسرار في Key Vault مع تدوير.
- [ ] الأخطاء لا تكشف تفاصيل داخلية؛ Swagger محميّ في الإنتاج.

---

_الأمان مسؤولية كل طبقة. أي انحراف عن هذا المرجع يجب أن يمرّ بمراجعة أمنية موثّقة._
