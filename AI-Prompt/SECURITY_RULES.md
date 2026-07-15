# SECURITY_RULES — Smart ERP POS

> Governed by **[MASTER_PROMPT.md](./MASTER_PROMPT.md)**. Security is the highest priority (above performance and convenience). Treat every input as hostile and every tenant boundary as a hard security perimeter. A tenant data leak is a **Sev-1** incident, not a bug.

---

## 1. OWASP Top 10 — Baseline Defenses

You must actively defend against the OWASP Top 10. Concretely:

1. **Broken Access Control** → deny-by-default authorization, permission checks on every endpoint, tenant + store scoping, no IDOR (§4).
2. **Cryptographic Failures** → TLS everywhere, secrets in a vault, hashed passwords (Identity/PBKDF2/BCrypt), encrypted sensitive data at rest.
3. **Injection** → parameterized queries only, no dynamic SQL, output encoding (§2/§3).
4. **Insecure Design** → these rule files; threat-model each feature; secure defaults.
5. **Security Misconfiguration** → hardened headers, no verbose errors to clients, least-privilege DB user, Swagger locked in prod.
6. **Vulnerable Components** → pin & patch NuGet/JS deps; scan in CI (**[DEPLOYMENT_RULES.md](./DEPLOYMENT_RULES.md)**).
7. **Auth Failures** → strong JWT + refresh rotation, lockout, MFA-ready, no credential leakage (§5).
8. **Data Integrity Failures** → signed tokens, integrity of updates, no unsafe deserialization, idempotency.
9. **Logging/Monitoring Failures** → audit + structured logs with correlation id; alert on anomalies; never log secrets.
10. **SSRF** → validate/whitelist any server-side outbound URL; no fetching arbitrary client-supplied URLs.

---

## 2. Injection (SQLi)

2.1. **All data access is parameterized** through EF Core LINQ or parameterized raw SQL. **Never** concatenate user input into SQL/`FromSqlRaw` strings. Use `FromSqlInterpolated`/parameters only.

2.2. Dynamic filtering/sorting is done via **whitelisted** column/operator maps (**[API_RULES.md](./API_RULES.md)** §6) — never by interpolating client field names into queries.

2.3. No `EXEC(@sql)` dynamic SQL built from input. Stored procedures, if used, take typed parameters.

---

## 3. XSS & Output Encoding

3.1. Razor auto-encodes output — **do not** use `Html.Raw` / `@Html.Raw` on user/tenant data. If raw HTML is unavoidable, sanitize with an allow-list sanitizer first.

3.2. All user-generated content rendered in the UI or via JS is encoded/escaped for its context (HTML, attribute, JS, URL). No building DOM by string-concatenating untrusted data.

3.3. Set a strict **Content-Security-Policy**, plus `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`/`frame-ancestors`, `Referrer-Policy`, and `Strict-Transport-Security`.

3.4. Validate/normalize rich inputs (file names, notes) and store them safely; never trust `Content-Type` from the client for uploads (validate real content + size + extension allow-list; store outside webroot / scan).

---

## 4. Access Control, IDOR & Tenant Isolation

4.1. **Deny by default.** Every endpoint requires authentication and an explicit permission. No "forgot to authorize" endpoints. Use policy/permission attributes, not ad-hoc checks.

4.2. **IDOR prevention:** every resource fetch/update is filtered by the current `TenantId` (and `StoreId`/ownership where relevant) via the global query filter (**[DATABASE_RULES.md](./DATABASE_RULES.md)** §3). Requesting another tenant's/user's id returns `404`/`403` — you can never load a row you don't own even if you guess its id. Prefer non-sequential GUID keys to make ids non-enumerable.

4.3. **`TenantId` is NEVER accepted from the client to determine ownership.** It is resolved server-side from the authenticated principal / tenant middleware and injected into the DbContext. Any request whose target data resolves to a different tenant is rejected and logged as a potential attack.

4.4. On write, `TenantId`/`StoreId` are **stamped server-side**; `SaveChanges` rejects any entity whose `TenantId` ≠ current tenant (**[DATABASE_RULES.md](./DATABASE_RULES.md)** §3.3). No mass-assignment can set/override tenant.

4.5. **Permission-based authorization** (fine-grained: `products.read`, `sales.post`, `returns.approve`, `inventory.adjust`, …), aggregated into roles. Check the permission, not just the role name. Platform-owner/cross-tenant operations are a separate, explicitly-gated privilege.

4.6. **No mass-assignment / over-posting:** bind to request DTOs, not entities; never let a request set `Id`, `TenantId`, `IsDeleted`, prices/totals, audit fields, or permissions it shouldn't.

---

## 5. Authentication (JWT + Refresh Rotation)

5.1. **JWT access tokens** are short-lived (e.g. 5–15 min), signed (strong secret/asymmetric key from the vault), with claims: `sub` (user), `tenant_id`, permissions/roles, `jti`, issuer/audience, expiry. Validate issuer, audience, lifetime, and signature on every request.

5.2. **Refresh tokens** are long-lived, opaque, stored **hashed** server-side, single-use, and **rotated** on every use (rotation with reuse detection: if a used/old refresh token is replayed, revoke the whole token family — likely theft).

5.3. Support token **revocation** (logout, password change, admin revoke). Bind refresh tokens to device/session metadata; allow "log out everywhere".

5.4. Passwords via ASP.NET Core **Identity** (strong hashing, salting). Enforce password policy, **account lockout** on brute force, and be **MFA-ready** (TOTP). Never store or log plaintext passwords; never return password hashes.

5.5. Do not put secrets/PII in the JWT beyond what's needed; the token is readable by the client. Never trust unsigned/`alg:none` tokens.

---

## 6. Rate Limiting & Abuse Protection

6.1. Apply **rate limiting** (ASP.NET Core rate limiter) globally and tighter on sensitive endpoints (login, refresh, password reset, POS post). Exceeding limits → `429` (**[API_RULES.md](./API_RULES.md)**).

6.2. Throttle/limit per IP + per tenant + per user to prevent one tenant/user starving others (noisy-neighbor protection) and to blunt brute force / scraping.

6.3. Guard against enumeration: uniform responses/timing on auth failures ("invalid credentials", not "user not found").

---

## 7. CSRF

7.1. Cookie-based MVC/form flows require **anti-forgery tokens** (`[ValidateAntiForgeryToken]` / automatic per-request token) on every state-changing request; the UI's shared AJAX helper attaches it (**[UI_RULES.md](./UI_RULES.md)** §6).

7.2. Pure token-bearer API calls (Authorization header, no ambient cookie) are not CSRF-susceptible the same way, but ensure auth is via header, `SameSite` cookies are `Lax`/`Strict`, and CORS is a strict allow-list.

---

## 8. Encryption & Secrets

8.1. **TLS 1.2+ everywhere**; HSTS on. No plaintext transport.

8.2. **Secrets are never in source, config files, or logs.** Use a secrets manager / vault (Azure Key Vault, environment-injected secrets) resolved at runtime (**[DEPLOYMENT_RULES.md](./DEPLOYMENT_RULES.md)**). Rotate keys/secrets; support key rotation for JWT signing.

8.3. Encrypt sensitive data at rest (DB TDE and/or column encryption for the most sensitive fields). The app **does not store raw payment card data** — payments go through a PCI-compliant provider/token; store only tokens/last4.

8.4. Least-privilege DB account (no `sa`, only needed rights). Separate credentials per environment.

---

## 9. Auditing & Monitoring

9.1. **Security-relevant events are audited** (immutable): logins/failures, permission changes, role assignments, refresh-token reuse detection, cross-tenant access attempts, price/credit-limit overrides, voids/refunds, data exports, config changes. Include who/when(UTC)/what/tenant/ip/correlationId.

9.2. Alert on anomalies (spike in 401/403, refresh reuse, mass export, off-hours admin actions).

9.3. **Never log** passwords, tokens, secrets, full PII, card data, or another tenant's data. Redact by default (**[API_RULES.md](./API_RULES.md)** §7).

---

## 10. Security Checklist (per feature)

- [ ] Endpoint requires auth + explicit permission; deny-by-default.
- [ ] Tenant/store scoping enforced via global filter; ids non-enumerable; no IDOR.
- [ ] `TenantId` server-resolved, never from client; write-stamp verified in `SaveChanges`.
- [ ] Binds to DTOs; no over-posting of `Id`/tenant/price/audit/permission fields.
- [ ] Parameterized data access; whitelisted filter/sort; no dynamic SQL.
- [ ] Output encoded; no `Html.Raw` on untrusted data; security headers + CSP set.
- [ ] JWT validated; refresh rotated with reuse detection; lockout + rate limiting on auth.
- [ ] CSRF token on cookie/form mutations; CORS allow-list; SameSite cookies.
- [ ] Secrets from vault; TLS/HSTS; no secrets/PII/cross-tenant data in logs.
- [ ] Security-relevant actions audited immutably.
