# 03 — Project Structure (بنية المجلدات المفصّلة)

> بنية المجلدات الكاملة لكل مشروع من المشاريع الستّة. هذه البنية **إلزامية** — أي كود مُولَّد يجب أن يوضَع في مكانه الصحيح. الأسماء بالإنجليزية (قياسية).

---

## 1) بنية الـ Solution العليا

```
SmartApp/
├── SmartApp.sln
├── Directory.Build.props          ← إعدادات موحّدة (نسخة .NET، Nullable, LangVersion)
├── .editorconfig                  ← معايير التنسيق
├── global.json                    ← تثبيت نسخة SDK
│
├── src/
│   ├── SmartApp.Domain/
│   ├── SmartApp.Application/
│   ├── SmartApp.Infrastructure/
│   ├── SmartApp.Persistence/
│   ├── SmartApp.Shared/
│   └── SmartApp.API/
│
├── tests/
│   ├── SmartApp.Domain.Tests/
│   ├── SmartApp.Application.Tests/
│   └── SmartApp.IntegrationTests/
│
└── docs/                          ← هذا التوثيق (SmartApp-Architecture)
```

---

## 2) `SmartApp.Domain` — قلب النظام

> لا يعتمد على أي مشروع سوى `SmartApp.Shared`. لا EF، لا MediatR.

```
SmartApp.Domain/
├── Common/
│   ├── BaseEntity.cs                 ← الأعمدة المشتركة (Id, TenantId, audit, soft delete)
│   ├── ITenantOwned.cs               ← علامة الكيانات المملوكة لمستأجر
│   ├── IAuditable.cs                 ← علامة الكيانات القابلة للتدقيق
│   ├── ISoftDeletable.cs
│   ├── IAppendOnly.cs                ← علامة الجداول التي لا تُعدَّل (StockMovements, AuditLogs)
│   ├── AggregateRoot.cs
│   └── DomainEvent.cs
│
├── Tenancy/
│   ├── Tenant.cs
│   └── Enums/
│       └── TenantStatus.cs           ← Active / Suspended / Disabled
│
├── Identity/
│   ├── AppUser.cs                    ← يرث IdentityUser<long>
│   ├── AppRole.cs                    ← يرث IdentityRole<long>
│   ├── Permission.cs
│   ├── RolePermission.cs
│   └── RefreshToken.cs
│
├── Catalog/
│   ├── Product.cs
│   ├── ProductBarcode.cs
│   ├── ProductUnit.cs
│   ├── Category.cs
│   └── Unit.cs
│
├── Inventory/
│   ├── Stock.cs
│   ├── StockMovement.cs              ← Append-Only
│   ├── StockAdjustment.cs
│   └── Enums/
│       └── StockMovementType.cs
│
├── Partners/
│   ├── Customer.cs
│   ├── Supplier.cs
│   ├── CustomerPayment.cs
│   └── SupplierPayment.cs
│
├── Sales/
│   ├── SalesInvoice.cs
│   ├── SalesInvoiceItem.cs
│   ├── SalesReturn.cs
│   ├── SalesReturnItem.cs
│   └── Enums/
│       └── InvoiceStatus.cs
│
├── Purchases/
│   ├── PurchaseInvoice.cs
│   ├── PurchaseInvoiceItem.cs
│   ├── PurchaseReturn.cs
│   └── PurchaseReturnItem.cs
│
├── Audit/
│   └── AuditLog.cs                   ← Append-Only
│
├── Settings/
│   ├── TenantSetting.cs
│   └── Sequence.cs                   ← ترقيم المستندات لكل مستأجر
│
├── Exceptions/
│   ├── DomainException.cs
│   ├── BusinessRuleViolationException.cs
│   └── TenantMismatchException.cs
│
└── ValueObjects/
    ├── Money.cs                      ← (Amount, Currency) — DECIMAL(18,4)
    └── Barcode.cs
```

---

## 3) `SmartApp.Application` — حالات الاستخدام

> يعتمد على `Domain` و`Shared`. هنا CQRS + Validators + الواجهات (Interfaces) دون تنفيذ.

```
SmartApp.Application/
├── Common/
│   ├── Behaviors/
│   │   ├── LoggingBehavior.cs
│   │   ├── ValidationBehavior.cs
│   │   ├── TenantGuardBehavior.cs
│   │   └── TransactionBehavior.cs
│   ├── Interfaces/
│   │   ├── IApplicationDbContext.cs
│   │   ├── ITenantProvider.cs
│   │   ├── ICurrentUserService.cs
│   │   ├── IJwtService.cs
│   │   ├── IDateTimeProvider.cs
│   │   ├── IUnitOfWork.cs
│   │   ├── IRepository.cs
│   │   └── IAuditService.cs
│   ├── Mappings/
│   │   └── MappingProfile.cs         ← AutoMapper base
│   ├── Models/
│   │   ├── Result.cs                 ← (قد يكون في Shared حسب الحاجة)
│   │   └── PagedResult.cs
│   └── Exceptions/
│       ├── ValidationException.cs
│       ├── NotFoundException.cs
│       └── ForbiddenException.cs
│
├── Tenancy/
│   ├── Commands/
│   │   ├── CreateTenant/            (Command + Handler + Validator)
│   │   ├── ActivateTenant/
│   │   ├── SuspendTenant/
│   │   └── DisableTenant/
│   ├── Queries/
│   │   ├── GetTenants/
│   │   └── GetTenantById/
│   └── Dtos/
│       └── TenantDto.cs
│
├── Identity/
│   ├── Commands/
│   │   ├── Login/
│   │   ├── RefreshToken/
│   │   ├── Logout/
│   │   ├── ChangePassword/
│   │   ├── CreateUser/
│   │   ├── AssignRole/
│   │   └── CreateRole/
│   ├── Queries/
│   │   ├── GetUsers/
│   │   ├── GetRoles/
│   │   └── GetPermissions/
│   └── Dtos/
│
├── Catalog/
│   ├── Commands/  (CreateProduct, UpdateProduct, DeleteProduct, CreateCategory, …)
│   ├── Queries/   (GetProducts, GetProductById, GetProductByBarcode, GetCategories)
│   └── Dtos/
│
├── Inventory/
│   ├── Commands/  (AdjustStock, …)
│   ├── Queries/   (GetStock, GetStockMovements)
│   └── Dtos/
│
├── Partners/
│   ├── Commands/  (CreateCustomer, CreateSupplier, RecordCustomerPayment, …)
│   ├── Queries/
│   └── Dtos/
│
├── Sales/
│   ├── Commands/  (CreateSalesInvoice, CreateSalesReturn, …)
│   ├── Queries/   (GetSalesInvoices, GetSalesInvoiceById)
│   └── Dtos/
│
├── Purchases/
│   ├── Commands/  (CreatePurchaseInvoice, CreatePurchaseReturn, …)
│   ├── Queries/
│   └── Dtos/
│
├── Reporting/
│   ├── Queries/   (GetSalesReport, GetInventoryReport, GetCustomerBalances, …)
│   └── Dtos/
│
├── Audit/
│   └── Queries/   (GetAuditLogs)
│
├── Settings/
│   ├── Commands/  (UpdateTenantSettings)
│   └── Queries/   (GetTenantSettings)
│
└── DependencyInjection.cs            ← AddApplication() — MediatR, FluentValidation, AutoMapper
```

> **قالب مجلد الأمر/الاستعلام الواحد** (Vertical Slice):
> ```
> Commands/CreateProduct/
> ├── CreateProductCommand.cs
> ├── CreateProductCommandHandler.cs
> ├── CreateProductCommandValidator.cs
> └── CreateProductResult.cs   (اختياري)
> ```

---

## 4) `SmartApp.Infrastructure` — الخدمات التقنية

> يعتمد على `Application` و`Domain` و`Shared`. يُنفّذ الواجهات المعرّفة في `Application` (عدا الوصول للبيانات — ذاك في `Persistence`).

```
SmartApp.Infrastructure/
├── Identity/
│   ├── JwtService.cs                 ← ينفّذ IJwtService
│   ├── TokenValidationParameters.cs
│   └── PermissionAuthorizationHandler.cs
│
├── MultiTenancy/
│   ├── TenantProvider.cs             ← ينفّذ ITenantProvider (يقرأ من HttpContext/Claims)
│   └── CurrentUserService.cs         ← ينفّذ ICurrentUserService
│
├── Services/
│   ├── DateTimeProvider.cs           ← SYSUTCDATETIME() wrapper
│   ├── EmailSender.cs                (اختياري — لإعادة تعيين كلمة المرور)
│   └── CacheService.cs
│
├── Security/
│   └── PasswordPolicy.cs
│
└── DependencyInjection.cs            ← AddInfrastructure()
```

---

## 5) `SmartApp.Persistence` — الوصول للبيانات

> يعتمد على `Application` و`Domain` و`Shared`. كل ما يخصّ EF Core هنا.

```
SmartApp.Persistence/
├── Context/
│   ├── AppDbContext.cs               ← ينفّذ IApplicationDbContext + Global Query Filters + TenantId stamping
│   └── AppDbContextFactory.cs        ← لأدوات الـ migrations وقت التصميم
│
├── Configurations/                   ← IEntityTypeConfiguration<T> لكل كيان
│   ├── Tenancy/
│   │   ├── TenantConfiguration.cs
│   │   └── TenantSettingConfiguration.cs
│   ├── Identity/
│   ├── Catalog/
│   ├── Inventory/
│   ├── Partners/
│   ├── Sales/
│   ├── Purchases/
│   ├── Audit/
│   └── Settings/
│
├── Repositories/
│   ├── Repository.cs                 ← IRepository<T> عام
│   ├── UnitOfWork.cs                 ← ينفّذ IUnitOfWork
│   └── (repositories مخصّصة عند الحاجة فقط)
│
├── Interceptors/
│   ├── AuditableEntityInterceptor.cs ← يملأ CreatedBy/ModifiedBy/التواريخ
│   └── SoftDeleteInterceptor.cs      ← يحوّل الحذف إلى IsDeleted=1
│
├── Migrations/                       ← EF Core migrations (كود مُولَّد)
│
├── Seed/
│   ├── PermissionSeeder.cs           ← يزرع مفاتيح الصلاحيات الثابتة
│   ├── RoleSeeder.cs                 ← الأدوار الافتراضية
│   └── SystemOwnerSeeder.cs          ← حساب مالك النظام الأول
│
└── DependencyInjection.cs            ← AddPersistence() — DbContext, Repositories, Interceptors
```

---

## 6) `SmartApp.Shared` — عناصر مشتركة محايدة

> **لا يعتمد على أي مشروع.** عناصر يستخدمها الجميع دون خلق تبعية دائرية.

```
SmartApp.Shared/
├── Results/
│   ├── Result.cs                     ← Result / Result<T> (نجاح/فشل موحّد)
│   ├── PagedResult.cs
│   └── Error.cs                      ← (Code, Message)
│
├── Constants/
│   ├── Permissions.cs                ← ثوابت مفاتيح الصلاحيات (products.create, …)
│   ├── Roles.cs                      ← أسماء الأدوار الافتراضية
│   ├── DocTypes.cs                   ← SALES_INVOICE, PURCHASE_INVOICE, …
│   └── CacheKeys.cs                  ← أنماط مفاتيح الكاش (t:{tenantId}:…)
│
├── Enums/
│   └── (Enums محايدة مشتركة عبر أكثر من طبقة)
│
├── Guards/
│   └── Guard.cs                      ← فحوص المدخلات (Guard.AgainstNull, …)
│
└── Extensions/
    └── (امتدادات عامّة: strings, dates, …)
```

> **ملاحظة تصميمية:** `Result<T>` و`PagedResult<T>` توضَع في `Shared` لأنها تُستخدم في `Application` (المخرجات) و`API` (التغليف). أي Enum جوهري للـ Domain يبقى في `Domain`؛ فقط الـ Enums المحايدة تماماً تُوضَع في `Shared`.

---

## 7) `SmartApp.API` — نقطة الدخول (Composition Root)

> يعتمد على كل المشاريع. الوحيد الذي يربط التنفيذ بالواجهات عبر DI. Controllers رفيعة (Thin).

```
SmartApp.API/
├── Controllers/
│   └── v1/
│       ├── AuthController.cs         ← login, refresh, logout, change-password
│       ├── TenantsController.cs      ← إدارة المستأجرين + التفعيل اليدوي (لمالك النظام)
│       ├── UsersController.cs
│       ├── RolesController.cs
│       ├── ProductsController.cs
│       ├── CategoriesController.cs
│       ├── CustomersController.cs
│       ├── SuppliersController.cs
│       ├── InventoryController.cs
│       ├── SalesController.cs
│       ├── PurchasesController.cs
│       ├── ReportsController.cs
│       ├── AuditController.cs
│       └── SettingsController.cs
│
├── Middleware/
│   ├── ExceptionHandlingMiddleware.cs ← يحوّل الأخطاء إلى ProblemDetails
│   ├── TenantResolutionMiddleware.cs  ← يستخرج المستأجر + يفحص Status
│   └── RequestLoggingMiddleware.cs    (أو Serilog built-in)
│
├── Authorization/
│   ├── PermissionRequirement.cs
│   └── PermissionPolicyProvider.cs    ← يحوّل products.view إلى Policy تلقائياً
│
├── Filters/
│   └── ApiExceptionFilter.cs          (بديل/مكمّل للـ middleware)
│
├── Extensions/
│   ├── SwaggerExtensions.cs
│   └── ServiceCollectionExtensions.cs
│
├── Configuration/
│   ├── JwtSettings.cs
│   └── (POCOs للإعدادات)
│
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json
└── Program.cs                         ← الـ Composition Root الفعلي
```

### `Program.cs` (ترتيب الربط في الجذر التركيبي)

```
builder.Services
    .AddSharedServices()        // من Shared
    .AddApplication()           // MediatR, FluentValidation, AutoMapper, Behaviors
    .AddInfrastructure(config)  // JWT, TenantProvider, Services
    .AddPersistence(config)     // DbContext, Repositories, Interceptors, Seed
    .AddApiServices(config);    // Controllers, Swagger, Auth, CORS, Versioning

// Middleware pipeline (بالترتيب الحاكم):
app.UseExceptionHandling();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseTenantResolution();      // بعد المصادقة ليقرأ tenant_id من الـ claim
app.UseAuthorization();
app.MapControllers();
```

---

## 8) بنية مشاريع الاختبار (Tests)

```
tests/
├── SmartApp.Domain.Tests/            ← اختبار قواعد الأعمال في الكيانات (بلا DB)
├── SmartApp.Application.Tests/       ← اختبار Handlers + Validators (Mocks/InMemory)
└── SmartApp.IntegrationTests/
    ├── Isolation/                    ← اختبارات العزل الإلزامية (Tenant A لا يرى B)
    ├── Auth/
    └── Modules/                      ← اختبارات end-to-end لكل وحدة
```

> **إلزامي:** مجلد `Isolation/` يحتوي اختبارات تثبت أن مستأجراً لا يرى/يكتب بيانات مستأجر آخر — تُشغَّل في كل CI. التفصيل في [09-Multi-Tenant.md](09-Multi-Tenant.md).

---

## 9) أين يوضَع كود POS مستقبلاً (Extensibility)

عند إضافة امتداد POS، **لا تُعدَّل النواة** — تُضاف مجلدات موازية:

```
Domain/Pos/           ← PosShift, PosSession, CashDrawerMovement, …
Application/Pos/       ← Commands/Queries الخاصة بـ POS
Persistence/Configurations/Pos/
API/Controllers/v1/PosController.cs
```

> POS يبني فوق `Sales` و`Inventory` (النواة) عبر واجهاتها العامّة، ولا يعدّل جداولها الأساسية. التفصيل في [04-Domain-Boundaries.md](04-Domain-Boundaries.md).

---

_يُكمّله [02-Solution-Architecture.md](02-Solution-Architecture.md) (المسؤوليات) و[04-Domain-Boundaries.md](04-Domain-Boundaries.md) (حدود الوحدات)._
