# 02 — System Architecture (معمارية النظام)

> يعتمد **Smart ERP POS** على **Clean Architecture** مع فصل صارم للطبقات، وحقن الاعتماديات (DI)، وأنماط **Repository + Unit of Work** و**CQRS عبر MediatR**. الهدف: كود قابل للاختبار، مستقل عن التفاصيل التقنية، وقابل للتطوّر دون كسر النواة.

---

## 1) نظرة عامة (Overview)

النظام مقسّم إلى **خمس طبقات** تتبع قاعدة الاعتماد (Dependency Rule): الاعتماديات تشير **دائماً للداخل** نحو النواة (Domain)، ولا تشير النواة للخارج أبداً.

```
            ┌───────────────────────────────────────────────────────────┐
            │                     PRESENTATION                          │
            │   ┌─────────────────┐        ┌────────────────────────┐    │
            │   │   MVC UI         │        │   Web API              │    │
            │   │ (Back-office)    │        │ (POS / Mobile / 3rd)   │    │
            │   │ Bootstrap+jQuery │        │ Controllers + JWT      │    │
            │   └────────┬────────┘        └───────────┬────────────┘    │
            └────────────┼───────────────────────────┼──────────────────┘
                         │        (تعتمد على)         │
                         ▼                           ▼
            ┌───────────────────────────────────────────────────────────┐
            │                     APPLICATION                           │
            │  Commands / Queries (MediatR) · Handlers · DTOs           │
            │  FluentValidation · AutoMapper Profiles · Interfaces      │
            └────────────┬──────────────────────────────┬───────────────┘
                         │                              │
       (تُنفَّذ عبر)      ▼                              ▼  (يعتمد على عقود)
            ┌──────────────────────────┐   ┌────────────────────────────┐
            │      INFRASTRUCTURE      │   │          DOMAIN            │
            │ EF Core DbContext        │──▶│ Entities · Value Objects   │
            │ Repositories · UoW       │   │ Enums · Domain Events      │
            │ External Services        │   │ BaseEntity · Interfaces    │
            │ Serilog · SignalR Hubs   │   │ (لا اعتماديات خارجية)      │
            └────────────┬─────────────┘   └────────────────────────────┘
                         │
                         ▼
            ┌──────────────────────────┐
            │      SQL Server 2022     │
            └──────────────────────────┘
```

> **قاعدة الاعتماد:** الأسهم تشير للداخل. `Domain` لا يعرف شيئاً عن EF Core أو ASP.NET. `Application` تعرف `Domain` فقط عبر واجهات. `Infrastructure` تُنفّذ واجهات `Application`. الطبقات الخارجية تعرف الداخلية، والعكس ممنوع.

---

## 2) الطبقات ومسؤولياتها (Layers & Responsibilities)

### 2.1 Domain Layer (النواة — `SmartPos.Domain`)

قلب النظام. لا اعتماديات على أي مكتبة خارجية (لا EF، لا ASP.NET، لا MediatR).

**يحتوي:**
- **Entities**: الكيانات المحاسبية (`Product`, `SalesInvoice`, `StockMovement`...).
- **BaseEntity**: الأعمدة المشتركة الإجبارية (انظر [04-Database-Design.md](04-Database-Design.md)).
- **Value Objects**: `Money`, `Barcode`, `Quantity` (كائنات غير قابلة للتغيير تحمل قواعد سلامة).
- **Enums**: `InvoiceStatus`, `MovementType`, `PaymentMethod`.
- **Domain Events**: `SalesInvoicePostedEvent` تُطلق عند ترحيل الفاتورة.
- **Domain Interfaces**: عقود المستودعات (`IRepository<T>`) والخدمات النقيّة.

```csharp
// Domain/Common/BaseEntity.cs — الأساس لكل جدول أعمال
public abstract class BaseEntity
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long? StoreId { get; set; }
    public DateTime CreatedDate { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? DeletedDate { get; set; }
    public long? DeletedBy { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] ConcurrencyStamp { get; set; } = default!; // ROWVERSION
}
```

**لماذا نواة نقيّة؟** لأن قواعد العمل المحاسبية هي أثمن ما نملك — يجب أن تكون قابلة للاختبار بلا قاعدة بيانات، ومستقلة عن أي إطار عمل قد نستبدله لاحقاً.

### 2.2 Application Layer (`SmartPos.Application`)

تنسّق حالات الاستخدام (Use Cases). تعتمد على `Domain` فقط، وتُعرّف واجهات لما تحتاجه من الخارج (مثل `IApplicationDbContext`, `ITenantProvider`, `IDateTime`).

**يحتوي:**
- **Commands & Queries** (نمط CQRS) + **Handlers** عبر **MediatR**.
- **DTOs** (كائنات نقل البيانات — لا تُكشف الـ Entities للخارج).
- **Validators** (**FluentValidation**) — تحقّق مدخلات كل Command.
- **AutoMapper Profiles** — تحويل Entity ↔ DTO.
- **Behaviors** (Pipeline): `ValidationBehavior`, `LoggingBehavior`, `TransactionBehavior`.

### 2.3 Infrastructure Layer (`SmartPos.Infrastructure`)

تُنفّذ الواجهات المُعرّفة في `Application`. هنا التفاصيل التقنية القابلة للاستبدال.

**يحتوي:**
- **`ApplicationDbContext`** (EF Core 9) + تكوينات الكيانات (`IEntityTypeConfiguration`).
- **Repositories + Unit of Work** — تنفيذ فعلي.
- **Global Query Filters** لعزل المستأجر و Soft Delete.
- **External Services**: البريد، الرسائل، بوّابات الدفع، تخزين الملفات.
- **Serilog Sinks**, **SignalR Hubs**, **Hangfire Jobs**.

### 2.4 API Layer (`SmartPos.Api`)

نقطة دخول للأنظمة الخارجية: POS، تطبيق الجوّال مستقبلاً، تكاملات الطرف الثالث.

- **Controllers** رفيعة (Thin) — تستقبل الطلب وتفوّضه لـ MediatR فقط.
- **JWT Authentication** + استخراج `TenantId` من الـ Claims.
- **Middleware**: معالجة الأخطاء الموحّدة، تسجيل الطلبات، حقن سياق المستأجر.
- **API Versioning** + **Swagger/OpenAPI**.

### 2.5 MVC UI Layer (`SmartPos.Web`)

الواجهة الخلفية (Back-office) لأصحاب المتاجر والمحاسبين.

- **ASP.NET Core MVC** + Bootstrap 5 + jQuery + AJAX + DataTables + Chart.js.
- Views + ViewModels، والاتصال إمّا مباشرة بـ MediatR أو عبر الـ API.
- **SignalR client** للإشعارات اللحظية ولوحة التحكّم الحيّة.

---

## 3) قاعدة الاعتماد (The Dependency Rule)

```
SmartPos.Web  ─┐
               ├─▶ SmartPos.Application ─▶ SmartPos.Domain
SmartPos.Api  ─┘            ▲
                            │
SmartPos.Infrastructure ────┘  (تُنفّذ واجهات Application، تعتمد Domain)
```

**القواعد الصارمة:**
1. `Domain` لا يرجع (`reference`) أي مشروع آخر.
2. `Application` يرجع `Domain` فقط.
3. `Infrastructure` يرجع `Application` و `Domain`.
4. `Api` و `Web` يرجعان `Application` (و`Infrastructure` عبر DI فقط في `Program.cs`، لا في الكود).

**الفائدة:** يمكن استبدال SQL Server بأي مخزن، أو EF Core بـ Dapper، أو MVC بـ Blazor — دون لمس قواعد العمل.

---

## 4) نمط Repository + Unit of Work

رغم أن EF Core نفسه يطبّق UoW و Repository، نضيف تجريداً رفيعاً لـ:
- إخفاء EF عن `Application` (اختبارية أسهل، عزل التقنية).
- تجميع الحفظ في **معاملة واحدة** لعمليات متعدّدة الجداول (فاتورة + بنود + مخزون).

```csharp
public interface IUnitOfWork : IDisposable
{
    IRepository<Product> Products { get; }
    IRepository<SalesInvoice> SalesInvoices { get; }
    IRepository<StockMovement> StockMovements { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IDbTransaction> BeginTransactionAsync(CancellationToken ct = default);
}
```

**مثال — بيع POS ذرّي:** إنشاء الفاتورة، إضافة البنود، خصم المخزون، تسجيل حركة الصندوق — كلّها ضمن `SaveChangesAsync` واحد داخل معاملة واحدة (Atomicity). انظر [13-Inventory.md](13-Inventory.md) و [18-POS.md](18-POS.md).

---

## 5) CQRS عبر MediatR

نفصل **الأوامر (Commands — تغيّر الحالة)** عن **الاستعلامات (Queries — تقرأ فقط)**.

```csharp
// Command
public record CreateSalesInvoiceCommand(long CustomerId, List<InvoiceItemDto> Items)
    : IRequest<Result<SalesInvoiceDto>>;

// Handler
public class CreateSalesInvoiceHandler
    : IRequestHandler<CreateSalesInvoiceCommand, Result<SalesInvoiceDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ITenantProvider _tenant;

    public async Task<Result<SalesInvoiceDto>> Handle(
        CreateSalesInvoiceCommand cmd, CancellationToken ct)
    {
        // 1) قواعد العمل  2) خصم المخزون  3) توليد رقم الفاتورة  4) حفظ ذرّي
    }
}
```

**لماذا CQRS؟**
- الاستعلامات الثقيلة (تقارير) تُحسَّن بمعزل عن الكتابة (Projections مباشرة لـ DTO، تجاوز التتبّع `AsNoTracking`).
- الأوامر تركّز على قواعد العمل والسلامة المحاسبية.
- كل Handler مسؤولية واحدة → يُختبر بسهولة.

> نستخدم CQRS **عند الحاجة** (مبدأ من [README.md](README.md)) — لا نُثقل كل CRUD بسيط به.

### Pipeline Behaviors

كل طلب MediatR يمرّ عبر سلسلة سلوكيات موحّدة قبل الوصول للـ Handler:

```
Request ─▶ LoggingBehavior ─▶ ValidationBehavior ─▶ TransactionBehavior ─▶ Handler
```

- **ValidationBehavior**: يشغّل FluentValidation؛ يرفض قبل لمس القاعدة.
- **TransactionBehavior**: يلفّ الأوامر (Commands) في معاملة تلقائياً.
- **LoggingBehavior**: يسجّل عبر Serilog مع `TenantId` و `CorrelationId`.

---

## 6) المكوّنات الداعمة (Cross-Cutting Concerns)

| المكوّن | الاستخدام | لماذا |
|---------|-----------|-------|
| **FluentValidation** | تحقّق مدخلات الأوامر | فصل قواعد التحقّق عن الـ Handler، رسائل واضحة |
| **AutoMapper** | Entity ↔ DTO | تقليل الكود المكرّر، منع كشف الكيانات |
| **Serilog** | تسجيل مُهيكل (Structured) | استعلام السجلات بـ `TenantId`, `CorrelationId` |
| **SignalR** | إشعارات ولوحة تحكّم لحظية | Push بدل Polling — كفاءة وتجربة أفضل |
| **Hangfire** | مهام خلفية (إغلاق يومي، تقارير مجدولة) | موثوقية التنفيذ + إعادة المحاولة |
| **MediatR** | CQRS + فصل الطبقات | Handlers مستقلة، Pipeline موحّد |

---

## 7) مخطّط تدفّق طلب كامل (Request Flow: بيع POS)

مثال: كاشير يسجّل عملية بيع من واجهة الـ POS حتى القاعدة.

```
[1] POS Client (JS)
      │  POST /api/sales-invoices  { customerId, items[] }  + JWT
      ▼
[2] API Middleware
      │  • يتحقّق من JWT
      │  • يستخرج TenantId + StoreId + UserId من Claims
      │  • يضع سياق المستأجر في ITenantProvider (scoped)
      ▼
[3] SalesInvoicesController  (Thin)
      │  _mediator.Send(new CreateSalesInvoiceCommand(...))
      ▼
[4] MediatR Pipeline
      │  LoggingBehavior → ValidationBehavior (FluentValidation) → TransactionBehavior
      ▼
[5] CreateSalesInvoiceHandler  (Application)
      │  • يتحقّق من قواعد العمل (رصيد، ائتمان العميل، صلاحية العرض)
      │  • يبني SalesInvoice + Items (Domain Entities)
      │  • يطلب رقم الفاتورة من Sequences (ذرّياً)
      │  • ينشئ StockMovement لكل بند (خصم)
      │  • يسجّل CashDrawerMovement
      ▼
[6] IUnitOfWork.SaveChangesAsync()  (Infrastructure)
      │  • EF Core يطبّق Global Query Filter (TenantId + !IsDeleted)
      │  • يملأ CreatedDate/CreatedBy تلقائياً (SaveChanges interceptor)
      │  • يتحقّق من ConcurrencyStamp (ROWVERSION)
      │  • كل ذلك داخل معاملة واحدة → COMMIT
      ▼
[7] SQL Server 2022  (RCSI مفعّل)
      │  INSERT invoice + items + movements  (ACID)
      ▼
[8] Domain Event: SalesInvoicePostedEvent
      │  • SignalR Hub يبثّ تحديث لوحة التحكّم للفرع
      │  • Hangfire: (اختياري) تحديث نقاط ولاء العميل
      ▼
[9] Response: SalesInvoiceDto (رقم الفاتورة، الإجمالي) ──▶ POS يطبع الإيصال
```

**نقاط سلامة في التدفّق:**
- `TenantId` يُحقن من الـ JWT **لا** من جسم الطلب (منع التلاعب).
- خصم المخزون وإنشاء الفاتورة في **معاملة واحدة** — لا فاتورة بلا خصم مخزون.
- رقم الفاتورة من `Sequences` عبر `UPDATE ... OUTPUT` ذرّياً (لا تعارض).

---

## 8) التعامل مع سياق المستأجر (Tenant Context Flow)

```csharp
// واجهة في Application
public interface ITenantProvider
{
    long CurrentTenantId { get; }
    long? CurrentStoreId { get; }
    long? CurrentUserId { get; }
}

// تنفيذ في Infrastructure/Api — يقرأ من HttpContext Claims (JWT)
public class HttpTenantProvider : ITenantProvider { /* من الـ Claims */ }
```

يُحقن `ITenantProvider` كـ **Scoped** ويُستهلك في:
- **Global Query Filter** في `DbContext` (عزل القراءة).
- **SaveChanges Interceptor** (ملء `TenantId`, `CreatedBy` عند الكتابة).

انظر [24-MultiTenant.md](24-MultiTenant.md) و [27-Security.md](27-Security.md).

---

## 9) لماذا اخترنا كل نمط (Justification)

| النمط/التقنية | البديل المرفوض | لماذا اخترناه |
|----------------|----------------|---------------|
| **Clean Architecture** | معمارية طبقية بسيطة (N-Tier) | قابلية اختبار قواعد العمل بلا DB، عزل التقنية عن النواة، تطوّر آمن |
| **CQRS + MediatR** | خدمات ضخمة (Fat Services) | فصل القراءة عن الكتابة، Handlers أحادية المسؤولية، Pipeline موحّد |
| **Repository + UoW** | استخدام DbContext مباشرة في Application | إخفاء EF، معاملات صريحة متعدّدة الجداول، اختبارية |
| **Global Query Filter** | فلترة يدوية بكل استعلام | استحالة نسيان `TenantId` → منع تسرّب البيانات معمارياً |
| **FluentValidation** | Data Annotations | قواعد معقّدة، رسائل واضحة، فصل عن النموذج |
| **Serilog (Structured)** | تسجيل نصّي | استعلام السجلات بحقول (`TenantId`) لا نصّ حرّ |
| **SignalR** | Polling دوري | تحديث لحظي، حمل أقل على الخادم |
| **BIGINT IDENTITY PK** | GUID كـ clustered | أداء وحجم أفضل كمفتاح مجمّع (انظر [04](04-Database-Design.md)) |

---

## 10) الوثائق ذات الصلة

| الوثيقة | الغرض |
|---------|-------|
| [03-Database-Strategy.md](03-Database-Strategy.md) | استراتيجية التخزين والعزل |
| [04-Database-Design.md](04-Database-Design.md) | معايير الجداول والأعمدة المشتركة |
| [24-MultiTenant.md](24-MultiTenant.md) | سياق المستأجر بالتفصيل |
| [25-API-Design.md](25-API-Design.md) | تصميم نقاط النهاية |
| [27-Security.md](27-Security.md) | الأمان و RLS |
| [29-Performance.md](29-Performance.md) | الأداء والفهرسة |

---

_وثيقة حيّة — تُحدَّث مع تطوّر المعمارية._
