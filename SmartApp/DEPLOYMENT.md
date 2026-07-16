# SmartApp — Deployment Guide (Phase 12)

Operational guidance for running the SmartApp backend in production.

---

## 1. Prerequisites

- .NET 9 runtime (ASP.NET Core).
- SQL Server (2019+ or Azure SQL).
- A reverse proxy terminating TLS (nginx / IIS / Azure App Service) is recommended.

## 2. Configuration & secrets

Configuration layers: `appsettings.json` → `appsettings.{Environment}.json` → **environment
variables** (highest). Secrets are **never** in source — `appsettings.Production.json` ships with
empty `ConnectionStrings:SmartAppDb` and `Jwt:SigningKey` placeholders that **must** be supplied via
environment variables:

| Setting | Env var | Notes |
|---------|---------|-------|
| DB connection | `ConnectionStrings__SmartAppDb` | SQL Server connection string. |
| JWT signing key | `Jwt__SigningKey` | ≥ 256-bit random secret. Rotate periodically. |
| JWT issuer/audience | `Jwt__Issuer` / `Jwt__Audience` | Match your token consumers. |
| CORS origins | `Cors__AllowedOrigins__0`, `__1`, … | Frontend origin(s). Empty = same-origin only. |
| Rate limiting | `RateLimiting__Enabled` (=`true` in prod), `RateLimiting__PermitLimit`, `RateLimiting__WindowSeconds` | Global fixed window. |

Set `ASPNETCORE_ENVIRONMENT=Production`. In Production, **Swagger is disabled** and the stricter log
levels from `appsettings.Production.json` apply.

## 3. Database & migrations

The schema is EF Core code-first. Apply migrations before starting the app:

```bash
dotnet ef database update \
  --project src/SmartApp.Persistence \
  --startup-project src/SmartApp.API
```

Migration chain: `InitialCreate → AddIdentity → AddCatalog → AddInventory → AddPurchasing → AddSales`.
The **permission catalog** is seeded automatically at startup (idempotent). Provision each tenant's
**Owner** role (all permissions) via the tenant-provisioning path.

## 4. Running

```bash
dotnet run --project src/SmartApp.API      # or: dotnet SmartApp.API.dll after publish
```

Publish for deployment:

```bash
dotnet publish src/SmartApp.API -c Release -o ./publish
```

## 5. Health, logging, monitoring

- **Health probe:** `GET /health` (anonymous) reports **Healthy** only when the database is reachable
  — wire it to your load balancer / orchestrator liveness+readiness checks.
- **Logging:** structured logging via `Microsoft.Extensions.Logging`. In production, forward logs to a
  sink (Application Insights / Seq / ELK). The global exception handler logs unhandled errors and
  returns a generic `INTERNAL_ERROR` envelope (never a stack trace).
- Consider adding request/correlation-id logging (the response envelope already carries a correlation
  id from `TraceIdentifier`).

## 6. Backup & recovery strategy

- **Database:** enable automated backups — **full** (daily), **differential** (every few hours), and
  **transaction-log** (every 5–15 min) to meet a low RPO. On Azure SQL, use point-in-time restore.
- **Test restores** regularly; a backup is only as good as its last verified restore.
- **Retention:** align with business/compliance needs (e.g. 30–90 days rolling + periodic long-term).
- **Secrets:** back up the JWT signing key and connection secrets in a secure vault (Azure Key Vault /
  a secrets manager), separate from the database backups.
- Because the stock/audit ledger is **append-only**, point-in-time restore fully reconstructs
  inventory balances from movement history.

## 7. Performance & indexing (reviewed)

- Every business table has the **tenant isolation** access pattern covered by a filtered index that
  includes `TenantId` and excludes soft-deleted rows.
- Hot lookups are indexed: product SKU / barcode (unique per tenant), stock by (product, warehouse),
  stock movements by (tenant, product, date), invoices by (tenant, number/date/partner).
- Money aggregation for dashboards/reports is bounded by date-range filters.
- Use connection pooling (default) and enable **RCSI** (read-committed snapshot isolation) on the
  database to reduce read/write contention for the atomic document transactions.

## 8. Pre-launch checklist

- [ ] `ASPNETCORE_ENVIRONMENT=Production`, Swagger confirmed off.
- [ ] `Jwt__SigningKey` and `ConnectionStrings__SmartAppDb` set via env, not in source.
- [ ] TLS enforced at the proxy; HTTPS redirection on.
- [ ] `Cors__AllowedOrigins` set to the real frontend origin(s).
- [ ] `RateLimiting__Enabled=true` with tuned limits.
- [ ] Migrations applied; permission catalog seeded; Owner roles provisioned.
- [ ] `/health` green; logs flowing to a sink; backups scheduled and a restore tested.
- [ ] `dotnet list package --vulnerable` clean in CI.
