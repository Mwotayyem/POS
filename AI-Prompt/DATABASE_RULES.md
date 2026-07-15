# DATABASE_RULES — Smart ERP POS

> Governed by **[MASTER_PROMPT.md](./MASTER_PROMPT.md)**. Target: **SQL Server 2022+ / Azure SQL** via **EF Core 9 (code-first)**. These rules are mandatory for every table, entity, and query. Multi-tenancy and data integrity are non-negotiable.

---

## 1. Mandatory Common Columns (every business table)

Every tenant-scoped business entity **MUST** include:

| Column | Type | Rule |
|---|---|---|
| `Id` | `UNIQUEIDENTIFIER` (GUID, sequential) or `BIGINT IDENTITY` | Primary key. Prefer `UNIQUEIDENTIFIER DEFAULT NEWSEQUENTIALID()` for tenant-safe, non-enumerable keys (anti-IDOR). Be consistent per project. |
| `TenantId` | `UNIQUEIDENTIFIER` | **Required, indexed, part of every query filter.** Never nullable on business tables. |
| `StoreId` | `UNIQUEIDENTIFIER` NULL | Present on store-scoped entities; nullable only for tenant-wide config. |
| `CreatedAtUtc` | `DATETIME2(7)` | UTC, set server-side on insert. |
| `CreatedBy` | `UNIQUEIDENTIFIER`/`NVARCHAR(450)` | User id, set server-side. |
| `ModifiedAtUtc` | `DATETIME2(7)` NULL | UTC, set server-side on update. |
| `ModifiedBy` | `UNIQUEIDENTIFIER`/`NVARCHAR(450)` NULL | User id, set server-side. |
| `IsDeleted` | `BIT` NOT NULL DEFAULT 0 | Soft delete flag. |
| `DeletedAtUtc` | `DATETIME2(7)` NULL | Set when soft-deleted. |
| `DeletedBy` | `UNIQUEIDENTIFIER` NULL | Set when soft-deleted. |
| `ConcurrencyStamp` / `RowVersion` | `ROWVERSION` (`byte[]`, `[Timestamp]`) | Optimistic concurrency. Mandatory on mutable business tables. |

Implement these via a shared `BaseEntity` / `AuditableTenantEntity` abstract class and configure once in a shared EF configuration — never copy-paste column config per entity.

---

## 2. Data Types (strict)

- **Money / prices / costs / totals / balances / tax amounts →** `DECIMAL(18,4)`. **NEVER** `float`, `real`, `double`, or `money`. In C#: `decimal`.
- **Quantities →** `DECIMAL(18,4)` (support fractional units) unless the product is strictly integer-unit (then `DECIMAL(18,4)` still, validated as whole).
- **Percentages / rates →** `DECIMAL(9,6)` (e.g. tax rate 0.150000).
- **Dates/times →** `DATETIME2(7)`, stored in **UTC only**. Never `DATETIME`. Never store local time. Convert to tenant timezone only at the presentation edge.
- **Text →** `NVARCHAR` (Unicode) with explicit, sensible max lengths. Avoid `NVARCHAR(MAX)` unless truly unbounded (notes, JSON). Never `TEXT`/`NTEXT`.
- **Booleans →** `BIT`.
- **Enums →** store as `INT` (or `TINYINT`) with a C# enum; do not store enum names as strings unless there is a documented reason. Map with EF value conversions.
- **Keys / user ids →** `UNIQUEIDENTIFIER` or Identity's `NVARCHAR(450)` consistently.

---

## 3. Multi-Tenant Enforcement (critical)

3.1. **Global Query Filter:** every tenant-scoped entity gets an EF Core global query filter:
```csharp
modelBuilder.Entity<T>().HasQueryFilter(e =>
    e.TenantId == _tenantProvider.TenantId && !e.IsDeleted);
```
Applied centrally (reflection over all `ITenantEntity`), so no entity is ever queried without it.

3.2. `_tenantProvider.TenantId` is resolved from the **authenticated principal / tenant middleware**, injected into the `DbContext`. It is **never** taken from request payloads for ownership decisions.

3.3. **On `SaveChanges`,** override to stamp `TenantId`, audit columns, and `IsDeleted` handling automatically. Reject (throw) any `Added`/`Modified` tenant entity whose `TenantId` does not equal the current tenant.

3.4. `IsDeleted` filter is combined with the tenant filter so soft-deleted rows are invisible by default. Hard-delete paths must be explicitly justified and audited.

3.5. Cross-tenant admin/reporting queries (platform owner only) go through a **separate, explicit, permission-gated** code path that intentionally bypasses the filter — never the default DbContext.

---

## 4. Keys, Constraints & Relationships

4.1. **Every foreign key is explicit** with a configured relationship (`HasOne/WithMany`, `HasForeignKey`). No shadow-guessed relationships for business links.

4.2. FK columns are **indexed**. Choose delete behavior deliberately: business links use `DeleteBehavior.Restrict` / `NoAction` (soft delete handles removal), never blind cascade that could wipe financial history.

4.3. **Unique constraints** enforce business identity **within a tenant**: e.g. product SKU unique per `(TenantId, Sku)`; document number unique per `(TenantId, DocumentType, Number)`. Always include `TenantId` in tenant-scoped unique indexes.

4.4. Use **check constraints** for invariants the DB can guard: non-negative quantities where applicable, valid enum ranges, `GrandTotal = Subtotal + TaxTotal - Discount` where feasible.

4.5. Filtered unique indexes account for soft delete when needed (e.g. `WHERE IsDeleted = 0`).

---

## 5. Indexing

- Index `TenantId` first (it is in every filter), typically as the leading column of composite indexes: `(TenantId, StoreId, <lookup cols>)`.
- Index all FK columns.
- Index columns used in frequent filters/sorts (status, dates, customer/supplier, product) — composite and covering where it pays off.
- For high-volume ledgers (stock movements, customer/supplier ledger), index by `(TenantId, <entity>Id, CreatedAtUtc)` to serve running-balance and history queries.
- Do not over-index write-heavy tables; justify each index. Avoid indexing wide `NVARCHAR(MAX)`.

---

## 6. Transactions & Unit of Work

6.1. Any operation touching **more than one aggregate/table** that must be consistent (sale + stock movement + ledger; return + stock + credit) runs inside **one transaction** via the **Unit of Work**. All-or-nothing.

6.2. Use the appropriate isolation to prevent oversell/overspend under concurrency (e.g. `READ COMMITTED SNAPSHOT` for reads; targeted row locking / `UPDLOCK` or optimistic `RowVersion` retry for stock and credit decrements). Never allow two concurrent sales to both consume the last unit.

6.3. Concurrency conflicts (`DbUpdateConcurrencyException`) are handled deliberately: retry-with-refresh for counters/balances, or surface a clear conflict error to the user. Never swallow silently.

6.4. Long-running/batch operations are chunked and do not hold transactions open across external I/O.

---

## 7. When JSON Is Allowed (and when it is forbidden)

**Allowed** (as `NVARCHAR(MAX)` + EF value conversion / SQL Server JSON):
- Flexible, non-relational, per-tenant configuration blobs (theme settings, feature flags, tenant preferences).
- Sparse metadata / attributes that vary per product type and are not queried relationally.
- Snapshot payloads for audit ("before/after" state).

**Forbidden as JSON:**
- Anything you filter, join, aggregate, or enforce integrity on: money, quantities, prices, FKs, statuses, tenant/store ids, ledger amounts. These are **first-class columns** with types, constraints, and indexes.
- Anything that must participate in a unique/foreign-key/check constraint.
- Line items of financial documents (they are relational rows, not a JSON array).

Rule of thumb: **if you'd ever query it, sum it, or protect it with a constraint, it is a column, not JSON.**

---

## 8. Naming Conventions

- **Tables:** PascalCase, singular entity name — EF entity `Product` → table `Products` (pluralized) is acceptable if consistent project-wide; pick one convention and keep it.
- **Columns:** PascalCase matching entity properties.
- **Primary key:** `Id`.
- **Foreign keys:** `<Entity>Id` (e.g. `CustomerId`, `ProductId`).
- **Indexes:** `IX_<Table>_<Cols>` (e.g. `IX_Products_TenantId_Sku`).
- **Unique:** `UX_<Table>_<Cols>`.
- **Foreign keys:** `FK_<Child>_<Parent>_<Col>`.
- **Check:** `CK_<Table>_<Rule>`.
- **Default:** `DF_<Table>_<Col>`.
- No spaces, no reserved words, no Hungarian notation, no abbreviations that aren't domain-standard (SKU, PO, UOM are fine).

---

## 9. Migrations

- All schema changes go through **EF Core migrations** committed to source control (see **[DEPLOYMENT_RULES.md](./DEPLOYMENT_RULES.md)**). No manual schema drift.
- Migrations are **forward-only and reviewable**; include sensible defaults for new NOT NULL columns to avoid breaking existing rows.
- Never edit an already-shipped migration; add a new one.
- Data migrations (backfills) are explicit, idempotent, and tenant-aware.
- Seed only reference/lookup data (units, default tax profiles, roles/permissions) — never demo/business data.

---

## 10. Query Hygiene

- Prefer projection to DTOs (`Select`) over loading full entities for reads (CQRS read side). Do not `Include` graphs you don't need.
- Reads are `AsNoTracking()` by default; only tracked entities for writes.
- **Never** build SQL by string concatenation. Parameterize everything (EF does this; raw SQL must use parameters — see **[SECURITY_RULES.md](./SECURITY_RULES.md)**).
- Paginate all list endpoints (keyset/offset per **[API_RULES.md](./API_RULES.md)**); never return unbounded result sets.
- Avoid N+1: use projections/`Include` deliberately; measure with logging.

---

## 11. Data Integrity Checklist (per new table)

- [ ] `Id`, `TenantId`, `StoreId?`, full audit columns, `IsDeleted`+delete audit, `ConcurrencyStamp`.
- [ ] Money/qty = `DECIMAL(18,4)`; rates = `DECIMAL(9,6)`; times = UTC `DATETIME2(7)`.
- [ ] Global tenant + soft-delete query filter registered.
- [ ] `SaveChanges` stamps tenant/audit and rejects foreign tenant writes.
- [ ] Explicit FKs, indexed, with deliberate delete behavior.
- [ ] Tenant-scoped unique/check constraints.
- [ ] Indexes lead with `TenantId`; FKs and hot filter columns indexed.
- [ ] EF migration written, reversible-safe, no demo data.
