# 13 — Development Rules & Coding Standards (قواعد التطوير ومعايير الكود)

> القواعد الحاكمة لكتابة الكود: SOLID، Clean Architecture، CQRS، Repository/UoW، التسمية، Async، معالجة الأخطاء، و**Definition of Done**. **إلزامية** لكل كود يُولَّد بعد اعتماد المعمارية.

---

## 1) المبادئ العليا (Top Principles)

1. **Domain لا يعتمد على شيء** — لا EF ولا MediatR ولا مراجع خارجية في `SmartApp.Domain`.
2. **Thin Controllers** — كل المنطق في MediatR Handlers؛ الـ Controller يرسل ويعيد فقط.
3. **لا تسرّب كيانات** — الـ Handlers تُعيد DTOs دائماً، لا `Entity`.
4. **Tenant-safe by default** — لا كتابة يدوية لشرط `TenantId`؛ الفلتر العالمي والختم التلقائي.
5. **Async في كل I/O** — `async/await` مع `CancellationToken` لكل عملية DB/شبكة.
6. **Production-ready only** — لا `TODO`، لا `NotImplementedException`، لا بيانات وهمية، لا `Console.WriteLine`.
7. **Consistency over cleverness** — طابِق الأنماط والتسمية القائمة.

---

## 2) SOLID عملياً

| المبدأ | التطبيق في SmartApp |
|--------|----------------------|
| **S** — Single Responsibility | كل Handler يعالج حالة استخدام واحدة · كل Validator لأمر واحد |
| **O** — Open/Closed | إضافة ميزة = Command/Query جديد لا تعديل قائم · POS كامتداد لا تعديل نواة |
| **L** — Liskov | كل `BaseEntity` قابل للاستبدال في الفلتر العالمي · Repositories عبر واجهاتها |
| **I** — Interface Segregation | واجهات صغيرة مركّزة (`ITenantProvider`, `IJwtService`) لا واجهة عملاقة |
| **D** — Dependency Inversion | الطبقات تعتمد على واجهات `Application` لا على تنفيذ `Infrastructure`/`Persistence` |

---

## 3) Clean Architecture — قواعد الطبقات

```
✅ مسموح:  API → Application → Domain
✅ مسموح:  Infrastructure/Persistence → Application (تنفيذ واجهاتها)
✅ مسموح:  الكل → Shared
❌ ممنوع:  Domain → أي مشروع (عدا Shared)
❌ ممنوع:  Application → Infrastructure/Persistence (تعرف الواجهات فقط)
❌ ممنوع:  استخدام DbContext مباشرة في Controller
❌ ممنوع:  منطق أعمال في Controller أو Persistence
```

**اختبار السلامة:** لو حذفت `Infrastructure` و`Persistence`، يجب أن يُبنى `Application` و`Domain` (يعتمدان على واجهات فقط).

---

## 4) CQRS — قواعد الأوامر والاستعلامات

| القاعدة | التفصيل |
|---------|---------|
| فصل واضح | `Command` يُغيّر حالة · `Query` يقرأ فقط (لا side effects) |
| Handler واحد لكل Command/Query | مسؤولية مفردة |
| Command يُعيد الحدّ الأدنى | Id أو `Result` — لا كيان كامل غالباً |
| Query يُعيد DTO مباشرة | projection في الاستعلام (تجنّب N+1) |
| الأوامر داخل معاملة | `TransactionBehavior` يغلّف كل Command تلقائياً |
| التحقّق قبل الـ Handler | `ValidationBehavior` يرفض المُدخَل الخاطئ مبكراً |

### قالب الشريحة العمودية (Vertical Slice)

```
Application/Catalog/Commands/CreateProduct/
├── CreateProductCommand.cs         : IRequest<Result<long>>
├── CreateProductCommandHandler.cs  : IRequestHandler<CreateProductCommand, Result<long>>
└── CreateProductCommandValidator.cs: AbstractValidator<CreateProductCommand>
```

---

## 5) Repository & Unit of Work

```csharp
// واجهات في Application، تنفيذ في Persistence
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(long id, CancellationToken ct);
    Task<T> AddAsync(T entity, CancellationToken ct);
    void Update(T entity);
    void Remove(T entity);                // soft delete عبر الـ interceptor
    IQueryable<T> Query();                // يخضع للفلتر العالمي
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
    Task<IDisposable> BeginTransactionAsync(CancellationToken ct);
}
```

| القاعدة | التفصيل |
|---------|---------|
| متى Repository | للعمليات الشائعة والكيانات الجذرية (Aggregate Roots) |
| متى `IApplicationDbContext` مباشرة | للاستعلامات المعقّدة/التقارير (projection) — أبسط من repo عملاق |
| معاملة | عملية متعدّدة الجداول (فاتورة+بنود+مخزون+رصيد) في `UnitOfWork` واحد |
| لا `SaveChanges` متعدّد | حفظ واحد في نهاية العملية للذرّية |

---

## 6) قواعد الأعمال المحورية (Critical Business Rules)

هذه القواعد **مورّثة ومُكيَّفة** من التصميم القديم — إلزامية في التنفيذ:

### 6.1 المرتجعات بسعر أصلي مجمّد

```
① كل مرتجع يشير للفاتورة/البند الأصلي (SalesReturnItems.SalesInvoiceItemId).
② السعر = UnitPrice المجمّد من البند الأصلي — لا سعر المنتج الحالي.
③ منع الإرجاع الزائد على مستويين:
     - قاعدياً:  CHECK (ReturnedQty <= Quantity)
     - تزامنياً: UPDATE ... SET ReturnedQty += @q
                 WHERE Id=@item AND ReturnedQty + @q <= Quantity  (ذرّي)
```

### 6.2 تكلفة المخزون — Weighted Average Cost (WAC)

```
عند الشراء (وارد):
   newAvgCost = (QtyOnHand*AvgCost + PurchaseQty*PurchaseCost) / (QtyOnHand + PurchaseQty)
عند البيع (صادر):
   يُخصَم بالتكلفة المتوسّطة الحالية · AvgCost لا يتغيّر بالبيع
كل حركة → StockMovements (append-only) بـ snapshot للرصيد والتكلفة بعدها.
```

### 6.3 السجلّات Append-Only

```
StockMovements, AuditLogs:  INSERT فقط. لا UPDATE ولا DELETE ولا soft delete.
أي تصحيح = حركة معاكسة جديدة (Reversing Entry).
```

### 6.4 الذرّية (Atomicity)

```
إنشاء فاتورة = معاملة واحدة:
   توليد رقم (Sequences) + الفاتورة + البنود + حركة مخزون + رصيد شريك + AuditLog
أي فشل جزئي → rollback كامل. لا حالة نصف-مكتملة أبداً.
```

---

## 7) معايير التسمية (Naming Conventions)

| العنصر | النمط | مثال |
|--------|-------|------|
| Class / Method / Property | PascalCase | `CreateProductCommand`, `GetByIdAsync` |
| المتغيّرات المحلّية / المُعاملات | camelCase | `productId`, `tenantId` |
| الحقول الخاصّة | `_camelCase` | `_mediator`, `_tenant` |
| الواجهات | `I` + PascalCase | `ITenantProvider` |
| الثوابت | PascalCase | `Permissions.Products.Create` |
| Command/Query | فعل + مورد + Command/Query | `UpdateCustomerCommand`, `GetSalesReportQuery` |
| Handler | نفس الاسم + Handler | `UpdateCustomerCommandHandler` |
| DTO | مورد + Dto | `ProductDto`, `SalesInvoiceDto` |
| الجداول | PascalCase جمع | `Products`, `SalesInvoices` |
| الأعمدة | PascalCase | `TenantId`, `GrandTotal` |

---

## 8) Async & Performance

```
✅ كل عملية DB/شبكة async مع CancellationToken.
✅ projection مباشر إلى DTO في الاستعلامات (Select) — تجنّب N+1.
✅ ترقيم إجباري لكل قائمة (Skip/Take) — لا ToList() لجدول كامل.
✅ AsNoTracking() لاستعلامات القراءة فقط.
❌ لا .Result / .Wait() (deadlock).
❌ لا async void (عدا event handlers).
```

---

## 9) معالجة الأخطاء (Error Handling)

```
① الأخطاء المتوقّعة → استثناءات Application محدّدة
     (NotFoundException, ValidationException, BusinessRuleViolationException).
② ExceptionHandlingMiddleware يحوّلها إلى ProblemDetails موحّد ([12-API §4]).
③ لا try/catch في الـ Handlers لابتلاع الأخطاء — دعها تصعد للـ middleware.
④ رسائل عامّة للعميل · التفاصيل في اللوج (Serilog + correlation id).
⑤ قواعد الأعمال تُرمى من الـ Domain/Handler، لا تُفحَص في الـ Controller.
```

---

## 10) Validation & Mapping

| الأداة | القاعدة |
|--------|---------|
| **FluentValidation** | Validator لكل Command/Query يقبل مُدخَلاً · يُشغَّل عبر `ValidationBehavior` قبل الـ Handler |
| **AutoMapper** | Profiles للتحويل Entity↔DTO · لا mapping يدوي متكرّر · لا منطق أعمال في الـ mapping |
| **Guard** | فحوص الثوابت الأساسية عبر `Shared/Guards/Guard.cs` |

---

## 11) Logging & Audit

```
✅ Serilog مُنظَّم (structured) بـ correlation id لكل طلب.
✅ AuditLog لكل تغيير حسّاس (عبر AuditableEntityInterceptor / Behavior).
✅ تسجيل أحداث الأمان (فشل دخول، reuse توكن، تغيير حالة مستأجر).
❌ لا أسرار/كلمات مرور/توكنات في اللوج.
❌ لا معلومات مستأجر آخر في لوج مستأجر.
```

---

## 12) الاختبارات (Testing)

| النوع | النطاق | المشروع |
|-------|--------|---------|
| Unit | قواعد الأعمال في الكيانات (WAC, منع الإرجاع الزائد) بلا DB | `Domain.Tests` |
| Unit | Handlers + Validators (mocks / InMemory) | `Application.Tests` |
| Integration | end-to-end لكل وحدة | `IntegrationTests` |
| **Isolation (إلزامي)** | مستأجر A لا يرى/يكتب بيانات B · مستأجر Suspended يُرفض | `IntegrationTests/Isolation` |

> اختبارات العزل تُشغَّل في **كل CI** — أي كسر للعزل يوقف الدمج.

---

## 13) Definition of Done (لكل وحدة عمل)

الكود مقبول فقط إذا:

- [ ] يُبنى تحت .NET 9 مع nullable enabled بلا تحذيرات مُقدَّمة.
- [ ] يحترم اتجاه اعتماد Clean Architecture (Domain لا يعتمد على شيء).
- [ ] Tenant-safe: `TenantId` مختوم خادم-جانبياً، الفلتر العالمي محترَم، لا مسار عبر-مستأجر.
- [ ] الأموال `DECIMAL(18,4)` · التواريخ UTC · الجداول الجديدة بأعمدة audit + soft delete + concurrency.
- [ ] المُدخَلات مُتحقَّق منها (FluentValidation) · المُخرَجات DTOs (لا تسرّب كيانات).
- [ ] الأخطاء مُعالَجة مركزياً · مُسجَّلة بـ correlation id · لا أسرار في اللوج.
- [ ] يتضمّن migration واختبارات (منها اختبار عزل عند إضافة كيان).
- [ ] يتّبع كل القواعد أعلاه · شريحة عمودية كاملة (Entity→DTO→Validation→Handler→Endpoint→Migration→Test).

---

## 14) ما يُمنَع صراحةً (Forbidden)

```
❌ منطق أعمال في Controller أو Persistence.
❌ DbContext في Controller.
❌ إعادة Entity من Handler (استخدم DTO).
❌ قراءة TenantId من العميل.
❌ IgnoreQueryFilters() / FromSqlRaw بلا مراجعة أمنية.
❌ money كـ float/double.
❌ حذف فعلي لبيانات مالية (soft delete فقط).
❌ UPDATE/DELETE على StockMovements أو AuditLogs.
❌ إنشاء جداول Subscription/Billing/Payment/Plan (لا SaaS).
❌ TODO / NotImplementedException / بيانات وهمية في كود مُسلَّم.
```

---

_يُكمّله [02-Solution-Architecture.md](02-Solution-Architecture.md) · [04-Domain-Boundaries.md](04-Domain-Boundaries.md) · [12-API-Architecture.md](12-API-Architecture.md). هذا الملف هو دستور كتابة الكود بعد الاعتماد._
