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
| 10 | **Sales** (Customers, Sales Invoices, Payments, Returns, cost-of-sale snapshot) | ✅ |
| 11 | Dashboard & Reports API | ⏳ next |
| 12 | Production Readiness (security/perf review, deployment config) | ⏳ |
| 13 | Frontend Application | ⏳ |

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

Every non-auth, non-profile endpoint is guarded by a `catalog.*` / `inventory.*` / `purchasing.*` /
`sales.*` / `users.*` / `roles.*` / `settings.*` permission. The Owner role (seeded per tenant) holds
every permission.

---

## Quality gate

- **Build:** 0 warnings / 0 errors (warnings-as-errors).
- **Tests:** 108 integration tests passing (auth, administration, catalog, inventory, purchasing,
  sales) — CRUD, tenant isolation, authorization, validation, plus WAC math, append-only enforcement,
  and the atomic purchase-invoice / sales-invoice → stock/balance flows.
- **Migrations:** verified, no pending model changes.

---

## How to run

```bash
dotnet build SmartApp.sln
dotnet run --project src/SmartApp.API
# Swagger: http://localhost:<port>/swagger
```

SQL Server connection string and JWT signing key come from user-secrets / environment variables
(empty in `appsettings.json`). A dev-only signing key + LocalDB string live in
`appsettings.Development.json`. The permission catalog is seeded at startup (idempotent).
