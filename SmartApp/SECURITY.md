# SmartApp — Security Review (Phase 12)

This document records the security posture of the SmartApp backend, an OWASP Top-10-oriented review,
and the authorization + tenant-isolation review. It reflects what is implemented in the codebase.

> SmartApp is a multi-tenant Business Management Platform. It is **not** SaaS: there is no billing,
> subscription, or payment-gateway surface to attack. "Payments" in the domain are commercial debt
> settlements, not card processing.

---

## 1. Authentication

- **JWT Bearer** access tokens (HMAC-SHA256), short-lived (default 15 min). Issuer/audience/lifetime/
  signing-key all validated (`AuthenticationExtensions`). No token accepted without a valid signature.
- **Refresh tokens** are opaque, random, and **never stored in plaintext** — only a SHA-256 hash is
  persisted (`RefreshToken.TokenHash`). Refresh performs **rotation** (old token revoked, new issued)
  with **reuse detection** (presenting a revoked token revokes the whole chain).
- **Passwords** are hashed with **PBKDF2** (ASP.NET Core `PasswordHasher`). Plaintext passwords are
  never stored or logged. Login returns a **generic** error ("invalid email or password") to avoid
  user enumeration.
- **Signing key & connection string are secrets** — empty in `appsettings.json` /
  `appsettings.Production.json`; supplied via environment variables / user-secrets. Never committed.

## 2. Authorization (RBAC)

- **Permission-based**, `resource.action` model, enforced by `[HasPermission("...")]` +
  a dynamic policy provider. Permissions live in the JWT (`permissions` claim) issued at login.
- **Every** business endpoint carries an explicit permission (`catalog.*`, `inventory.*`,
  `purchasing.*`, `sales.*`, `reports.*`, `users.*`, `roles.*`, `settings.*`). The only
  authenticated-but-unpermissioned endpoints are the caller's **own** profile (`/profile`).
- Permissions are a **fixed catalog** seeded at startup; they cannot be created at runtime. The
  per-tenant **Owner** role is granted every permission on provisioning.
- **System-owner** short-circuit is explicit and limited to the tenant-management capability.

## 3. Multi-tenant isolation (reviewed)

- `TenantId` is **stamped server-side** from the authenticated principal's claim — **never** read from
  request bodies or query strings.
- An **EF Core global query filter** scopes **every** read of a `BaseEntity` to the current tenant and
  excludes soft-deleted rows. Non-`BaseEntity` tenant-owned types (`AppUser`, `RefreshToken`,
  `UserRole`, `RolePermission`, `StockMovement`) have **explicit** equivalent filters in
  `AppDbContext`.
- Cross-tenant object access returns **NOT_FOUND**, never leaking existence.
- Isolation is covered by integration tests for **every** module (a resource created by tenant B is
  invisible to tenant A: 404 on get, absent from lists).

## 4. OWASP Top 10 (2021) review

| # | Risk | Status / mitigation |
|---|------|---------------------|
| A01 | Broken Access Control | Permission checks on every endpoint; server-side tenant stamping + global query filter; tested cross-tenant isolation. |
| A02 | Cryptographic Failures | PBKDF2 password hashing; refresh tokens stored only as SHA-256 hashes; JWT HMAC-SHA256; HTTPS redirection enabled; secrets from env. |
| A03 | Injection | EF Core parameterized queries throughout (no string-concatenated SQL); FluentValidation on all commands; `ISJSON` guard on JSON columns. |
| A04 | Insecure Design | Clean Architecture with a single write path for stock (`IStockLedger`); append-only ledger (`StockMovements`) enforced at the persistence interceptor; atomic document transactions. |
| A05 | Security Misconfiguration | Swagger disabled in Production; unified error envelope hides stack traces; CORS whitelist (deny cross-origin by default); rate limiting available; warnings-as-errors build. |
| A06 | Vulnerable Components | Pinned package versions; known-vulnerable packages removed (AutoMapper advisory) during development. Run `dotnet list package --vulnerable` in CI. |
| A07 | Identification & Auth Failures | Short-lived access tokens; refresh rotation + reuse detection; account lockout fields present; generic auth errors; deactivating a user revokes active tokens. |
| A08 | Software & Data Integrity | Append-only financial/audit ledger; optimistic concurrency (`ROWVERSION`) on business rows; server-side document numbering. |
| A09 | Logging & Monitoring | Source-generated structured logging; global exception handler logs unhandled errors (without leaking to clients); `/health` probe. Ship logs to a sink in production (see DEPLOYMENT.md). |
| A10 | SSRF | No server-side fetching of user-supplied URLs; no outbound HTTP from request handling. |

## 5. Data protection

- **Money** is `DECIMAL(18,4)` (never float). **Dates** are `DATETIME2(3)` UTC.
- **Soft delete** for business data; **append-only** for the stock/audit ledger (no update/delete —
  enforced by the interceptor, which throws on any modify/delete of an `IAppendOnly` row).
- **Optimistic concurrency** (`ConcurrencyStamp` ROWVERSION) guards against lost updates, including
  concurrent document-number generation and stock changes.

## 6. Residual items / recommendations

- Add `dotnet list package --vulnerable` and secret-scanning to CI.
- Consider per-user / per-IP rate-limit partitioning (current limiter is a global fixed window).
- Add security headers (HSTS, X-Content-Type-Options, etc.) via middleware or the reverse proxy.
- Row-Level Security (RLS) in SQL Server is **design-ready** (see architecture docs) as defence in
  depth behind the application-level tenant filter.
