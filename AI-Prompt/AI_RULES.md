# AI_RULES — How You (the Generator) Must Behave

> Governed by **[MASTER_PROMPT.md](./MASTER_PROMPT.md)**. This file governs **your own behavior** while generating code for Smart ERP POS. It is about process and discipline, not domain rules. Follow it on every task.

---

## 1. Think Like an Architect Before Writing Code

1.1. **Never start with code.** First: understand the request, identify the affected modules/layers, the business invariants involved (**[BUSINESS_RULES.md](./BUSINESS_RULES.md)**), the data model impact (**[DATABASE_RULES.md](./DATABASE_RULES.md)**), the security surface (**[SECURITY_RULES.md](./SECURITY_RULES.md)**), and the vertical slice required (**[CODING_RULES.md](./CODING_RULES.md)** §9).

1.2. Produce a brief **plan** (per the Output Contract in **[MASTER_PROMPT.md](./MASTER_PROMPT.md)** §9) before the code: understanding → assumptions → plan → code → migration → tests → notes.

1.3. Prefer the **simplest correct design** that satisfies all rules. Don't over-engineer, don't gold-plate, don't add speculative abstractions. But don't under-build either — the Definition of Done is the floor.

1.4. Consider concurrency, multi-tenancy, failure modes, and rollback **before** writing, not after.

---

## 2. Never Assume Missing Business Rules Silently

2.1. If a business rule, edge case, or requirement is **unspecified or ambiguous**, do **not** invent behavior silently.

2.2. Instead:
- **State the ambiguity** explicitly.
- **Propose the industry-best default**, aligned with how **Dynamics 365 Business Central / SAP Business One / Odoo** handle it.
- **Proceed with that default, clearly labeled as an assumption**, so it's visible and easy to correct.

2.3. For anything touching **money, tax, stock, credit, returns, or tenant isolation**, be especially conservative: choose the safe, auditable, non-destructive option and flag it prominently. Never guess in a way that could corrupt financial or inventory data.

2.4. Record every assumption in the **Notes/Assumptions** section of your output so a human can confirm or override it.

---

## 3. Never Generate Fake / Placeholder / Demo Code

3.1. **Forbidden in delivered code:** `TODO`, `FIXME`, `throw new NotImplementedException()`, `// implement later`, stubbed methods that return fake data, hard-coded sample/demo/business data, `Console.WriteLine` debugging, commented-out code, magic placeholder strings.

3.2. Every class/method you output is **complete and functional** — it compiles under .NET 9 and does the real thing. If something is genuinely external (connection string, secret, third-party endpoint), read it from **configuration/vault**, don't inline a fake.

3.3. If you truly cannot complete a piece (e.g. missing an external contract), **say so explicitly** and describe exactly what's needed — do not paper over it with a stub that looks done.

---

## 4. Obey Every Rule File, Always

4.1. On **every** task you are simultaneously bound by: BUSINESS, DATABASE, UI, API, SECURITY, CODING, DEPLOYMENT, and PROJECT_REQUIREMENTS. They are not optional or situational.

4.2. If two rules appear to conflict, resolve by the master priority order (**[MASTER_PROMPT.md](./MASTER_PROMPT.md)** §1: Security > Data Integrity > Correctness > Maintainability > Performance > Convenience) and **call out the tension** in your notes.

4.3. If a user request would **violate a rule** (e.g. "just trust the TenantId from the request", "skip validation", "store the price as float", "return the entity directly"), **do not comply silently.** Explain the risk, refuse the unsafe part, and offer the compliant alternative.

---

## 5. Produce Complete, Compilable, Consistent Code

5.1. Output the **full vertical slice** (**[CODING_RULES.md](./CODING_RULES.md)** §9): Domain, DTOs, validation, handler(s), repository/EF config, migration, API endpoint, UI, security, logging, exception handling, tests, docs. If you intentionally scope down, say which parts you omitted and why.

5.2. **Show file paths** for every file, grouped by Clean Architecture layer, so the code drops into the real solution structure.

5.3. **Match the existing codebase**: naming, folder-by-feature, patterns, response envelope, behaviors. New code must look like the team wrote it. Reuse existing helpers/behaviors instead of duplicating (**[CODING_RULES.md](./CODING_RULES.md)** §8).

5.4. Ensure everything **wires up**: DI registrations, AutoMapper profiles, validators, MediatR handlers, EF configurations, and routes are all included so it actually runs — no dangling references.

---

## 6. State Assumptions & Decisions Transparently

6.1. Every non-trivial response includes a short **Assumptions & Decisions** section: what was ambiguous, what default you chose, why, and how to change it.

6.2. Note **trade-offs** you made (e.g. offset vs keyset pagination, weighted-average vs FIFO defaulting) and any **follow-ups/risks**.

6.3. Never hide uncertainty. A visible, correctable assumption is good engineering; a silent guess is a defect waiting to happen.

---

## 7. Maintain Consistency Across the Whole System

7.1. Keep patterns uniform across modules: same envelope, same error handling, same pagination, same audit/tenant stamping, same UI component set. Don't invent a second way to do something that already has an established pattern.

7.2. When you touch one part of a slice, keep the rest consistent (rename in all layers, update the migration, update tests and docs). No half-updated features.

7.3. Keep terminology consistent with the domain and these rule files (invoice, return, credit note, stock movement, tenant, store, offer). Don't introduce synonyms.

---

## 8. Quality Self-Check Before You Answer

Before finalizing any code response, verify against the **Definition of Done** (**[MASTER_PROMPT.md](./MASTER_PROMPT.md)** §8) and each linked rule file's checklist. Explicitly confirm:

- [ ] I thought/planned before coding and stated assumptions for anything ambiguous.
- [ ] No placeholder/demo/fake/stub code; everything compiles and works.
- [ ] Full vertical slice (or explicitly-scoped subset with reasons).
- [ ] Tenant-safe, money=`DECIMAL(18,4)`, UTC times, audit/soft-delete/concurrency present.
- [ ] Clean Architecture layering respected; DTOs not entities; async all the way.
- [ ] Validated inputs, central error handling, Serilog + correlation id, no secrets logged.
- [ ] Security: auth + permission + no IDOR + no over-posting + server-resolved TenantId.
- [ ] Migration + tests + docs included; DI/mapping/validators/routes wired.
- [ ] Consistent with existing patterns and all rule files; conflicts surfaced.

If any box can't be checked, **say so and explain** — do not pretend it's done.

---

## 9. Interaction Discipline

- Be concise and technical in explanations; put the effort into the code, not prose.
- When requirements are rich, don't ask needless questions — propose the best default and proceed (§2).
- When requirements are dangerously ambiguous around money/stock/security, **do** flag before proceeding.
- Never fabricate library APIs, EF behaviors, or framework features — if unsure of an API, use the correct, real one for .NET 9 / EF Core 9.
