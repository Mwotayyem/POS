# SmartApp — منصّة إدارة أعمال (ASP.NET Core)

> **Solution فعلية** مبنية على **Clean Architecture** و **.NET 9**، متعدّدة المستأجرين (Multi-Tenant) بعزل تامّ للبيانات وإدارة تفعيل يدوية للعملاء — **بلا اشتراكات ولا فوترة ولا مدفوعات**.

**الحالة:** ✅ **Phase 1 — Foundation** مكتملة. هيكل نظيف يعمل Build ويُقلِع، **بلا منطق أعمال بعد**.

---

## ما أُنجز في هذه المرحلة (Phase 1)

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
│   ├── SmartApp.Infrastructure   ← الخدمات التقنية (JWT, TenantProvider, ...) — لاحقاً
│   ├── SmartApp.Persistence      ← الوصول للبيانات (EF Core + SQL Server) — لاحقاً
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
| ORM (لاحقاً) | EF Core 9 + SQL Server |
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

## المرحلة التالية (Next: Phase 2)

**Tenancy + العزل (Multi-Tenant Core):** كيان `Tenant` + `AppDbContext` + Global Query Filter + `TenantResolutionMiddleware` + **اختبارات العزل الإلزامية**. التفاصيل في [14-Implementation-Roadmap.md](../SmartApp-Architecture/14-Implementation-Roadmap.md).

---

## التوثيق المعماري (Architecture Docs)

كل القرارات والتفاصيل في مجلد [SmartApp-Architecture](../SmartApp-Architecture/) — **المرجع الحاكم** لكل الكود. لا يُكتب كود يخالف التوثيق المعتمد.

---

_SmartApp · Phase 1 (Foundation) · بُني على .NET 9 · Clean Architecture._
