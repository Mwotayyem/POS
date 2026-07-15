# DEPLOYMENT_RULES — Smart ERP POS

> Governed by **[MASTER_PROMPT.md](./MASTER_PROMPT.md)**. Code you generate must deploy cleanly through CI/CD to multiple environments with zero-downtime and safe rollback. Assume containers, migrations-as-code, health checks, and vault-managed secrets.

---

## 1. Environments

1.1. At least four environments, promoted in order: **Local → Development → Staging → Production**. Staging mirrors production (same infra shape, sanitized data).

1.2. **Configuration per environment** via `appsettings.{Environment}.json` + environment variables + vault. No hard-coded environment values in code. `ASPNETCORE_ENVIRONMENT` selects config.

1.3. **No secrets in any `appsettings*.json` in source control.** Only non-secret defaults there; secrets are injected at runtime (§8). Never commit connection strings, JWT keys, API keys.

1.4. Production disables developer conveniences: no detailed error pages, Swagger UI restricted/authenticated or off, verbose logging off (structured logs shipped to a sink), dev CORS origins removed.

---

## 2. CI/CD Pipeline

2.1. Every commit/PR runs CI: **restore → build (warnings-as-errors where configured) → unit tests → integration tests → static analysis / linters → dependency & container vulnerability scan → produce artifact/image**. A red pipeline blocks merge.

2.2. **CD** deploys the built artifact/image (not a re-build) through environments with approvals for Staging→Production. The same immutable image promoted across environments; only config differs.

2.3. Pipeline gates: tests green, coverage threshold met, no high/critical vulnerabilities, migrations validated (§3), health check passes post-deploy (§5).

2.4. Every deploy is traceable: version/tag, git SHA, changelog, who/when. Tag releases; keep a deployment log.

---

## 3. EF Core Migrations

3.1. Schema changes ship **only** via EF Core migrations committed to source (**[DATABASE_RULES.md](./DATABASE_RULES.md)** §9). No manual DB edits, no drift.

3.2. Migrations are applied in a **controlled step** (migration job / `dotnet ef database update` / bundled migrations executable), **not** silently on app startup in production (avoids race conditions across multiple instances and uncontrolled schema changes). Run the migration step before/around the rollout as one gated action.

3.3. Migrations must be **backward-compatible for zero-downtime** (expand/contract pattern):
- **Expand:** add nullable columns/new tables first; deploy code that writes both old & new.
- **Migrate:** backfill data (idempotent, tenant-aware, batched).
- **Contract:** remove old columns only after all instances use the new schema, in a later release.
Never do a breaking schema change and a code change that requires it in a single non-compatible step during a live rollout.

3.4. Migrations are reviewed like code; destructive operations (drop/rename) require explicit sign-off and a rollback plan. Backfills are idempotent and re-runnable.

---

## 4. Containerization (Docker)

4.1. The app ships as a **Docker image**: multi-stage build (SDK build stage → runtime stage on the .NET 9 ASP.NET runtime base), non-root user, minimal image, no secrets baked in.

4.2. Images are versioned/tagged (semantic version + git SHA), pushed to a registry, and scanned for vulnerabilities in CI. `latest` is not deployed to production.

4.3. Configuration comes from environment variables / mounted secrets at runtime, not from the image. Externalize logs (stdout → log collector), data, and state.

4.4. Orchestrated (e.g. Kubernetes/App Service): define resource requests/limits, readiness/liveness probes (§5), horizontal scaling, and rolling updates.

---

## 5. Health Checks

5.1. Expose **health endpoints**: `/health/live` (process up) and `/health/ready` (dependencies OK: database, cache, message/queue, critical externals). Use ASP.NET Core HealthChecks.

5.2. Orchestrator **readiness** gates traffic: an instance receives requests only after `/health/ready` passes (DB reachable, migrations compatible, warm). **Liveness** restarts a hung instance.

5.3. Health endpoints don't leak internals to the public and don't require heavy work. Deploys verify health before shifting traffic (§6).

---

## 6. Zero-Downtime Deployment

6.1. Use **rolling / blue-green / canary** deployment: new instances come up, pass readiness, then traffic shifts; old instances drain in-flight requests before termination (graceful shutdown honoring `CancellationToken`).

6.2. Combined with expand/contract migrations (§3.3), old and new app versions run simultaneously against a compatible schema during rollout without errors.

6.3. Sessions are not sticky-dependent (stateless app; state in DB/cache/token), so instances are interchangeable and scalable.

6.4. Long-running work is in resilient background services/queues that tolerate instance restarts (at-least-once + idempotency, **[API_RULES.md](./API_RULES.md)** §10).

---

## 7. Rollback

7.1. Every deploy has a **rollback plan**. Because images are immutable and promoted, rollback = redeploy the previous known-good image/tag.

7.2. **Schema rollback** is handled by forward-compatible design (expand/contract) so the previous app version still works against the current schema. Prefer roll-forward fixes over destructive down-migrations; never run a destructive down-migration against production data without a verified backup.

7.3. **Backups & restore are tested**: automated DB backups + point-in-time restore, periodically rehearsed. A migration that can't be safely reversed requires a backup checkpoint before it runs.

7.4. Feature flags let risky features be disabled without a redeploy.

---

## 8. Configuration & Secrets

8.1. **Secrets from a vault** (Azure Key Vault / managed secret store), injected as environment variables or mounted at runtime, resolved via configuration providers (**[SECURITY_RULES.md](./SECURITY_RULES.md)** §8). Never in source/image/plain config.

8.2. Distinct secrets/credentials per environment; **least-privilege** DB and service accounts. Rotate secrets; support JWT signing-key rotation without downtime.

8.3. Configuration is validated at startup (fail fast if a required setting/secret is missing) — the app refuses to start misconfigured rather than running insecurely.

8.4. Observability wired at deploy: Serilog sinks to centralized logging, metrics, tracing, and alerting (**[API_RULES.md](./API_RULES.md)** §7). Correlation ids flow through so production issues are traceable.

---

## 9. Deployment Checklist (per release)

- [ ] CI green: build, unit + integration tests, coverage, static analysis, dependency/image scan.
- [ ] Same immutable image promoted; only config differs per environment.
- [ ] Migrations are expand/contract, backward-compatible, applied as a gated step (not on startup in prod), backfills idempotent.
- [ ] Secrets from vault; none in source/image/config; config validated at startup.
- [ ] Health `live`/`ready` implemented; readiness gates traffic.
- [ ] Zero-downtime strategy (rolling/blue-green/canary) with graceful drain.
- [ ] Rollback = redeploy prior image; schema forward-compatible; backup taken before destructive changes.
- [ ] Logs/metrics/traces + correlation id shipping to central observability; alerts configured.
- [ ] Release tagged & logged (version, SHA, changelog, approver).
