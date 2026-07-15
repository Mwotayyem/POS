# 14 — Implementation Roadmap (خارطة التنفيذ)

> مراحل التطوير والترتيب المُوصى به لبناء SmartApp **بعد اعتماد المعمارية**. الترتيب مبني على اعتماد الوحدات ([04-Domain-Boundaries.md §3](04-Domain-Boundaries.md)): نبني الأساس أولاً، ثم ما يعتمد عليه.

**المبدأ:** لا يبدأ التنفيذ قبل اعتماد هذا التوثيق كاملاً. كل مرحلة تُسلَّم كشرائح عمودية كاملة ([13-Development-Rules §13](13-Development-Rules.md)) مع اختبارات.

---

## 0) شرط البدء (Gate 0 — Approval)

```
✅ اعتماد التوثيق المعماري (00→14) كاملاً.
✅ حسم أي أسئلة معلّقة (لا يوجد حالياً — كل القرارات محسومة).
✅ تجهيز البيئة (SQL Server 2022، .NET 9 SDK).
```

> **قبل هذه البوّابة: لا كود تنفيذي.**

---

## المرحلة 1 — الأساس (Foundation & Skeleton)

**الهدف:** solution يُبنى ويعمل بهيكل نظيف، بلا منطق أعمال بعد.

| # | المهمّة | المخرج |
|---|--------|--------|
| 1.1 | إنشاء الـ Solution والمشاريع الستّة + مراجع الاعتماد | `SmartApp.sln` يُبنى |
| 1.2 | `Directory.Build.props`, `.editorconfig`, `global.json` | معايير موحّدة |
| 1.3 | `BaseEntity` + الواجهات المشتركة (`ITenantOwned`, `IAuditable`, ...) | `Domain/Common` |
| 1.4 | `Result<T>`, `PagedResult<T>`, `Error`, ثوابت الصلاحيات | `Shared` |
| 1.5 | إعداد DI الأساسي + `Program.cs` + Serilog + Swagger | API يعمل (health check) |
| 1.6 | MediatR + Behaviors (Logging/Validation) + AutoMapper + FluentValidation | `Application/Common` |

**Gate 1:** المشروع يُبنى، يعمل، Swagger يظهر، لا تحذيرات.

---

## المرحلة 2 — Tenancy + العزل (Multi-Tenant Core)

**الهدف:** العزل يعمل من اليوم الأول — أساس كل ما بعده.

| # | المهمّة | المخرج |
|---|--------|--------|
| 2.1 | كيان `Tenant` + `TenantStatus` + `TenantSettings` | `Domain/Tenancy` |
| 2.2 | `AppDbContext` + Global Query Filter + ختم `TenantId` + SoftDelete interceptor | `Persistence` |
| 2.3 | `ITenantProvider` + `TenantProvider` (يقرأ من HttpContext) | `Infrastructure` |
| 2.4 | `TenantResolutionMiddleware` (استخراج + فحص Status) | `API` |
| 2.5 | **اختبارات العزل** (A لا يرى B · Suspended يُرفض) | `IntegrationTests/Isolation` |
| 2.6 | Migration أولى + seed مالك النظام الأول | `Persistence/Migrations` |

**Gate 2:** اختبارات العزل تمرّ · مستأجر Suspended يُرفض (403) · `TenantId` يُختَم تلقائياً.

> **حرج:** العزل يُبنى ويُختبَر **قبل** أي وحدة أعمال — لا نضيف بيانات قبل ضمان عزلها.

---

## المرحلة 3 — Identity & RBAC

**الهدف:** مصادقة وتفويض كاملان.

| # | المهمّة | المخرج |
|---|--------|--------|
| 3.1 | كيانات `AppUser`, `AppRole`, `Permission`, `RolePermission`, `RefreshToken` | `Domain/Identity` |
| 3.2 | ASP.NET Core Identity مُخصَّص + `PermissionSeeder` + `RoleSeeder` | `Persistence/Seed` |
| 3.3 | `IJwtService` + `JwtService` + توليد التوكن بالصلاحيات | `Infrastructure` |
| 3.4 | Login / Refresh (rotation + reuse detection) / Logout / ChangePassword | `Application/Identity` |
| 3.5 | `PermissionPolicyProvider` + `[HasPermission]` + `PermissionHandler` | `API/Authorization` |
| 3.6 | إدارة Users / Roles / تعيين الصلاحيات | `Application` + `API` |
| 3.7 | إدارة المستأجرين لمالك النظام (activate/suspend/disable) | `TenantsController` |

**Gate 3:** دخول يُصدر JWT بصلاحيات · endpoint محميّ بصلاحية يعمل · تفعيل/تعليق مستأجر يدوياً يعمل ويُدقَّق.

---

## المرحلة 4 — Catalog + Inventory Core

**الهدف:** المنتجات والمخزون (أساس البيع والشراء).

| # | المهمّة | المخرج |
|---|--------|--------|
| 4.1 | `Category` (شجري) + `Unit` + CRUD | `Application/Catalog` |
| 4.2 | `Product` + `ProductUnit` + `ProductBarcode` + CRUD + بحث بالباركود | `Application/Catalog` |
| 4.3 | `Stock` + `StockMovement` (append-only) + منطق WAC | `Application/Inventory` |
| 4.4 | `StockAdjustment` + بنوده + أثرها على الرصيد والحركات | `Application/Inventory` |
| 4.5 | اختبارات: WAC · حركة append-only · منع حذف منتج له حركات | tests |

**Gate 4:** إنشاء منتج · تسوية مخزون تُنشئ حركة وتحدّث الرصيد بـ WAC صحيح.

---

## المرحلة 5 — Partners + Sales + Purchases

**الهدف:** دورة البيع والشراء الكاملة (قلب النظام التجاري).

| # | المهمّة | المخرج |
|---|--------|--------|
| 5.1 | `Customer` / `Supplier` + CRUD + `CustomerPayment` / `SupplierPayment` | `Application/Partners` |
| 5.2 | `Sequences` + توليد أرقام المستندات الذرّي | `Application/Common` |
| 5.3 | `PurchaseInvoice` + بنوده + أثر WAC + حركة مخزون واردة + رصيد المورد (معاملة ذرّية) | `Application/Purchases` |
| 5.4 | `SalesInvoice` + بنوده (snapshot) + حركة صادرة + رصيد العميل (معاملة ذرّية) | `Application/Sales` |
| 5.5 | `SalesReturn` / `PurchaseReturn` (سعر مجمّد + منع إرجاع زائد ذرّياً) | `Application` |
| 5.6 | اختبارات: ذرّية الفاتورة · منع الإرجاع الزائد · تجميد السعر · أثر WAC | tests |

**Gate 5:** فاتورة بيع كاملة (رقم+بنود+مخزون+رصيد+تدقيق) في معاملة واحدة · مرتجع بسعر أصلي · منع إرجاع زائد.

---

## المرحلة 6 — Reporting + Audit + Settings

**الهدف:** استكمال النواة بالقراءة والتدقيق والتهيئة.

| # | المهمّة | المخرج |
|---|--------|--------|
| 6.1 | `AuditableEntityInterceptor` + كتابة `AuditLog` تلقائياً | `Persistence` |
| 6.2 | استعلام سجل التدقيق | `Application/Audit` |
| 6.3 | تقارير: مبيعات · مشتريات · مخزون · أرصدة عملاء/موردين · ربحية | `Application/Reporting` |
| 6.4 | `TenantSettings` (عملة/ضريبة/منطقة زمنية/ثيم) قراءة وتحديث | `Application/Settings` |

**Gate 6:** كل تغيير حسّاس يُسجَّل تدقيقاً · تقارير النواة تعمل مع احترام العزل.

---

## المرحلة 7 — التصلّب والإطلاق (Hardening & Launch)

**الهدف:** جاهزية إنتاج.

| # | المهمّة | المخرج |
|---|--------|--------|
| 7.1 | تدقيق أمني شامل (OWASP checklist [11-Security §9](11-Security-Architecture.md)) | تقرير |
| 7.2 | Security headers · Rate limiting · CORS · TLS/HSTS | `API` |
| 7.3 | RCSI + مراجعة خطط التنفيذ للتقارير الثقيلة | DB |
| 7.4 | CI/CD: بناء + اختبارات (منها العزل) + فحص الحزم + migrations | pipeline |
| 7.5 | Seed بيانات مرجعية + توثيق تشغيلي (نشر/نسخ احتياطي/استعادة) | docs |
| 7.6 | اختبار تكامل end-to-end لكل الوحدات | tests |

**Gate 7:** كل الاختبارات خضراء · تدقيق أمني نظيف · نشر ناجح لبيئة staging.

---

## المرحلة 8+ — الامتدادات المستقبلية (Post-Core Extensions)

تُبنى **بعد** استقرار النواة، كوحدات موازية دون تعديلها ([04-Domain-Boundaries §5](04-Domain-Boundaries.md)):

| المرحلة | الامتداد | يعتمد على |
|:-------:|----------|-----------|
| 8 | **POS Terminal + Cashier Shifts** | Sales, Inventory, Partners |
| 9 | **Barcode POS Terminal** | Catalog (ProductBarcodes) |
| 10 | **Promotions & Offers** | Sales (نقطة حقن حساب الفاتورة) |
| 11 | **Loyalty Program** | Sales (event `SalesInvoiceCreated`) |
| 12 | **Advanced Multi-store** | إضافة `StoreId` انتقائياً + جدول `Stores` |
| — | **RLS (تقوية العزل)** | تفعيل SQL Security Policy ([11-Security §6](11-Security-Architecture.md)) |

> كل امتداد يمرّ ببوّابته الخاصّة، ويُختبَر أن حذفه لا يكسر النواة (اختبار سلامة الحدود).

---

## ترتيب التنفيذ — نظرة كلّية

```
Gate 0: اعتماد التوثيق
   │
   ▼
① Foundation ──► ② Tenancy+Isolation ──► ③ Identity+RBAC
                                              │
   ┌──────────────────────────────────────────┘
   ▼
④ Catalog+Inventory ──► ⑤ Partners+Sales+Purchases ──► ⑥ Reporting+Audit+Settings
                                                              │
                                                              ▼
                                                     ⑦ Hardening & Launch (نواة جاهزة)
                                                              │
                                                              ▼
                                         ⑧+ Extensions (POS · Offers · Loyalty · Multi-store · RLS)
```

---

## مبادئ التنفيذ الحاكمة (Execution Principles)

1. **العزل أولاً** — المرحلة 2 قبل أي بيانات أعمال.
2. **شرائح عمودية كاملة** — كل ميزة: Entity→DTO→Validation→Handler→Endpoint→Migration→Test.
3. **بوّابة لكل مرحلة** — لا انتقال قبل خضرة اختبارات المرحلة السابقة (خاصّة العزل).
4. **النواة تعمل بلا امتداد** — تُسلَّم نواة كاملة قابلة للبيع قبل أي POS.
5. **لا SaaS** — لا جدول/كود اشتراك أو فوترة أو دفع في أي مرحلة.

---

_يُكمّله [04-Domain-Boundaries.md](04-Domain-Boundaries.md) (اعتماد الوحدات) و[13-Development-Rules.md](13-Development-Rules.md) (Definition of Done لكل شريحة). آخر ملفات التوثيق المعماري — بعده يبدأ التنفيذ عند الاعتماد._
