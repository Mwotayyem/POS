# SmartApp — منصّة إدارة أعمال (ASP.NET Core)

> **Solution فعلية** مبنية على **Clean Architecture** و **.NET 9**، متعدّدة المستأجرين (Multi-Tenant) بعزل تامّ للبيانات وإدارة تفعيل يدوية للعملاء — **بلا اشتراكات ولا فوترة ولا مدفوعات**.

**الحالة:** ✅ Phase 1–6 مكتملة. Foundation + Tenant Core + Identity & RBAC + API Foundation + Authentication + **Administration (Users/Roles/Permissions/Settings/Profile)**. **36/36 اختبار تمرّ** · إدارة كاملة عبر REST.

---

## ما أُنجز في Phase 6 (Administration Foundation)

طبقة إدارة كاملة عبر REST، بنفس القوالب (CQRS · Clean Architecture · Tenant Isolation · Response Envelope · Validation · Authorization Policies):

- ✅ **Users Management** — `GET /api/v1/users` (بحث) · `GET /users/{id}` · `POST /users` (إنشاء + إسناد أدوار) · `PUT /users/{id}` (تعديل + مطابقة الأدوار) · `POST /users/{id}/activate` · `POST /users/{id}/deactivate` (يُبطِل refresh tokens النشطة).
- ✅ **Roles Management (RBAC)** — CRUD كامل (`GET`/`POST`/`PUT`/`DELETE /api/v1/roles`) + `PUT /roles/{id}/permissions` (إسناد الصلاحيات) · حماية الأدوار النظامية (Owner) من التعديل/الحذف · منع حذف دور مُسنَد لمستخدمين.
- ✅ **Permissions Management** — `GET /api/v1/permissions` (قراءة فقط — الكتالوج ثابت مُعرَّف بالنظام؛ الإسناد يتمّ عبر الأدوار).
- ✅ **Tenant Settings** — `GET /api/v1/tenant/settings` (يُرجع الافتراضيات إن لم تُضبَط) · `PUT` (upsert: عملة/منطقة زمنية/ضريبة/locale/theme JSON مُتحقَّق منه).
- ✅ **Current User Profile** — `GET /api/v1/profile` (الهوية + الأدوار + الصلاحيات الفعلية) · `PUT /profile` (تعديل ذاتي) · `POST /profile/change-password` (تحقّق كلمة المرور الحالية + إبطال الجلسات). **مصادقة فقط، بلا صلاحية محدّدة.**
- ✅ **Seeding** — `PermissionSeeder` (يزرع الكتالوج عند الإقلاع، idempotent) + `TenantRoleSeeder` (دور Owner بكل الصلاحيات لكل مستأجر). أُضيفت صلاحيات جديدة: `roles.create/update/delete/permissions.manage` · `settings.view/manage`.
- ✅ **Authorization** — كل endpoint محميّ بـ `[HasPermission("resource.action")]`؛ عزل المستأجر يضمن أن كيانات مستأجر آخر غير مرئية (NOT_FOUND).
- ✅ **اختبارات (23 جديدة، 36/36 إجمالاً):** happy-path لكل مجموعة · عزل المستأجرين (users من مستأجر آخر → 404) · authorization (بلا صلاحية → 403 · بلا مصادقة → 401) · validation (بريد/كلمة مرور/عملة/صلاحية غير صالحة → 400) · تدوير كلمة المرور تعمل بالجديدة · حماية الدور النظامي.

> **لم يُنشأ بعد:** Products/Sales/Inventory · Business modules · Frontend.

---

## ما أُنجز في Phase 5 (API Foundation + Authentication)

- ✅ **Response Envelope موحّد** — `ApiResponse<T>` (success/data/error/meta) + `Result<T>` + `Error` + `ErrorStatusMapper`.
- ✅ **Authentication endpoints:**
  - `POST /api/v1/auth/login` — email+password · تحقّق المستأجر (Active) · تحقّق المستخدم · JWT بصلاحيات + refresh token (hash مخزَّن).
  - `POST /api/v1/auth/refresh` — تدوير التوكن (rotation) + إبطال القديم + **reuse detection**.
  - `POST /api/v1/auth/logout` — إبطال refresh token.
- ✅ **CQRS handlers** (MediatR) + FluentValidation عبر `ValidationBehavior`.
- ✅ **Permission-based Authorization** — `[HasPermission("resource.action")]` + `PermissionPolicyProvider` (ديناميكي) + `PermissionAuthorizationHandler`.
- ✅ **JWT Authentication** — Bearer + التحقّق (issuer/audience/lifetime/key) · binding كسول من `JwtSettings`.
- ✅ **Global Exception Handling** — يحوّل الأخطاء إلى الـ envelope الموحّد (ProblemDetails-style).
- ✅ **Swagger** — زرّ Authorize (JWT Bearer) + الـ endpoints ظاهرة.
- ✅ **اختبارات (6 جديدة، 13/13 إجمالاً):** login ناجح · كلمة مرور خاطئة (401) · مستأجر معطّل (403 TENANT_INACTIVE) · تدوير refresh + إبطال القديم · logout يُبطِل · validation بالـ envelope.

> **لم يُنشأ بعد:** Products/Sales/Inventory · Business modules · Register endpoint.

---

## ما أُنجز في Phase 4 (Identity & RBAC Foundation)

- ✅ **Identity Entities** — `AppUser` (TenantId nullable للـ system owner) · `AppRole` · `Permission` (مرجعي عالمي، int key) · `UserRole` · `RolePermission` (junction) · `RefreshToken` (hash + rotation + reuse detection).
- ✅ **EF Configurations** — علاقات · فهارس · قيود فريدة tenant-scoped (`UX_Users_Tenant_Email` · `UX_Roles_Tenant_Name` · `UX_Permissions_Code`) · فلاتر عزل صريحة للكيانات nullable-tenant.
- ✅ **Shared Constants** — `Permissions` (resource.action) + `RoleNames`.
- ✅ **Authentication Foundation** — `IPasswordHasher` + `PasswordHasher` (PBKDF2) · `IJwtService` + `JwtService` (JWT بصلاحيات + refresh token hashing) · `JwtSettings` binding.
- ✅ **Migration `AddIdentity`** — 6 جداول Identity بكل الفهارس والقيود. **بلا model drift**.
- ✅ **اختبارات العزل (3 جديدة، 7/7 إجمالاً)**: عزل المستخدمين · عزل الأدوار والصلاحيات (مع تأكيد أن `Permissions` مرجعي مشترك) · ملكية RefreshToken لمستأجره فقط.

> **لم يُنشأ بعد:** Login/Register endpoints · Controllers · Authorization policies كاملة.

---

## ما أُنجز في Phase 3 (Tenant Core)

- ✅ **Tenant Entity** — `Tenant` (يرث `AuditableEntity` + `ISoftDeletable`، **بلا `TenantId`** لأنه تعريف المستأجر نفسه) + `TenantSetting` (tenant-owned، يرث `BaseEntity`).
- ✅ **EF Configurations** — `TenantConfiguration` + `TenantSettingConfiguration` (Fluent · Indexes · Unique constraints · ROWVERSION · Check constraints).
- ✅ **أول Migration حقيقية** — `InitialCreate` (جدولا `Tenants` + `TenantSettings` مع كل الفهارس والقيود). **بلا model drift**.
- ✅ **TenantResolutionMiddleware** — يقرأ المستأجر من سياق الطلب + بوّابة التفعيل اليدوي (Active/Suspended/Disabled → 403).
- ✅ **اختبارات العزل (4/4 تمرّ)** على SQLite حقيقي:
  - `TenantA_Cannot_Read_TenantB_Data` — مستأجر لا يرى بيانات آخر.
  - `Global_Query_Filter_Scopes_Reads_To_Current_Tenant` — الفلتر العالمي يعمل.
  - `TenantId_Is_Stamped_Server_Side_On_Insert` — الختم التلقائي.
  - `Delete_Is_Soft_And_Excluded_By_Filter` — Soft Delete يعمل.

> **لم يُنشأ بعد:** باقي Entities (Products/Sales/Inventory...) · Controllers · Business logic · Authentication.

---

## ما أُنجز في Phase 2 (Domain + Persistence Foundation)

- ✅ **Domain Core:** `Entity` · `AuditableEntity` · `BaseEntity` (Tenant + Audit + Soft Delete + ConcurrencyStamp) + الواجهات (`ITenantOwned`, `IAuditable`, `ISoftDeletable`, `IAppendOnly`).
- ✅ **Common enums:** `TenantStatus` (Active/Suspended/Disabled — بديل SaaS).
- ✅ **Domain Exceptions:** `DomainException` (base) · `BusinessRuleViolationException` · `TenantMismatchException`.
- ✅ **Application Interfaces:** `ITenantProvider` · `ICurrentUserService` · `IDateTimeProvider` · `IApplicationDbContext`.
- ✅ **Persistence:** `AppDbContext` مع **EF Core Global Query Filter** (عزل المستأجر + Soft Delete تلقائياً لكل `BaseEntity`) + **ختم `TenantId`** خادم-جانبياً + `AuditableEntityInterceptor` (audit + تحويل الحذف لـ soft delete).
- ✅ **Infrastructure:** `TenantProvider` · `CurrentUserService` · `DateTimeProvider` (يقرؤون من `HttpContext`/Claims).
- ✅ **Migration-ready:** `AppDbContextFactory` (design-time) + connection config — تمّ **التحقّق فعلياً** بتوليد migration ناجحة ثم إزالتها (حسب قيد "لا تنشئ migrations الآن").
- ✅ **Build نظيف: 0/0** · التطبيق يُقلِع و DI يحلّ كل الخدمات.

> **لم يُنشأ بعد:** Entities فعلية · DbSets · Controllers · Business logic · Authentication.

---

## ما أُنجز في Phase 1 (Foundation)

هذه المرحلة تُنشئ **الهيكل والبنية التحتية فقط**، حسب [Implementation Roadmap](../SmartApp-Architecture/14-Implementation-Roadmap.md):

- ✅ إنشاء `SmartApp.sln` والمشاريع الستّة + مشاريع الاختبار.
- ✅ إعداد Project References حسب Clean Architecture (الاعتماد يتّجه نحو الـ Domain).
- ✅ الهيكل الداخلي للمجلّدات لكل مشروع.
- ✅ ملفات إعداد موحّدة (`Directory.Build.props`, `.editorconfig`, `global.json`).
- ✅ بنية Dependency Injection لكل طبقة (`AddApplication` / `AddInfrastructure` / `AddPersistence` / `AddApiServices`).
- ✅ `Program.cs` + Middleware pipeline + تحميل الإعدادات.
- ✅ بنية معالجة الأخطاء الموحّدة (Global Exception Handling → Envelope موحّد).
- ✅ بنية API Versioning (`/api/v1/...`) + إعداد Swagger (مع JWT Bearer).
- ✅ **Build نظيف: 0 تحذير · 0 خطأ** · الخادم يُقلِع و Swagger يعمل (200).

> **لم يُنشأ بعد (مؤجَّل لمراحل لاحقة):** Entities · DbContext · Migrations · Controllers · Business Logic · Authentication implementation.

---

## بنية المشاريع (Clean Architecture — 6 مشاريع)

```
SmartApp.sln
├── src/
│   ├── SmartApp.Domain          ← قلب النظام (لا يعتمد إلا على Shared)
│   ├── SmartApp.Application      ← حالات الاستخدام (CQRS/MediatR + FluentValidation + الواجهات)
│   ├── SmartApp.Infrastructure   ← الخدمات التقنية (JWT, PasswordHasher, TenantProvider)
│   ├── SmartApp.Persistence      ← الوصول للبيانات (EF Core + SQL Server + Seeding)
│   ├── SmartApp.Shared           ← عناصر محايدة مشتركة (Result, Constants, ...)
│   └── SmartApp.API              ← نقطة الدخول (Composition Root + Middleware + Swagger)
│
└── tests/
    ├── SmartApp.Domain.Tests
    ├── SmartApp.Application.Tests
    └── SmartApp.IntegrationTests
```

### اتجاه الاعتماد (Dependency Direction)

```
API → Application → Domain → Shared
Infrastructure → Application + Domain + Shared
Persistence   → Application + Domain + Shared
API (Composition Root) → كل المشاريع
```

- **`Domain`** لا يعتمد على أي مشروع سوى `Shared` (ولا حزم خارجية).
- **`Application`** يعرف الواجهات فقط (Interfaces)، لا تنفيذ البنية التحتية.
- **`API`** وحده يربط التنفيذ بالواجهات عبر DI (Dependency Inversion).

---

## التقنيات (Technology Stack)

| المجال | التقنية |
|--------|---------|
| Runtime | .NET 9 (C# 13, Nullable enabled, Implicit usings) |
| API | ASP.NET Core 9 Web API |
| CQRS | MediatR 12.4.1 |
| Validation | FluentValidation 11.11 |
| ORM | EF Core 9 + SQL Server (SQLite in-memory للاختبارات) |
| Auth | JWT Bearer + Permission-based Authorization (`resource.action`) |
| Passwords | PBKDF2 (ASP.NET Core `PasswordHasher`) |
| Versioning | Asp.Versioning (URL segment: `/api/v1/`) |
| Docs | Swagger / Swashbuckle 7.2 (JWT Bearer) |

> الجودة: `TreatWarningsAsErrors = true` + `AnalysisLevel = latest-recommended` — لا تحذيرات مُقدَّمة، ولا حزم بها ثغرات معروفة.

---

## التشغيل (Getting Started)

**المتطلّبات:** .NET 9 SDK · SQL Server (يُستخدم لاحقاً في Phase 2) · Visual Studio 2022 أو VS Code.

```bash
# بناء الحلّ كاملاً
dotnet build SmartApp.sln

# تشغيل الـ API
dotnet run --project src/SmartApp.API

# ثم افتح Swagger:
#   http://localhost:<port>/swagger
```

> فتح المشروع في Visual Studio: افتح `SmartApp.sln` مباشرةً.

---

## ملاحظات مهمّة (Notes)

- **إدارة العملاء يدوية بالكامل** — لا نظام اشتراك/فوترة/دفع. حالة المستأجر (`Active`/`Suspended`/`Disabled`) يتحكّم بها مالك النظام (يُنفَّذ في Phase 2).
- **العزل قرار معماري أساسي** — `TenantId` هو حدّ العزل، عبر EF Core Global Query Filter (يُنفَّذ في Phase 2). لا `StoreId` مبدئياً.
- **الأسرار لا تُرفَع** — `ConnectionStrings:SmartAppDb` و`Jwt:SigningKey` فارغة في `appsettings.json`؛ تُملأ عبر User Secrets / متغيّرات البيئة.
- **AutoMapper مؤجَّل** — بسبب توافق النسخ/الترخيص، تُحسَم مكتبة الـ mapping في Phase 3 عند الحاجة الفعلية للتحويل.

---

## المرحلة التالية (Next: Business Modules)

**أول Business Module** (مثل Products/Catalog) فوق الأساس الجاهز: Domain entities + configs + migration + CQRS endpoints + tests — بنفس قوالب العزل والتحقّق والصلاحيات. التفاصيل في [14-Implementation-Roadmap.md](../SmartApp-Architecture/14-Implementation-Roadmap.md).

---

## التوثيق المعماري (Architecture Docs)

كل القرارات والتفاصيل في مجلد [SmartApp-Architecture](../SmartApp-Architecture/) — **المرجع الحاكم** لكل الكود. لا يُكتب كود يخالف التوثيق المعتمد.

---

_SmartApp · Phase 1–6 (Foundation → Administration) · بُني على .NET 9 · Clean Architecture._
