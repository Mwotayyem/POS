# 02 — Solution Architecture (معمارية الحلّ)

> يشرح هذا الملف **Clean Architecture** المطبّقة على SmartApp: المشاريع الستّة، اتجاه الاعتماد (Dependency Direction)، مسؤولية كل طبقة، تدفّق الطلب الكامل، ونمط CQRS.

---

## 1) لماذا Clean Architecture

الهدف: **عزل منطق الأعمال (Domain)** عن التفاصيل التقنية (قاعدة البيانات، الويب، المكتبات الخارجية) بحيث:

- منطق الأعمال قابل للاختبار دون قاعدة بيانات أو خادم.
- تبديل التقنية (مثلاً EF Core → Dapper، أو SQL Server → Postgres) لا يمسّ الـ Domain.
- الاعتماد يتّجه **للداخل** دائماً: الطبقات الخارجية تعتمد على الداخلية، لا العكس.

**القاعدة الذهبية:** `Domain` لا يعتمد على أي مشروع. كل شيء يعتمد عليه، وهو لا يعتمد على شيء.

---

## 2) المشاريع الستّة (The Six Projects)

```
SmartApp.sln
│
├── src/
│   ├── SmartApp.Domain          ← قلب النظام (Entities, Value Objects, Domain Rules)
│   ├── SmartApp.Application      ← حالات الاستخدام (CQRS, Handlers, Validators, Interfaces)
│   ├── SmartApp.Infrastructure   ← الخدمات الخارجية (JWT, Email, TenantProvider, Time)
│   ├── SmartApp.Persistence      ← الوصول للبيانات (DbContext, Repositories, Migrations, Configs)
│   ├── SmartApp.Shared           ← عناصر مشتركة عبر الطبقات (Constants, Enums, Result, DTOs base)
│   └── SmartApp.API              ← نقطة الدخول (Controllers, Middleware, DI, Swagger)
│
└── tests/
    ├── SmartApp.Domain.Tests
    ├── SmartApp.Application.Tests
    └── SmartApp.IntegrationTests
```

### مسؤولية كل مشروع

| المشروع | المسؤولية | يحتوي | لا يحتوي |
|---------|-----------|-------|----------|
| **`SmartApp.Domain`** | قلب منطق الأعمال | Entities, Value Objects, Enums الجوهرية, Domain Exceptions, Domain Interfaces (مثل `ITenantOwned`), Domain Events | لا EF، لا MediatR، لا مراجع خارجية |
| **`SmartApp.Application`** | حالات الاستخدام والتنسيق | Commands/Queries + Handlers (MediatR), Validators (FluentValidation), Mapping Profiles (AutoMapper), واجهات (`IApplicationDbContext`, `ITenantProvider`, `IJwtService`, `IUnitOfWork`), Pipeline Behaviors, DTOs | لا تنفيذ فعلي للبنية التحتية |
| **`SmartApp.Infrastructure`** | تنفيذ الخدمات التقنية غير-قاعدة-البيانات | `JwtService`, `TenantProvider`, `CurrentUserService`, `DateTimeProvider`, `PasswordHasher` wrappers, Email/Notification senders, Caching | لا Controllers |
| **`SmartApp.Persistence`** | الوصول للبيانات | `AppDbContext`, EF `IEntityTypeConfiguration<T>`, Global Query Filters, Repositories, `UnitOfWork`, Migrations, Seed | لا منطق أعمال |
| **`SmartApp.Shared`** | عناصر مشتركة محايدة | Constants, Permission keys, Common Enums, `Result<T>`, `PagedResult<T>`, Error codes, Guard helpers | لا اعتماد على أي طبقة أخرى |
| **`SmartApp.API`** | الواجهة (Presentation) | Controllers, Middleware (Tenant/Exception/Logging), DI Composition Root, Swagger, `Program.cs`, `appsettings` | لا منطق أعمال (Thin Controllers) |

---

## 3) اتجاه الاعتماد (Dependency Graph)

```
                       ┌─────────────────────┐
                       │   SmartApp.API      │  (Presentation / Composition Root)
                       └──────────┬──────────┘
                                  │ يعتمد على
              ┌───────────────────┼───────────────────┐
              ▼                   ▼                   ▼
   ┌────────────────────┐ ┌──────────────┐ ┌────────────────────┐
   │ SmartApp.          │ │ SmartApp.    │ │ SmartApp.          │
   │ Infrastructure     │ │ Persistence  │ │ Application        │
   └─────────┬──────────┘ └──────┬───────┘ └─────────┬──────────┘
             │ يعتمدان على        │                   │ يعتمد على
             └──────────┬─────────┘                   │
                        ▼                             ▼
              ┌──────────────────┐          ┌──────────────────┐
              │ SmartApp.        │◄─────────│ SmartApp.        │
              │ Application      │          │ Domain           │
              └────────┬─────────┘          └────────┬─────────┘
                       │ يعتمد على                    │
                       └──────────────┬───────────────┘
                                      ▼
                            ┌──────────────────┐
                            │ SmartApp.Shared  │  (لا يعتمد على شيء)
                            └──────────────────┘
```

### قواعد الاعتماد الصارمة

| المشروع | يعتمد على |
|---------|-----------|
| `Domain` | `Shared` فقط |
| `Application` | `Domain`, `Shared` |
| `Infrastructure` | `Application`, `Domain`, `Shared` |
| `Persistence` | `Application`, `Domain`, `Shared` |
| `Shared` | **لا شيء** |
| `API` | كل ما سبق (Composition Root فقط — يربط التنفيذ بالواجهات) |

> **الحرجة:** `API` هو الوحيد الذي يعرف `Infrastructure` و`Persistence` معاً — يربطهما بالواجهات المعرّفة في `Application` عبر DI. أي طبقة أخرى تعتمد على **الواجهات (Abstractions) في `Application`** لا على التنفيذ. هذا هو **Dependency Inversion** عملياً.

---

## 4) نمط CQRS (عبر MediatR)

كل عملية = **Command** (تُغيّر حالة) أو **Query** (تقرأ فقط)، ولكلٍّ **Handler** واحد.

```
Controller
   │  يرسل Command/Query عبر IMediator.Send(...)
   ▼
MediatR Pipeline Behaviors (بالترتيب):
   ① LoggingBehavior        ← تسجيل + correlation id
   ② ValidationBehavior     ← FluentValidation (يرفض قبل الوصول للـ Handler)
   ③ TenantGuardBehavior    ← تأكيد وجود سياق مستأجر صالح
   ④ TransactionBehavior    ← يغلّف الـ Command في Unit of Work (للأوامر فقط)
   │
   ▼
Handler (منطق حالة الاستخدام)
   │  يستدعي Repositories / IApplicationDbContext
   ▼
Persistence (EF Core + Global Query Filter + TenantId stamping)
   │
   ▼
النتيجة → DTO → Result<T> → Response Envelope
```

### أمثلة على الفصل

| العملية | النوع | المثال |
|---------|-------|--------|
| إنشاء منتج | Command | `CreateProductCommand` → `CreateProductCommandHandler` |
| تعديل عميل | Command | `UpdateCustomerCommand` |
| قائمة المنتجات مصفّاة | Query | `GetProductsQuery` → `PagedResult<ProductDto>` |
| تقرير مبيعات | Query | `GetSalesReportQuery` |

> **قاعدة:** لا يُعيد الـ Handler كياناً (`Entity`) أبداً — يُعيد **DTO**. التحويل عبر AutoMapper. منع تسرّب الكيانات للواجهة.

---

## 5) تدفّق الطلب الكامل (End-to-End Request Flow)

مثال: `GET /api/v1/products?search=milk` من مستخدم مسجَّل دخوله لمستأجر مُفعَّل.

```
┌──────────────────────────────────────────────────────────────────────┐
│  Client → GET /api/v1/products?search=milk  (Authorization: Bearer …) │
└─────────────────────────────────┬────────────────────────────────────┘
                                  │ (1) الطلب يصل API
                                  ▼
   ┌──────────────────────────────────────────────────────────┐
   │  Middleware Pipeline (بالترتيب):                          │
   │  ① ExceptionHandlingMiddleware  (يلتقط أي خطأ → Problem)  │
   │  ② Serilog RequestLogging       (correlation id)         │
   │  ③ Authentication (JWT)         (يتحقّق التوقيع + الصلاحية) │
   │  ④ TenantResolutionMiddleware   (tenant_id من الـ claim)  │
   │      └─ Tenant.Status = Active? ── لا → 403 مبكراً        │
   │  ⑤ Authorization (Permission: products.view)             │
   └─────────────────────────────────┬────────────────────────┘
                                     │ (2) TenantContext = {TenantId=1001}
                                     ▼
   ┌──────────────────────────────────────────────────────────┐
   │  ProductsController (Thin) → IMediator.Send(GetProductsQuery)│
   └─────────────────────────────────┬────────────────────────┘
                                     │ (3)
                                     ▼
   ┌──────────────────────────────────────────────────────────┐
   │  MediatR Pipeline: Logging → Validation → TenantGuard     │
   └─────────────────────────────────┬────────────────────────┘
                                     │ (4)
                                     ▼
   ┌──────────────────────────────────────────────────────────┐
   │  GetProductsQueryHandler                                  │
   │  _db.Products.Where(p => p.Name.Contains("milk"))         │
   └─────────────────────────────────┬────────────────────────┘
                                     │ (5) EF يضيف الفلتر العالمي تلقائياً
                                     ▼
   ┌──────────────────────────────────────────────────────────┐
   │  EF Core Global Query Filter                             │
   │  ... WHERE TenantId = 1001 AND IsDeleted = 0             │
   └─────────────────────────────────┬────────────────────────┘
                                     │ (6) SQL Server → صفوف المستأجر 1001 فقط
                                     ▼
   ┌──────────────────────────────────────────────────────────┐
   │  AutoMapper: Product → ProductDto                        │
   │  → PagedResult<ProductDto> → Result<T> → Envelope        │
   └─────────────────────────────────┬────────────────────────┘
                                     │ (7)
                                     ▼
              200 OK { success: true, data: {...}, ... }
```

---

## 6) الحدود المعمارية والوحدات (Modular Boundaries)

النواة مقسّمة إلى **وحدات (Modules)** داخل نفس المشاريع، بحدود منطقية واضحة:

```
Application/
├── Common/            (سلوكيات، واجهات، mapping مشترك)
├── Tenancy/           (وحدة إدارة المستأجرين)
├── Identity/          (المصادقة والصلاحيات)
├── Catalog/           (المنتجات والتصنيفات)
├── Inventory/         (المخزون والحركات)
├── Sales/             (المبيعات والمرتجعات)
├── Purchases/         (المشتريات والمرتجعات)
├── Partners/          (العملاء والموردون)
├── Reporting/         (التقارير)
├── Audit/             (سجل التدقيق)
└── Settings/          (الإعدادات)
```

> **قابلية التوسّع:** POS وبقية الامتدادات تُضاف كوحدات جديدة (`Application/Pos/`, `Domain/Pos/`) **تبني فوق النواة ولا تعدّلها**. التفصيل الكامل لحدود الوحدات في [04-Domain-Boundaries.md](04-Domain-Boundaries.md).

---

## 7) خلاصة القرارات المعمارية

| القرار | الاختيار | لماذا |
|--------|----------|-------|
| نمط المعمارية | Clean Architecture | عزل الـ Domain، قابلية اختبار وصيانة |
| عدد المشاريع | 6 (فصل Persistence وShared) | فصل الوصول للبيانات عن باقي البنية التحتية + عناصر محايدة مشتركة |
| نمط التطبيق | CQRS (MediatR) | فصل القراءة عن الكتابة، Handlers صغيرة قابلة للاختبار |
| الوصول للبيانات | Repository + Unit of Work فوق EF Core | تجريد + معاملات ذرّية |
| اتجاه الاعتماد | للداخل نحو Domain | Dependency Inversion |
| الواجهة | Thin Controllers | كل المنطق في Handlers |

---

_يُكمّله [03-Project-Structure.md](03-Project-Structure.md) (بنية المجلدات) و[04-Domain-Boundaries.md](04-Domain-Boundaries.md) (حدود الوحدات) و[13-Development-Rules.md](13-Development-Rules.md) (قواعد الكود)._
