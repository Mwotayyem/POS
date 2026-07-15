# CODING_RULES — Smart ERP POS

> Governed by **[MASTER_PROMPT.md](./MASTER_PROMPT.md)**. How code is structured and written. Clean Architecture + CQRS + Repository/UoW are mandatory. Code you produce must be indistinguishable from a senior teammate's and must satisfy the Definition of Done.

---

## 1. Clean Architecture — Layers & Dependency Direction

Four projects. **Dependencies point inward only.** Inner layers know nothing about outer layers.

```
Web (API + MVC)  ──►  Application  ──►  Domain
        │                  │
        └──►  Infrastructure ──► (implements Application/Domain abstractions)
```

- **Domain** (`SmartErpPos.Domain`) — enterprise core. Entities, value objects, domain enums, domain exceptions, domain services/interfaces, business invariants. **Depends on nothing** (no EF, no ASP.NET, no MediatR). Pure C#.
- **Application** (`SmartErpPos.Application`) — use cases. CQRS Commands/Queries + Handlers (MediatR), DTOs, FluentValidation validators, AutoMapper profiles, pipeline behaviors (validation, logging, transaction), and **interfaces** for repositories/UoW/external services. Depends on Domain only.
- **Infrastructure** (`SmartErpPos.Infrastructure`) — implementations. EF Core `DbContext`, entity configurations, migrations, Repository + UoW implementations, Identity, JWT, Serilog sinks, SignalR hubs' backing, external integrations, tenant provider. Depends on Application + Domain.
- **Web** (`SmartErpPos.Web` / `.Api`) — delivery. Controllers, MVC, Razor, middleware, DI composition root, Swagger. Depends on Application (+ Infrastructure for DI wiring only).

**Hard rules:** Domain never references EF/ASP.NET/MediatR. Controllers never touch `DbContext` directly. Business logic never lives in controllers or Razor. Cross-layer calls go through interfaces defined in the inner layer.

---

## 2. SOLID

- **S** — one reason to change: one handler per use case; one repository per aggregate; validators, mappers, and handlers are separate.
- **O** — extend via new handlers/strategies (e.g. offer types, costing methods) rather than editing switch-bombs.
- **L** — derived types honor base contracts; no surprising overrides.
- **I** — small, focused interfaces (`IProductRepository`, not a god `IRepository` with 40 methods) — a generic base repo is fine, specialized ones extend it.
- **D** — depend on abstractions (interfaces in Application/Domain), inject implementations from Infrastructure via DI. No `new`-ing concrete services across layers.

---

## 3. CQRS with MediatR

3.1. **Commands** change state (`CreateProductCommand`, `PostSaleCommand`, `ProcessReturnCommand`) and return minimal results (id/result DTO). **Queries** read (`GetProductByIdQuery`, `GetProductsPagedQuery`) and return DTOs. Never mix read and write in one handler.

3.2. Each request has: the Command/Query (record), its Handler, its FluentValidation validator, and (for reads) a projection to DTO. One use case = one vertical file group.

3.3. **Pipeline behaviors** (in order): correlation/logging → validation (FluentValidation) → transaction/UoW (for commands) → handler. Cross-cutting concerns live in behaviors, not scattered in handlers.

3.4. Write side loads tracked aggregates and enforces invariants; read side uses `AsNoTracking()` projections (`Select` to DTO) — do not load full graphs for reads (**[DATABASE_RULES.md](./DATABASE_RULES.md)** §10).

---

## 4. Repository + Unit of Work

4.1. Repositories abstract EF for **aggregates** (`IProductRepository`, `IInvoiceRepository`, `IStockRepository`). A generic `IRepository<T>` provides common CRUD; specialized repos add aggregate-specific queries. Repositories return domain entities (write side) or are used behind query projections.

4.2. **Unit of Work** (`IUnitOfWork`) owns the transaction boundary and `SaveChangesAsync`. Command handlers do work through repositories and commit once via UoW (wired through the transaction behavior). Multi-table business operations (sale + stock + ledger) commit atomically (**[DATABASE_RULES.md](./DATABASE_RULES.md)** §6, **[BUSINESS_RULES.md](./BUSINESS_RULES.md)**).

4.3. Repository/UoW **interfaces live in Application/Domain**; **implementations in Infrastructure**. Handlers depend on the interfaces only.

4.4. No leaking `IQueryable` of entities out of Infrastructure into Web. No `DbContext` injected into controllers or handlers directly (handlers use repos/UoW; read handlers may use a dedicated read abstraction).

---

## 5. DTOs & Mapping

5.1. **Never expose EF entities across the API or into views.** Every request binds to a request DTO/command; every response returns a response DTO. This prevents over-posting, lazy-load serialization, cycles, and tenant leakage (**[SECURITY_RULES.md](./SECURITY_RULES.md)**, **[API_RULES.md](./API_RULES.md)**).

5.2. Mapping via **AutoMapper** profiles (Application layer). Keep mappings explicit and tested; avoid mapping sensitive/audit/tenant fields into inbound entities. Complex projections may be hand-written LINQ `Select` for clarity/perf.

5.3. DTOs are purpose-built (a create DTO ≠ update DTO ≠ read DTO). Don't reuse one fat DTO for everything.

---

## 6. Naming Conventions

- **Types:** PascalCase. Interfaces `I`-prefixed. Async methods end with `Async`.
- **Commands/Queries:** `VerbNounCommand` / `GetNoun[By…]Query`; handlers `…CommandHandler`/`…QueryHandler`; validators `…Validator`.
- **DTOs:** `NounDto`, `CreateNounRequest`, `UpdateNounRequest`, `NounListItemDto`.
- **Repositories:** `INounRepository` / `NounRepository`.
- **Fields:** `_camelCase` private; `camelCase` locals/params; `PascalCase` public/consts.
- **Files:** one top-level type per file, filename = type name. Folder-by-feature within each layer (`Application/Products/Commands/CreateProduct/…`).
- No abbreviations except domain-standard (SKU, PO, UOM). Names read as intent.

---

## 7. Async / Await

7.1. All I/O (DB, HTTP, IO, SignalR) is **async all the way**: `async Task<T>`, `await`, no `.Result`/`.Wait()`/`.GetAwaiter().GetResult()` (deadlocks/thread starvation).

7.2. Pass and honor `CancellationToken` from controller → handler → repository → EF. Use `ConfigureAwait` appropriately in libraries.

7.3. No `async void` except event handlers. Don't wrap sync work in `Task.Run` on the server. Avoid fire-and-forget without a supervised background service.

---

## 8. No Duplication (DRY) & Reuse

8.1. Extract shared logic (audit stamping, tenant stamping, pagination, envelope building, error mapping, common validation rules) into base classes/behaviors/helpers used everywhere. If you write the same block twice, refactor.

8.2. Cross-cutting concerns (logging, validation, transactions, auth) are **behaviors/middleware/filters**, not repeated in each handler/controller.

8.3. Reuse existing components/patterns; match the codebase. Consistency beats novelty (**[MASTER_PROMPT.md](./MASTER_PROMPT.md)** §6).

---

## 9. Every Feature Is a Full Vertical Slice

A feature/change is **not complete** until it includes ALL of:

1. **Domain** — entity/value object/invariant changes.
2. **DTOs** — request/response.
3. **Validation** — FluentValidation validators.
4. **Business logic** — Command/Query + Handler enforcing **[BUSINESS_RULES.md](./BUSINESS_RULES.md)**.
5. **Persistence** — repository methods, EF configuration, and an **EF migration**.
6. **API** — versioned endpoint returning the envelope (**[API_RULES.md](./API_RULES.md)**).
7. **UI** — MVC view/partial with loading/empty/error states (**[UI_RULES.md](./UI_RULES.md)**).
8. **Security** — auth + permission + tenant scoping (**[SECURITY_RULES.md](./SECURITY_RULES.md)**).
9. **Logging** — Serilog structured events with correlation id.
10. **Exception handling** — typed domain exceptions mapped centrally.
11. **Tests** — unit tests for handlers/validators/domain rules; integration tests for the endpoint (happy + failure + tenant-isolation path).
12. **Docs** — Swagger annotations + a short note on assumptions/decisions.

Delivering only part of the slice is a rule violation — state explicitly if scope is intentionally partial and why.

---

## 10. Error Handling

10.1. Throw **typed domain exceptions** for business violations (`InsufficientStockException`, `CreditLimitExceededException`, `ReturnExceedsOriginalException`, `TenantMismatchException`). The global exception middleware maps them to statuses/envelope (**[API_RULES.md](./API_RULES.md)** §5).

10.2. Never swallow exceptions silently; never `catch (Exception) {}`. Catch narrowly, add context, rethrow or map. No control flow via exceptions for normal validation (that's FluentValidation → 422).

10.3. Guard clauses at method entry (null/args/invariants). Fail fast with clear messages. No returning `null` to signal errors where a result/exception is clearer.

---

## 11. Comments & Documentation

11.1. Code is self-documenting via clear names; comments explain **why**, not **what**. XML doc comments on public APIs/DTOs (feeds Swagger).

11.2. Document non-obvious business decisions and any assumptions made when a requirement was ambiguous (**[AI_RULES.md](./AI_RULES.md)**). No commented-out dead code, no noise comments.

---

## 12. Quality Bar

- Nullable reference types on; no `!` null-forgiving to silence real nulls.
- No magic numbers/strings — use constants/enums/config.
- Small methods, single responsibility; cyclomatic complexity kept low.
- Deterministic, testable units; inject `IClock`/time, don't call `DateTime.Now` (use UTC via an abstraction).
- No warnings introduced; analyzers respected; format consistent (`.editorconfig`).

---

## 13. Coding Checklist (per unit)

- [ ] Correct layer; dependency direction inward; Domain pure.
- [ ] CQRS: command/query + handler + validator; behaviors handle cross-cutting.
- [ ] Repository/UoW via interfaces; one transaction for multi-table writes.
- [ ] Binds/returns DTOs only; AutoMapper profile; no entity leakage/over-posting.
- [ ] Async all the way with `CancellationToken`; UTC via clock abstraction.
- [ ] Typed domain exceptions; central mapping; no silent catches.
- [ ] No duplication; cross-cutting in behaviors/middleware.
- [ ] Full vertical slice incl. migration, tests, logging, docs.
