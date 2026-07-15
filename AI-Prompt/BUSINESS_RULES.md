# BUSINESS_RULES — Smart ERP POS

> Governed by **[MASTER_PROMPT.md](./MASTER_PROMPT.md)**. These are hard domain invariants. They are enforced in the **Domain/Application layer** (not only in the UI). Violating any of these is a correctness/data-integrity defect. When a rule is not covered here, propose the industry-best default (Dynamics 365 BC / SAP B1 / Odoo behavior) per **[AI_RULES.md](./AI_RULES.md)**.

---

## 0. Enforcement Principles

- Every rule below is enforced in **business logic (Command/Query handlers + domain entities/services)**, never trusted from the client.
- All money math uses `decimal` in code and `DECIMAL(18,4)` in the DB. Never `float`/`double`. Round only at defined boundaries (see §7).
- Every state-changing rule runs inside a **Unit of Work transaction** (see **[DATABASE_RULES.md](./DATABASE_RULES.md)** §Transactions). Partial commits are forbidden.
- Every quantity/amount that changes stock or ledger balances must produce an **immutable movement/audit record**.
- All amounts, stock, and balances are scoped by `TenantId` (and `StoreId` where applicable). No rule may read or write across tenants.

---

## 1. Sales & Invoicing

1.1. A sale (invoice) captures, per line: `ProductId`, quantity, **unit price at time of sale**, line discount, tax rate applied, and computed line total. The historical unit price is **frozen** on the line — later price changes never alter an existing invoice.

1.2. Invoice totals are **derived and recomputed server-side** from lines: `Subtotal = Σ(line net)`, `TaxTotal = Σ(line tax)`, `GrandTotal = Subtotal + TaxTotal − InvoiceDiscount`. Client-supplied totals are ignored/validated, never trusted.

1.3. A confirmed/posted invoice is **immutable**. Corrections happen via credit note / return / cancellation, never by editing a posted invoice.

1.4. An invoice cannot be posted with zero lines, non-positive quantities, or a product not sellable in the invoice's store/tenant.

1.5. Every posted sale **decrements inventory** for each stocked line (§3) atomically within the same transaction. If stock cannot be decremented (§3.4), the whole sale fails.

---

## 2. Returns / Credit Notes

2.1. **A return MUST reference its original invoice** (and original invoice line). Blind returns without an original reference are not allowed unless the tenant explicitly enables "open returns" in settings (then treated as an exception, permission-gated, and flagged).

2.2. **The refund amount equals the ORIGINAL sale price of the returned item — even if the current price has since changed.** The return reads the frozen unit price and tax from the original invoice line, not the live product price.

2.3. **Returned quantity per line can never exceed** the originally sold quantity minus already-returned quantity for that original line. Over-return is rejected.

2.4. A return **increments inventory** back for stocked, resalable items (§3), atomically, and records a stock movement of type `Return`. Non-resalable/damaged returns adjust to a separate damaged/quarantine location, not sellable stock.

2.5. A return **reverses the tax** proportionally to the returned amount, using the original tax rate from the invoice line (not the current tax rate).

2.6. Returns **reduce the net contribution of any offer/promotion** that the original sale counted toward — see §5.6.

2.7. A return that produces a refund to a credit customer **restores that customer's available credit** by the refunded amount (§4).

---

## 3. Inventory / Stock

3.1. Stock is tracked per `ProductId` per `WarehouseId`/location within a store/tenant. Quantity on hand is a **materialized balance** kept consistent with the movement ledger.

3.2. **Every stock change writes an immutable `StockMovement` record**: type (`Purchase`, `Sale`, `Return`, `Adjustment`, `Transfer`, `Wastage`), quantity delta (signed), source document reference, resulting balance, `TenantId`, `StoreId`, user, and UTC timestamp. The balance is updated in the **same transaction** as the movement.

3.3. Stock is updated **immediately after every transaction that affects it** (purchase receipt, sale, return, adjustment, transfer). No deferred/nightly reconciliation is the source of truth; the ledger is.

3.4. **Negative on-hand stock is forbidden by default.** A sale/transfer/adjustment that would drive on-hand below zero is rejected with a clear "insufficient stock" error — unless the tenant explicitly enables backorder/negative-stock for a product category (then it is allowed, flagged, and reported).

3.5. Stock transfers between warehouses/stores are two-legged (decrement source, increment destination) inside one transaction; both legs succeed or both fail.

3.6. Reorder points: when on-hand crosses the product's reorder level, raise a low-stock notification (SignalR + notification record). This is advisory and never blocks sales by itself.

3.7. Inventory valuation uses the tenant's configured costing method (default **Weighted Average Cost**; FIFO if configured). Cost is recomputed on receipts and read for COGS on sales. Never mix methods within a product's history.

---

## 4. Customer Credit Management

4.1. A customer may have a **credit limit** and a running **outstanding balance** (both `DECIMAL(18,4)`, tenant-scoped).

4.2. **A credit sale is rejected if it would push the customer's outstanding balance above their credit limit** (`outstanding + saleTotal > creditLimit`). This check runs server-side inside the sale transaction; the limit is re-read fresh, not cached from the client.

4.3. Payments reduce outstanding balance; credit sales increase it; refunds/credit notes decrease it. Every change writes a customer ledger entry (immutable).

4.4. **Customer available balance/credit must never go negative in an invalid way**: available credit = `creditLimit − outstanding` and cannot be spent below zero. Store credit / wallet balances likewise cannot be overspent.

4.5. Blocked/inactive customers cannot transact on credit. Cash sales may still be allowed per tenant policy.

---

## 5. Offers / Promotions

5.1. An offer defines: scope (product/category/cart), type (percentage, fixed amount, buy-X-get-Y, bundle), value, **start & end datetime (UTC)**, usage limits (per-customer / total), tenant/store applicability, and priority/stacking rules.

5.2. **Offers expire automatically.** Any offer whose `EndAtUtc < now` is inactive with no manual action. Expiry is evaluated at transaction time against server UTC time — never against client time, never requiring a batch job to "turn it off".

5.3. Offers not yet started (`StartAtUtc > now`) are equally inactive. Only currently-active offers apply.

5.4. Offer application is deterministic and **recomputed server-side** at posting time. The client's claimed discount is validated against a fresh server evaluation; mismatches reject or re-price per policy.

5.5. Stacking: by default offers do **not** stack unless explicitly marked stackable; when multiple apply, resolve by priority then best-value-for-customer, deterministically.

5.6. **Returns reduce offer results/qualification.** If an offer's benefit depended on a threshold (e.g., spend ≥ X, buy N units) and a return drops the sale below that threshold, the offer benefit is recalculated/clawed back, and offer usage counters are decremented so the customer/tenant limits stay accurate.

5.7. Usage limits are enforced atomically: a per-customer or global cap cannot be exceeded even under concurrent transactions (use row-level concurrency / counters within the transaction).

---

## 6. Purchasing

6.1. A purchase order references a supplier (tenant-scoped) and lines with expected quantity and cost. Receiving a purchase (goods receipt) increments stock (§3.2) and updates weighted-average cost.

6.2. Purchase prices/costs are frozen on the receipt line for valuation history; later cost changes do not rewrite past receipts.

6.3. Supplier returns/debit notes mirror customer returns: reference the original receipt, decrement stock, and adjust payables.

6.4. Payables to suppliers are tracked in a supplier ledger analogous to §4.

---

## 7. Tax & Rounding

7.1. Tax is computed per line using the applicable tax rate (product/category/tenant tax profile) at transaction time and **frozen** on the line. Tax rate changes never retro-alter posted documents.

7.2. Tax mode (inclusive vs exclusive) is a tenant/store setting and applied consistently. Document the mode on the document.

7.3. Rounding: perform decimal math at full precision; round **only** at the final display/line-total boundary using the tenant's configured rounding (default: round-half-away-from-zero to the currency's minor unit, typically 2 dp for display while storing 4 dp). Never accumulate rounding error across lines.

7.4. `Subtotal`, `TaxTotal`, and `GrandTotal` must reconcile exactly (`Subtotal + TaxTotal − Discount == GrandTotal`) after rounding.

---

## 8. POS-Specific Rules

8.1. POS sales obey all sale rules (§1), stock rules (§3), offer rules (§5), and credit rules (§4) — the POS is not a shortcut around domain invariants.

8.2. A POS shift/session tracks opening float, cash in/out, and expected vs counted cash at close. Sales are attributed to the open session, cashier, and store.

8.3. POS must handle intermittent connectivity gracefully at the UX level, but **no sale is authoritative until posted to the server** and passes all server-side checks. The server is the source of truth for stock, price, and credit.

8.4. Voids/no-sales are permission-gated and audited; a voided POS sale reverses stock exactly like a return.

---

## 9. Documents, Numbering & Audit

9.1. All business documents (invoice, return, PO, receipt, adjustment, transfer) get a **sequential, tenant-scoped, gap-controlled number** generated server-side (no client numbering, no duplicates under concurrency).

9.2. Nothing is hard-deleted. Business documents use soft delete / status transitions (`Draft → Posted → Cancelled`), never row deletion. Posted financial documents are cancelled/reversed, not erased.

9.3. Every create/update/status-change on a business document writes to the audit trail: who, when (UTC), what changed (before/after), tenant, store.

---

## 10. Consistency Guarantees (checklist for any transactional feature)

- [ ] Runs in one Unit of Work transaction; all-or-nothing.
- [ ] Reads authoritative values (price/stock/credit/offer) fresh from server, ignores client-supplied money/totals.
- [ ] Writes an immutable movement/ledger/audit record for every balance change.
- [ ] Enforces non-negative stock and credit-limit invariants.
- [ ] Freezes historical prices/tax on document lines.
- [ ] Links returns to originals and clamps quantities/amounts to originals.
- [ ] Recalculates and, if needed, claws back offer benefits on return.
- [ ] Is fully tenant/store scoped end-to-end.
