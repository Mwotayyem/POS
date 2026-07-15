# API_RULES — Smart ERP POS

> Governed by **[MASTER_PROMPT.md](./MASTER_PROMPT.md)**. The API is **ASP.NET Core 9 Web API**, RESTful, versioned, validated, observable, and tenant-safe. Every endpoint obeys these rules and the security rules in **[SECURITY_RULES.md](./SECURITY_RULES.md)**.

---

## 1. REST Design

1.1. Resources are **nouns**, plural, hierarchical:
`GET /api/v1/products`, `GET /api/v1/products/{id}`, `POST /api/v1/products`, `PUT /api/v1/products/{id}`, `PATCH /api/v1/products/{id}`, `DELETE /api/v1/products/{id}` (soft delete).

1.2. Nested resources reflect ownership: `GET /api/v1/invoices/{id}/lines`. Don't nest deeper than ~2 levels; use query filters instead.

1.3. Use HTTP methods correctly: `GET` (safe, idempotent, no side effects), `POST` (create / domain action), `PUT` (full replace), `PATCH` (partial), `DELETE` (soft delete). Domain actions that aren't pure CRUD use a sub-resource verb: `POST /api/v1/invoices/{id}/post`, `POST /api/v1/invoices/{id}/cancel`, `POST /api/v1/returns`.

1.4. Controllers are **thin**: they bind, authorize, and dispatch a MediatR Command/Query, then map the result to the envelope. No business logic in controllers (**[CODING_RULES.md](./CODING_RULES.md)**).

---

## 2. Standard Response Envelope

**Every** JSON response (success and error) uses one consistent envelope:

```json
{
  "success": true,
  "data": { },
  "errors": [],
  "meta": { "page": 1, "pageSize": 20, "totalCount": 137, "totalPages": 7 },
  "correlationId": "0HM... / guid",
  "timestampUtc": "2026-01-01T12:00:00Z"
}
```

- `success`: boolean.
- `data`: payload (object, array, or null). Always a **DTO**, never an EF entity.
- `errors`: array of `{ code, message, field? }`; empty on success.
- `meta`: pagination/aggregate metadata when applicable; otherwise omitted/null.
- `correlationId`: the request correlation id (also in logs + response header) for support.
- `timestampUtc`: server UTC time.

Implement via a shared `ApiResponse<T>` type and an action-result/filter wrapper so no controller hand-builds the shape.

---

## 3. Versioning

3.1. **URL versioning**: `/api/v{major}/...`. Start at `v1`. Breaking changes → new major version; additive changes stay in the current version.

3.2. Use `Asp.Versioning` (API versioning) with version discovery in Swagger. Support at least the current and previous major during deprecation windows.

3.3. Never break an existing version's contract (field removal/rename/type change) — deprecate and add instead.

---

## 4. Validation

4.1. **FluentValidation** validates every incoming command/DTO via a MediatR **validation pipeline behavior** — before the handler runs. Controllers do not manually validate business input.

4.2. Validation failures return **HTTP 422 (Unprocessable Entity)** (or 400 for malformed requests) with `success:false` and per-field `errors` (`field`, `code`, `message`).

4.3. Validate: required fields, types, ranges, lengths, enum membership, referential existence (tenant-scoped), and cross-field rules. Never rely on client validation; the API is authoritative (**[SECURITY_RULES.md](./SECURITY_RULES.md)**).

4.4. Money/total fields sent by the client are validated/recomputed server-side per **[BUSINESS_RULES.md](./BUSINESS_RULES.md)** — the client can never dictate a price or total.

---

## 5. Exception Handling & ProblemDetails

5.1. A global **exception-handling middleware** catches all unhandled exceptions and converts them into the standard envelope. No raw exception ever reaches the client.

5.2. Errors also conform to **RFC 7807 ProblemDetails** semantics where appropriate (type, title, status, detail, instance, correlationId extension).

5.3. Map exception → status:
- Validation → `422`.
- Not found (tenant-scoped) → `404`.
- Unauthorized (no/invalid auth) → `401`.
- Forbidden (authenticated, lacks permission / cross-tenant) → `403`.
- Concurrency conflict / business conflict → `409`.
- Rate limited → `429`.
- Unhandled/server → `500` (generic message + correlationId; **never** stack trace or internals to client).

5.4. Domain rule violations throw typed domain exceptions (e.g. `InsufficientStockException`, `CreditLimitExceededException`) mapped to `409`/`422` with a stable machine-readable `code`.

---

## 6. Pagination, Filtering, Sorting

6.1. **All list endpoints are paginated.** No unbounded lists ever. Query params: `?page=1&pageSize=20&sort=name:asc&filter=...` with a **max page size cap** (e.g. 100).

6.2. Prefer **keyset/seek pagination** for large, frequently-scrolled datasets; offset pagination acceptable for smaller/back-office lists. Return `meta` totals where feasible.

6.3. Filtering is explicit and whitelisted (allowed fields/operators only) — never let clients inject arbitrary SQL/expressions. Sorting is whitelisted to known columns.

6.4. Defaults are sensible (`page=1`, `pageSize=20`, stable default sort). Invalid paging params are clamped or 422'd, not crashed.

---

## 7. Observability (Serilog + Correlation Id)

7.1. **Serilog** with structured logging. Every request/response is enriched with a **correlation id** (from incoming header `X-Correlation-Id` or generated), plus `TenantId`, `UserId`, route, status, and duration.

7.2. The correlation id is returned in the response header **and** envelope so support can trace a user report to logs.

7.3. Log levels: `Information` for requests/business events, `Warning` for handled anomalies, `Error` for failures, `Fatal` for crashes. **Never log** secrets, tokens, passwords, full card/PII, or another tenant's data (**[SECURITY_RULES.md](./SECURITY_RULES.md)**).

7.4. Emit key business events (sale posted, return processed, stock adjusted) as structured logs/metrics for auditing and dashboards.

---

## 8. Swagger / OpenAPI

8.1. Swagger/OpenAPI is generated for every version, with **JWT bearer auth** configured in the UI, XML doc comments on endpoints/DTOs, example requests/responses, and documented error/status codes.

8.2. Group endpoints by module/tag; mark deprecations; keep it accurate (it's the contract). In production, protect/limit the Swagger UI per **[DEPLOYMENT_RULES.md](./DEPLOYMENT_RULES.md)** / **[SECURITY_RULES.md](./SECURITY_RULES.md)**.

---

## 9. Status Codes (canonical usage)

| Scenario | Code |
|---|---|
| Read success | `200` |
| Create success | `201` (+ `Location` header) |
| Action success, no body | `204` |
| Malformed request | `400` |
| Not authenticated | `401` |
| Authenticated but forbidden / cross-tenant | `403` |
| Resource not found (within tenant) | `404` |
| Conflict / concurrency / business conflict | `409` |
| Validation failed | `422` |
| Rate limited | `429` |
| Server error | `500` |

Cross-tenant access returns `403`/`404` (never confirm existence of another tenant's data).

---

## 10. Idempotency for Critical Operations

10.1. State-changing, non-idempotent operations that must not double-execute (post sale, process refund, receive stock, create payment) accept an **`Idempotency-Key`** header.

10.2. The server records the key + result; a retry with the same key returns the original result instead of re-executing. Keys are tenant-scoped and expire.

10.3. This protects against double clicks, client retries, and network replays. Combine with DB transactions and concurrency control (**[DATABASE_RULES.md](./DATABASE_RULES.md)**).

---

## 11. General API Conventions

- JSON only (camelCase); `Content-Type: application/json`. UTC ISO-8601 datetimes.
- Return DTOs only; **never** serialize EF entities (avoids over-posting, lazy-load leaks, cycles, tenant leakage).
- Requests bind to explicit request DTOs/commands — **never** bind directly to entities (mass-assignment/over-posting risk, **[SECURITY_RULES.md](./SECURITY_RULES.md)**).
- Enforce auth + permission per endpoint (**[SECURITY_RULES.md](./SECURITY_RULES.md)**). Every endpoint is tenant-scoped by default.
- Set caching headers deliberately; most business data is `no-store`.
- CORS is explicit allow-list; no wildcard with credentials.

---

## 12. API Checklist (per endpoint)

- [ ] Versioned route, correct HTTP method, thin controller dispatching MediatR.
- [ ] Binds to a request DTO/command (never an entity); returns a DTO in the envelope.
- [ ] FluentValidation via pipeline behavior; 422 with per-field errors.
- [ ] Auth + permission enforced; tenant-scoped; cross-tenant → 403/404.
- [ ] Lists paginated (capped), filter/sort whitelisted, `meta` populated.
- [ ] Global exception middleware → envelope/ProblemDetails; correct status codes.
- [ ] Serilog with correlation id; no secrets/PII/cross-tenant data logged.
- [ ] Critical writes accept `Idempotency-Key`; run in a transaction.
- [ ] Swagger documented with examples and auth.
