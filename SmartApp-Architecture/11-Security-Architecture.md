# 11 — Security Architecture (معمارية الأمان)

> يشرح كيف يتعامل SmartApp مع كل بند من **OWASP Top 10**، وحماية العزل، وإدارة الأسرار، والتصميم الجاهز لـ **RLS المستقبلي**. يلتزم بـ [09-Multi-Tenant.md](09-Multi-Tenant.md) و[10-Identity-RBAC.md](10-Identity-RBAC.md).

**المبدأ الحاكم:** كل مُدخَل مُعادٍ حتى يُثبَت العكس. أولوية الأمان: `Security > Data Integrity > Correctness > ...`.

---

## 1) OWASP Top 10 — كل بند بحلّ خاصّ

| # | الخطر | كيف يعالجه SmartApp |
|---|-------|---------------------|
| **A01** | **Broken Access Control** | تفويض قائم على Permissions لكل endpoint · العزل التلقائي بـ `TenantId` · `PublicId` بدل `Id` المتسلسل لمنع IDOR · فحص ملكية المورد قبل التعديل |
| **A02** | **Cryptographic Failures** | كلمات المرور PBKDF2 · Refresh tokens مُخزَّنة hash فقط · JWT موقّع (HMAC-SHA256/RS256) · TLS إجباري · لا أسرار في الكود/اللوجات |
| **A03** | **Injection** | EF Core parameterized queries حصراً · منع `FromSqlRaw` بلا مراجعة · FluentValidation على كل مُدخَل · `ISJSON` على حقول JSON |
| **A04** | **Insecure Design** | Clean Architecture · Threat modeling للعزل · قواعد أعمال في الـ Domain لا الواجهة · Append-only للسجلّات المالية |
| **A05** | **Security Misconfiguration** | إعدادات لكل بيئة · Swagger معطّل في الإنتاج · Security headers · رسائل خطأ عامّة (ProblemDetails) لا تكشف تفاصيل |
| **A06** | **Vulnerable Components** | تثبيت نسخ الحزم · فحص `dotnet list package --vulnerable` في CI · تحديث دوري |
| **A07** | **Auth Failures** | قفل بعد محاولات فاشلة · Refresh rotation + reuse detection · عمر توكن قصير · سياسة كلمات مرور · فحص حالة المستأجر عند الدخول |
| **A08** | **Data Integrity Failures** | `ConcurrencyStamp (ROWVERSION)` Optimistic Concurrency · معاملات ذرّية · Append-only ledgers · توقيع JWT |
| **A09** | **Logging & Monitoring Failures** | Serilog مُنظَّم بـ correlation id · `AuditLogs` لكل تغيير حسّاس · تسجيل أحداث الأمان (فشل دخول، reuse) · لا أسرار في اللوجات |
| **A10** | **SSRF** | لا طلبات خارجية من مُدخَل مستخدم · قوائم بيضاء صارمة لأي تكامل مستقبلي |

---

## 2) حماية العزل (Isolation Security) — الحدّ الأمني الأهمّ

العزل بين المستأجرين **حدّ أمني (Security Boundary)**، لا مجرّد ميزة. تسرّب بيانات مستأجر = **Sev-1**.

```
طبقات الحماية الحالية:
  ① استخراج موثوق:  TenantId من JWT claim موقّع — لا من العميل أبداً
  ② فحص الحالة:     Tenant.Status = Active قبل أي منطق أعمال
  ③ فلتر تلقائي:    EF Core Global Query Filter على كل استعلام
  ④ ختم تلقائي:     TenantId يُختَم خادم-جانبياً عند الكتابة

طبقة مستقبلية جاهزة:
  ⑤ SQL RLS:        FILTER + BLOCK predicate على مستوى المحرّك (§6)
```

تفصيل مخاطر التسرّب والمنع في [09-Multi-Tenant.md §8](09-Multi-Tenant.md).

---

## 3) منع IDOR (Insecure Direct Object Reference)

```
❌ المشكلة:  GET /api/v1/invoices/1042  →  المستخدم يخمّن 1043 لمستأجر آخر
✅ الحماية:
    ① الفلتر العالمي: استعلام Id=1043 من مستأجر آخر يُرجِع لا شيء (مخفيّ)
    ② PublicId (GUID) في الـ API للكيانات الحسّاسة بدل Id المتسلسل
    ③ فحص ملكية صريح في الـ Handler قبل أي تعديل
```

الفلتر العالمي وحده يمنع IDOR عبر المستأجرين تلقائياً؛ `PublicId` طبقة إضافية ضدّ التخمين داخل نفس المستأجر.

---

## 4) الحماية على مستوى النقل والرؤوس (Transport & Headers)

| العنصر | الإعداد |
|--------|---------|
| **TLS** | HTTPS إجباري · HSTS · إعادة توجيه HTTP→HTTPS |
| **CORS** | قائمة أصول بيضاء صارمة (لا `*` في الإنتاج) |
| **Security Headers** | `X-Content-Type-Options: nosniff` · `X-Frame-Options: DENY` · `Content-Security-Policy` · `Referrer-Policy` |
| **Rate Limiting** | حدّ معدّل على `/auth/login` و`/auth/refresh` (منع brute-force وحشو التوكنات) |

---

## 5) إدارة الأسرار (Secrets Management)

```
❌ ممنوع:  سرّ في الكود · سرّ في appsettings.json مرفوع للمستودع · سرّ في اللوجات
✅ إلزامي:
    - التطوير:  User Secrets (dotnet user-secrets)
    - الإنتاج:  متغيّرات بيئة / Azure Key Vault / secret store
    - JWT signing key, connection string, SMTP creds → من الإعدادات دائماً
```

كل قيمة بيئية (connection string, signing key) تُقرأ من `IConfiguration` — لا تُضمَّن في الكود إطلاقاً.

---

## 6) RLS المستقبلي (Row-Level Security — تصميم جاهز، غير مُفعَّل)

القرار المعتمد: **الاعتماد على EF Core Global Query Filter الآن**، مع إبقاء التصميم جاهزاً لتفعيل RLS كطبقة تقوية (defense-in-depth) عند الحاجة (مثلاً لعملاء حسّاسين). **لا يُفعَّل في الإصدار الأول** حفاظاً على بساطة النشر والـ migrations.

### 6.1 التصميم الجاهز (عند التفعيل مستقبلاً)

```sql
-- (1) دالة تنبؤية: تُرجِع صفاً فقط إن طابق TenantId سياق الجلسة
CREATE FUNCTION dbo.fn_TenantPredicate(@TenantId BIGINT)
    RETURNS TABLE WITH SCHEMABINDING
AS
    RETURN SELECT 1 AS ok
           WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS BIGINT);
GO

-- (2) سياسة أمان: FILTER (قراءة) + BLOCK (كتابة) على كل جدول أعمال
CREATE SECURITY POLICY dbo.TenantSecurityPolicy
    ADD FILTER PREDICATE dbo.fn_TenantPredicate([TenantId]) ON dbo.Products,
    ADD BLOCK  PREDICATE dbo.fn_TenantPredicate([TenantId]) ON dbo.Products AFTER INSERT,
    ADD FILTER PREDICATE dbo.fn_TenantPredicate([TenantId]) ON dbo.SalesInvoices,
    ADD BLOCK  PREDICATE dbo.fn_TenantPredicate([TenantId]) ON dbo.SalesInvoices AFTER INSERT
    -- ... يُضاف كل جدول أعمال
    WITH (STATE = ON);
GO
```

### 6.2 ضبط سياق الجلسة (يتطلّبه RLS)

```csharp
// عند فتح الاتصال (DbConnection interceptor) — يُضاف عند تفعيل RLS
await using var cmd = connection.CreateCommand();
cmd.CommandText = "EXEC sys.sp_set_session_context @key=N'TenantId', @value=@tid;";
cmd.Parameters.AddWithValue("@tid", _tenant.CurrentTenantId);
await cmd.ExecuteNonQueryAsync();
```

- **FILTER PREDICATE:** يخفي صفوف المستأجرين الآخرين من كل `SELECT` على مستوى المحرّك.
- **BLOCK PREDICATE:** يمنع `INSERT`/`UPDATE` بقيمة `TenantId` مخالفة لسياق الجلسة.
- **الأثر:** حتى لو تسرّب استعلام خام بلا شرط `TenantId`، RLS يمنع الرؤية/الكتابة.

> الأعمدة (`TenantId` على كل جدول) والتصميم جاهزان بالفعل — تفعيل RLS لاحقاً هو إضافة migration للـ function/policy فقط، دون تغيير في الكيانات.

---

## 7) الأمان في الـ Pipeline (Defense at Each Layer)

```
┌─ Transport ─────────────────────────────────────┐
│  TLS · HSTS · Security Headers · Rate Limiting   │
├─ Authentication ────────────────────────────────┤
│  JWT signature · token expiry · lockout          │
├─ Tenant Resolution ─────────────────────────────┤
│  Status=Active check · TenantId من claim موقّع     │
├─ Authorization ─────────────────────────────────┤
│  Permission check لكل endpoint                    │
├─ Validation ────────────────────────────────────┤
│  FluentValidation قبل الوصول للـ Handler          │
├─ Data Access ───────────────────────────────────┤
│  Global Query Filter · TenantId stamping · params│
└─ Database (مستقبلاً) ────────────────────────────┘
   RLS FILTER + BLOCK predicate
```

كل طبقة تحمي بشكل مستقلّ — اختراق واحدة لا يكفي للتسرّب.

---

## 8) التدقيق كأداة أمان (Audit as Security)

- كل تغيير على بيانات حسّاسة → `AuditLogs` (append-only): من، ماذا، متى، من أي IP، correlation id.
- أحداث أمنية خاصّة تُسجَّل: فشل دخول متكرّر، reuse لتوكن، تغيير حالة مستأجر، تغيير صلاحيات.
- `AuditLogs` لا تُعدَّل ولا تُحذَف → دليل نزيه للتحقيق.

---

## 9) قائمة تحقّق الأمان (Security Checklist)

عند إضافة أي endpoint/ميزة:

- [ ] له صلاحية `[HasPermission(...)]` مناسبة.
- [ ] لا يقرأ `TenantId` من العميل.
- [ ] مُدخَلاته مُتحقَّق منها (FluentValidation).
- [ ] لا `FromSqlRaw`/`IgnoreQueryFilters` بلا مراجعة.
- [ ] الكيانات الحسّاسة تُكشَف بـ `PublicId` لا `Id`.
- [ ] العمليات الحسّاسة تُسجَّل في `AuditLogs`.
- [ ] لا سرّ في الكود/اللوج · رسائل الخطأ عامّة.
- [ ] اختبار عزل (قراءة + كتابة) يمرّ في CI.

---

## 10) خلاصة القرارات الأمنية

| القرار | الاختيار |
|--------|----------|
| المصادقة | JWT + Refresh rotation + reuse detection |
| التفويض | Permission-based (`resource.action`) |
| العزل | حدّ أمني · EF Filter + Status check الآن · RLS جاهز لاحقاً |
| منع IDOR | Global Filter + PublicId (GUID) |
| الأسرار | من الإعدادات/secret store — لا في الكود |
| كلمات المرور | PBKDF2 hash · سياسة · قفل |
| التدقيق | Append-only AuditLogs لكل حدث حسّاس |
| النقل | TLS · HSTS · Headers · CORS whitelist · Rate limit |

---

_يُكمّله [09-Multi-Tenant.md](09-Multi-Tenant.md) (تفاصيل العزل) و[10-Identity-RBAC.md](10-Identity-RBAC.md) (المصادقة والصلاحيات) و[13-Development-Rules.md](13-Development-Rules.md)._
