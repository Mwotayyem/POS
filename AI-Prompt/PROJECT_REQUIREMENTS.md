# PROJECT_REQUIREMENTS — Smart ERP POS

> Governed by **[MASTER_PROMPT.md](./MASTER_PROMPT.md)**. The complete functional and non-functional scope of Smart ERP POS, with acceptance criteria. This is the "what". The other rule files are the "how". A feature is done only when it meets its acceptance criteria **and** all rule-file checklists.

---

## 1. Vision

A commercial, multi-tenant, cloud **ERP + POS** SaaS for retail and wholesale businesses (single or multi-store), competing with **Dynamics 365 Business Central**, **SAP Business One**, and **Odoo**. Tenants sign up, configure their business, and run purchasing, inventory, sales, POS, returns, promotions, credit, reporting, and staff permissions — fully isolated from other tenants.

---

## 2. Actors & Roles

- **Platform Owner / SaaS Admin** — manages tenants, plans, platform config (cross-tenant, tightly gated).
- **Tenant Owner/Admin** — configures the tenant, stores, users, roles, tax, theme.
- **Store Manager** — manages a store's inventory, purchasing, staff, reports.
- **Cashier / POS Operator** — runs POS sales, returns, shift open/close.
- **Inventory Clerk** — receipts, adjustments, transfers, stocktakes.
- **Accountant** — customer/supplier ledgers, credit, tax, financial reports.
- **Auditor (read-only)** — read access + audit trail.

Roles are compositions of fine-grained **permissions** (**[SECURITY_RULES.md](./SECURITY_RULES.md)** §4.5); tenants can create custom roles.

---

## 3. Functional Modules

### 3.1 Multi-Tenant & Onboarding
Tenant registration/provisioning, per-tenant config (name, locale, currency, timezone, tax profile, theme/logo), store setup, subscription plan & feature gating.
**Acceptance:** a new tenant is fully isolated; no query/action can read or write another tenant's data; plan gates features.

### 3.2 Authentication & Authorization
Login, JWT + refresh rotation, logout/revoke, password policy + lockout, MFA-ready, roles & fine-grained permissions, per-endpoint enforcement.
**Acceptance:** deny-by-default; every endpoint requires auth + permission; refresh reuse detection revokes the token family; brute force is locked out & rate-limited.

### 3.3 Users & Roles
CRUD users, assign roles, define custom roles from permission catalog, activate/deactivate, per-store assignment.
**Acceptance:** permission changes take effect immediately and are audited; a user cannot escalate their own privileges.

### 3.4 Products / Catalog
CRUD products (SKU, barcode, name, category, unit of measure, tax class, cost, price, reorder level, active), variants/attributes as needed, images, tenant-unique SKU/barcode.
**Acceptance:** SKU/barcode unique per tenant; price/cost `DECIMAL(18,4)`; soft delete; searchable/paginated.

### 3.5 Categories
Hierarchical categories per tenant; assign products; category-level tax/attributes.
**Acceptance:** no cycles; category deletion is soft and handles product reassignment.

### 3.6 Suppliers
CRUD suppliers, contact/terms, supplier ledger (payables), supplier returns/debit notes.
**Acceptance:** tenant-scoped; ledger balances reconcile with receipts/payments/returns.

### 3.7 Customers
CRUD customers, contact, **credit limit & outstanding balance**, customer ledger, store-credit/wallet, block/inactive.
**Acceptance:** credit-limit and non-negative-balance invariants enforced (**[BUSINESS_RULES.md](./BUSINESS_RULES.md)** §4).

### 3.8 Warehouses / Locations
CRUD warehouses/locations per store, default locations, damaged/quarantine locations.
**Acceptance:** stock is tracked per product per location; transfers are two-legged and atomic.

### 3.9 Inventory
On-hand balances (materialized), immutable `StockMovement` ledger, adjustments, transfers, stocktake/reconciliation, valuation (weighted-average default / FIFO), low-stock alerts.
**Acceptance:** every stock change writes a movement in the same transaction; negative stock forbidden by default; balances always reconcile with the ledger (**[BUSINESS_RULES.md](./BUSINESS_RULES.md)** §3).

### 3.10 Purchases
Purchase orders → goods receipt → supplier invoice/payment; receiving increments stock and updates weighted-average cost; PO statuses; supplier returns.
**Acceptance:** receipts freeze cost for valuation; stock and payables update atomically; documents get gap-controlled tenant-scoped numbers.

### 3.11 Sales / Invoicing
Create/post/cancel invoices; lines with frozen price/tax; discounts; posting decrements stock and computes COGS; credit or cash; document numbering; print/PDF.
**Acceptance:** posted invoices immutable; totals recomputed server-side; stock + credit + tax enforced (**[BUSINESS_RULES.md](./BUSINESS_RULES.md)** §1, §4, §7); corrections via return/credit note only.

### 3.12 Returns / Credit Notes
Returns referencing original invoice/line; refund at **original price** even if price changed; quantity clamped to original minus prior returns; stock increment (resalable) or quarantine (damaged); proportional tax reversal; offer claw-back; credit restoration.
**Acceptance:** all of **[BUSINESS_RULES.md](./BUSINESS_RULES.md)** §2 & §5.6 enforced; over-return rejected; refund amount provably equals original.

### 3.13 POS
Fast touch-first sales UI, product search/scan, cart, offers, split/multi-tender payments, cash/credit, receipts, **shift/session** (open float, cash in/out, close & reconcile), voids (permission-gated), offline-resilient UX with server-authoritative posting.
**Acceptance:** POS honors all sale/stock/offer/credit rules; no sale authoritative until server-posted; shift reconciliation (expected vs counted) works (**[BUSINESS_RULES.md](./BUSINESS_RULES.md)** §8).

### 3.14 Offers / Promotions
Percentage/fixed/BXGY/bundle offers; scope product/category/cart; **auto start/expire by UTC**; usage limits (per-customer/global); stacking/priority; deterministic server-side application; return-driven recalculation & claw-back.
**Acceptance:** expired/not-started offers never apply; server recomputes and validates client-claimed discounts; usage limits hold under concurrency (**[BUSINESS_RULES.md](./BUSINESS_RULES.md)** §5).

### 3.15 Reports
Sales, returns, inventory valuation & movement, purchases, customer/supplier ledgers, credit exposure, tax, profit/COGS, best/slow sellers, cashier/shift reports — all filterable by date/store/category, paginated, exportable, permission-gated.
**Acceptance:** reports read from dedicated read/query endpoints (CQRS read side), tenant/store-scoped, never expose cross-tenant data.

### 3.16 Dashboard
Role-aware KPIs & charts (sales trend, top products, stock alerts, returns rate, credit exposure, cash position) via Chart.js from report endpoints.
**Acceptance:** loads fast, handles empty/loading/error states, respects tenant theme/RTL (**[UI_RULES.md](./UI_RULES.md)**).

### 3.17 Notifications
Real-time (SignalR) + persisted notifications: low stock, offer expiry, credit-limit breach attempts, large refunds, shift events; per-user/role targeting; read/unread.
**Acceptance:** tenant-scoped delivery; no cross-tenant leakage over SignalR; persisted for later viewing.

### 3.18 Settings
Tenant/store settings: currency, locale, timezone, tax mode/rates, rounding, costing method, numbering formats, theme/logo, feature flags, backorder policy.
**Acceptance:** settings are data-driven and applied everywhere (money/tax/stock/UI); changing a setting doesn't retro-alter posted documents.

---

## 4. Non-Functional Requirements

### 4.1 Performance
- Typical API reads < 300 ms P95 under normal load; list endpoints paginated & indexed.
- POS operations feel instant (sub-second post under normal conditions).
- Reports/dashboards use projected read models and appropriate indexes; no N+1; no unbounded queries.

### 4.2 Security
- Full **[SECURITY_RULES.md](./SECURITY_RULES.md)** compliance: OWASP Top 10 defended, JWT + refresh rotation, permission-based authz, tenant isolation, no IDOR/over-posting, parameterized data access, secrets in vault, TLS/HSTS, audit trail, rate limiting.
- No payment card data stored (tokenized provider).

### 4.3 Scalability
- Stateless app instances, horizontally scalable; state in DB/cache/token.
- Shared-DB multi-tenant model that scales to many tenants/stores; hot tables indexed and partition-ready.
- Background/queue work is idempotent and instance-safe.

### 4.4 Availability & Reliability
- Zero-downtime deploys (rolling/blue-green/canary), health checks gating traffic (**[DEPLOYMENT_RULES.md](./DEPLOYMENT_RULES.md)**).
- Transactional integrity for all money/stock/credit operations (all-or-nothing).
- Tested backups + point-in-time restore; graceful degradation and clear error handling.

### 4.5 Maintainability
- Clean Architecture + CQRS + Repo/UoW, SOLID, DRY, folder-by-feature, consistent patterns (**[CODING_RULES.md](./CODING_RULES.md)**).
- High-value automated tests (domain rules, handlers, validators, endpoints, tenant-isolation); CI enforces quality gates.
- Structured logging + correlation ids + Swagger; documented assumptions.

### 4.6 Usability & Accessibility
- Responsive, RTL/LTR bilingual (Arabic/English) UI, tenant-themed, WCAG 2.1 AA, consistent components, real loading/empty/error states (**[UI_RULES.md](./UI_RULES.md)**).

### 4.7 Observability
- Centralized structured logs, metrics, tracing, alerting; every request correlatable end-to-end; key business events emitted.

### 4.8 Compliance & Data
- Per-tenant data isolation and, where required, data export/delete for a tenant's own data; auditable financial records retained immutably; tax reporting support.

---

## 5. Global Acceptance Criteria (apply to every feature)

A feature is accepted only if:

- [ ] It is a **full vertical slice** (**[CODING_RULES.md](./CODING_RULES.md)** §9) — entity → DTO → validation → handler → persistence + migration → API → UI → security → logging → exception handling → tests → docs.
- [ ] It is **tenant/store-safe** end-to-end (server-resolved `TenantId`, global filter, no IDOR, no cross-tenant leak).
- [ ] It honors all relevant **[BUSINESS_RULES.md](./BUSINESS_RULES.md)** invariants (money frozen on documents, stock ledgered & non-negative, credit limit, offers auto-expire, returns reference originals & clamp & claw back).
- [ ] Money is `DECIMAL(18,4)`, times are UTC `DATETIME2`, audit/soft-delete/concurrency present (**[DATABASE_RULES.md](./DATABASE_RULES.md)**).
- [ ] API uses the standard envelope, versioning, validation, pagination, correct status codes, correlation id, idempotency where critical (**[API_RULES.md](./API_RULES.md)**).
- [ ] UI is responsive, RTL/LTR, themed, accessible, with loading/empty/error states (**[UI_RULES.md](./UI_RULES.md)**).
- [ ] Security checklist passes (**[SECURITY_RULES.md](./SECURITY_RULES.md)** §10).
- [ ] Deploys cleanly with backward-compatible migrations and health checks (**[DEPLOYMENT_RULES.md](./DEPLOYMENT_RULES.md)**).
- [ ] Assumptions for any ambiguity are stated (**[AI_RULES.md](./AI_RULES.md)**).

---

## 6. Out of Scope (v1, unless requested)
Manufacturing/BOM, full double-entry general ledger/financial statements, payroll/HR, e-commerce storefront, and native mobile apps are **not** in the initial scope. If a request implies these, flag it and propose phasing (**[AI_RULES.md](./AI_RULES.md)** §2).
