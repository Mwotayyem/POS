# SmartApp — Project Summary

**SmartApp** is a multi-tenant **Business Management Platform** built on ASP.NET Core (.NET 9) with
Clean Architecture. It is **not** SaaS: no subscriptions, billing, payments, or plans. Tenants are
activated/suspended/disabled **manually** by a system owner.

> This document is the living, high-level state of the backend. It is updated at the end of every
> phase. The governing design reference is [`SmartApp-Architecture/`](../SmartApp-Architecture/).

---

## Architecture

| Concern | Choice |
|---------|--------|
| Style | Clean Architecture — Domain / Application / Infrastructure / Persistence / Shared / API |
| Runtime | .NET 9 (C# 13, Nullable, ImplicitUsings, `TreatWarningsAsErrors=true`, latest-recommended analyzers) |
| CQRS | MediatR 12.4.1 (Commands / Queries / Handlers) |
| Validation | FluentValidation 11.11 via a MediatR `ValidationBehavior` pipeline |
| Persistence | EF Core 9 — SQL Server (production) / SQLite in-memory (tests) |
| AuthN | JWT Bearer (access + refresh with rotation & reuse detection) |
| AuthZ | Permission-based RBAC — `[HasPermission("resource.action")]` + dynamic policy provider |
| Multi-tenancy | EF Core global query filter + server-side `TenantId` stamping (never from client) |
| API shape | Unified response envelope `ApiResponse<T>` (success/data/error/meta) + `Result<T>` |
| Versioning | Asp.Versioning — URL segment `/api/v1/...` |
| Docs | Swagger / Swashbuckle 7.2 (JWT authorize button) |

**Dependency direction:** `API → Application → Domain → Shared`; `Infrastructure`/`Persistence →
Application + Domain + Shared`; `API` is the composition root.

**Data conventions:** every tenant-owned table inherits `BaseEntity` (Id BIGINT IDENTITY, TenantId,
audit fields, soft-delete, `ConcurrencyStamp` ROWVERSION). Money = `DECIMAL(18,4)`. Dates =
`DATETIME2(3)` UTC. All FKs `ON DELETE NO ACTION` (soft-delete; dependency checks in the Application
layer). Composite uniqueness = filtered unique index including `TenantId`, `WHERE [IsDeleted]=0`.

---

## Phase status

| Phase | Module | Status |
|-------|--------|--------|
| 1 | Foundation (solution, 6 projects + 3 test projects, DI, versioning, Swagger) | ✅ |
| 2 | Domain + Persistence foundation (BaseEntity, DbContext, global filter, audit interceptor) | ✅ |
| 3 | Tenant Core (`Tenant`, `TenantSetting`, `InitialCreate` migration, resolution middleware) | ✅ |
| 4 | Identity & RBAC (User/Role/Permission/UserRole/RolePermission/RefreshToken, `AddIdentity`) | ✅ |
| 5 | API + Authentication (envelope, login/refresh/logout, permission authorization) | ✅ |
| 6 | Administration (Users, Roles, Permissions, Tenant Settings, Profile + seeding) | ✅ |
| 7 | Catalog (Categories, Units, Brands, Products, ProductUnits, Barcodes, Prices) | ✅ |
| 8 | Inventory (Warehouses, Stock, StockMovements append-only, Adjustments, Transfers, WAC) | ✅ |
| 9 | Purchasing (Suppliers, Purchase Orders/Invoices/Returns, document numbering) | ✅ |
| 10 | Sales (Customers, Sales Invoices, Payments, Returns, cost-of-sale snapshot) | ✅ |
| 11 | Dashboard & Reports API (summary, sales/low-stock/product reports) | ✅ |
| 12 | Production Readiness (health check, CORS, rate limiting, SECURITY/DEPLOYMENT docs) | ✅ |
| 13 | **Frontend Application** (React + Vite + TypeScript SPA) | ✅ |

---

## Data model so far

**Tenancy:** `Tenants`, `TenantSettings`
**Identity:** `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions`, `RefreshTokens`
**Catalog:** `Categories` (tree), `Units`, `Brands`, `Products`, `ProductUnits`, `ProductBarcodes`,
`ProductPrices`
**Inventory:** `Warehouses`, `Stocks` (per product+warehouse), `StockMovements` (append-only)
**Purchasing:** `Suppliers`, `PurchaseOrders`(+Items), `PurchaseInvoices`(+Items), `PurchaseReturns`(+Items)
**Sales:** `Customers`, `SalesInvoices`(+Items), `Payments`, `SalesReturns`(+Items)
**Sequences:** `DocumentSequences` (per-tenant document numbering)

**Migrations:** `InitialCreate` → `AddIdentity` → `AddCatalog` → `AddInventory` → `AddPurchasing` →
`AddSales` (no model drift).

**Purchasing notes:** creating a purchase invoice is one atomic transaction — number + invoice +
items + inbound stock (via `IStockLedger`, WAC recompute) + supplier balance. A purchase return
reverses stock and balance and guards against over-return (ReturnedQty ≤ Quantity). Purchase orders
(Draft→Confirmed→Received→Cancelled) are a greenfield addition (not in the docs). Document numbers
come from `IDocumentNumberService` (atomic per-tenant sequence).

**Sales notes:** the mirror of purchasing. A sales invoice issues stock outbound (rejected if it
would go negative), snapshots each line's cost from the current WAC into `SalesInvoiceItem.UnitCost`
(for profit reporting), and increases the customer's receivable — one transaction. `CustomerId` is
optional (cash sale). Payments reduce the customer balance (and the invoice's paid amount when
linked). Sales returns restock inbound at the original cost, reduce the customer balance, and guard
over-return. All stock changes go through `IStockLedger`.

**Inventory notes:** stock changes flow through `IStockLedger`, which recomputes weighted-average
cost (inbound), records an append-only movement, and keeps `Product.CostPrice` in sync. Append-only
is enforced by the persistence interceptor (any UPDATE/DELETE of an `IAppendOnly` row throws).
Warehouses + per-warehouse stock + transfers are a deliberate, documented extension beyond the
architecture docs' Tenant-only / one-row-per-product model (per explicit Phase 8 requirements).

---

## API surface (v1)

- **Auth:** `POST /auth/login`, `/auth/refresh`, `/auth/logout`
- **Administration:** `/users` (+ activate/deactivate), `/roles` (+ `/{id}/permissions`),
  `/permissions`, `/tenant/settings`, `/profile` (+ change-password)
- **Catalog:** `/categories`, `/units`, `/brands`, `/products` (search + category/brand filters)
- **Inventory:** `/warehouses`, `/stock/balances`, `/stock/movements`, `/stock/adjust`, `/stock/transfer`
- **Purchasing:** `/suppliers`, `/purchase-orders` (+confirm/cancel), `/purchase-invoices`
  (+`/{id}/returns`)
- **Sales:** `/customers`, `/sales-invoices` (+`/{id}/returns`), `/payments`
- **Reporting:** `/dashboard/summary`, `/reports/sales`, `/reports/low-stock`, `/reports/products`
- **Ops:** `/health` (anonymous DB-reachability probe)

Every non-auth, non-profile endpoint is guarded by a `catalog.*` / `inventory.*` / `purchasing.*` /
`sales.*` / `reports.*` / `users.*` / `roles.*` / `settings.*` permission. The Owner role (seeded per
tenant) holds every permission.

**Production readiness:** `/health` DB check; configurable CORS whitelist (deny cross-origin by
default); opt-in global rate limiter (`RateLimiting:Enabled`); `appsettings.Production.json` with
secrets sourced from environment variables and Swagger disabled. Full OWASP Top-10 + authorization +
tenant-isolation review in [`SECURITY.md`](SECURITY.md); config/migrations/backup/monitoring in
[`DEPLOYMENT.md`](DEPLOYMENT.md).

---

## Quality gate

- **Build:** 0 warnings / 0 errors (warnings-as-errors).
- **Tests:** 119 integration tests passing (auth, administration, catalog, inventory, purchasing,
  sales, reporting, health) — CRUD, tenant isolation, authorization, validation, plus WAC math,
  append-only enforcement, the atomic purchase/sales → stock/balance flows, report aggregation, a
  consolidated end-to-end smoke test that walks login → Product → Customer → Purchase Invoice →
  Sales Invoice → Dashboard over the real HTTP pipeline, and the dev-seeder (default Owner login works
  out of the box + idempotency).
- **Migrations:** verified, no pending model changes (32 `DbSet`s ↔ 32 `CreateTable` calls).

### Final verification (2026-07-16)

| Check | Result |
|-------|--------|
| Solution projects | **9** (6 source + 3 test), all present in `SmartApp.sln` |
| `dotnet restore` / `build` | Succeeded — **0 warnings / 0 errors** |
| `dotnet test` | **117 / 117 passed**, 0 failed, 0 skipped |
| EF Core migrations | **6**, enumerated by `dotnet ef migrations list`; applied via `EnsureCreated`/tests |
| API host boot (Kestrel) | Started, `Now listening on: http://localhost:5101` |
| Swagger | `GET /swagger/v1/swagger.json` → **200** (full OpenAPI document generated) |
| Frontend `npm install` | **0 vulnerabilities** |
| Frontend `npm run build` | Succeeded — strict `tsc` + Vite, **112 modules, 0 errors** |
| Frontend `npm run dev` | Vite up, `GET http://localhost:5173/` → **200** (React `#root` served) |
| End-to-end business chain | login → Product → Customer → Purchase Invoice → Sales Invoice → Dashboard — **verified over HTTP** (sales 80, purchases 60, gross profit 56, A/R 80, A/P 60) |

> Note: the local SQL Server LocalDB instance would not start in this environment, so the DB-backed
> flows were verified against the identical application stack using the in-memory SQLite test host
> (real `Program`, middleware, controllers, auth, EF Core). The live Kestrel run confirmed startup +
> Swagger; its `/health` correctly reports unhealthy without a database (health = DB reachability).

---

## Frontend (Phase 13)

A **React + Vite + TypeScript** single-page app in [`frontend/`](frontend/README.md) consumes the
API. Chosen for the cleanest fit with a JWT REST backend, the largest ecosystem/maintainability, and
type safety mirroring the backend DTOs.

- **Auth:** login; JWT access+refresh with a single-flight **refresh-on-401** interceptor; logout;
  session restored from `/profile`.
- **Authorization:** sidebar menu and in-page actions **filtered by the user's permissions**,
  mirroring the backend `[HasPermission]` policies.
- **Layout:** responsive sidebar + header + user menu; RTL Arabic; light/dark theme.
- **18 pages:** Dashboard, Reports, Profile; Administration (Users, Roles + permission assignment,
  Permissions, Tenant Settings); Catalog (Products, Categories, Units, Brands); Inventory
  (Warehouses, Stock); Purchasing (Suppliers, Purchase Invoices); Sales (Customers, Sales Invoices).
- **Quality:** clean structure (components / typed API clients / hooks); unified error handling;
  loading states; reusable data table + modal; **`npm run build` passes** (strict `tsc` + Vite,
  0 errors, 112 modules).

---

## How to run

```bash
# 0) Create the database schema
dotnet ef database update --project src/SmartApp.Persistence --startup-project src/SmartApp.API

# Backend
dotnet build SmartApp.sln
dotnet run --project src/SmartApp.API      # Swagger: http://localhost:<port>/swagger

# Frontend (separate terminal)
cd frontend && npm install && npm run dev  # http://localhost:5173 (proxies /api to the backend)
```

**Default development login.** On first startup in the Development environment, if the database has
no users, the app auto-seeds a default tenant (`DEMO`) + Owner role (all permissions) + Owner user so
you can log in immediately — from Swagger or the SPA:

| Field | Value |
|-------|-------|
| Email | `admin@smartapp.local` |
| Password | `Admin@123456` |

This is gated by `Seed:DevData` (on in `appsettings.Development.json`, off in production), idempotent
(a no-op if any user already exists — never overwrites data), and configurable via
`Seed:OwnerEmail` / `Seed:OwnerPassword` / `Seed:TenantName` / `Seed:TenantCode`. See
[`DevDataSeeder`](src/SmartApp.Persistence/Seeding/DevDataSeeder.cs).

SQL Server connection string and JWT signing key come from user-secrets / environment variables
(empty in `appsettings.json`). A dev-only signing key + LocalDB string live in
`appsettings.Development.json`. The permission catalog is seeded at startup (idempotent).
