# UI_RULES — Smart ERP POS

> Governed by **[MASTER_PROMPT.md](./MASTER_PROMPT.md)**. Server UI is **ASP.NET Core 9 MVC + Razor + Bootstrap 5**. UI must be responsive, accessible, tenant-themed, RTL/LTR-aware, and consume the API per **[API_RULES.md](./API_RULES.md)**. No toy markup — every screen is production-grade.

---

## 1. Framework & Structure

1.1. **Bootstrap 5** is the base design system. Use its grid, utilities, and components; do not hand-roll layout that Bootstrap already provides. No jQuery-UI, no Bootstrap 3/4 patterns.

1.2. JS is vanilla ES6+ or lightweight modules; **DataTables** for grids and **Chart.js** for charts are the sanctioned libraries. Do not add heavy SPA frameworks to the MVC UI. (A separate SPA, if ever built, consumes the same API.)

1.3. Views follow MVC structure: `Views/<Controller>/<Action>.cshtml`, shared layout `_Layout.cshtml`, partials for reusable pieces (`_ProductRow`, `_Pagination`, `_ToastContainer`). **Reusable UI = partial view or tag helper**, never copy-pasted markup.

1.4. All user-facing text is **localized** (resource files / `IStringLocalizer`). No hard-coded UI strings in English or Arabic inline.

---

## 2. Responsive Design

2.1. **Mobile-first.** Every screen works from ~360px up to large desktop. Use responsive grid (`col`, `col-md`, `col-lg`), `flex`, and utility spacing.

2.2. Tables that can't fit small screens use responsive DataTables (horizontal scroll or responsive column collapsing). No fixed pixel-width layouts that break on tablets/phones.

2.3. **POS screens are touch-first**: large tap targets (min 44×44px), keypad-friendly numeric entry, minimal typing, fast product search, works on a tablet at a counter.

2.4. Test breakpoints: xs (phone), md (tablet/POS), lg/xl (back-office desktop).

---

## 3. RTL / LTR & Localization

3.1. **The app is bilingual (Arabic RTL + English LTR) at minimum.** Layout direction is driven by the tenant/user locale: set `dir="rtl"`/`dir="ltr"` and `lang` on `<html>` from culture.

3.2. Use Bootstrap 5 **RTL** stylesheet when direction is RTL. Use logical spacing utilities (`ms-`/`me-`, `ps-`/`pe-`) — never hard-coded left/right (`ml-`/`mr-`) that break under RTL.

3.3. Numbers, currency, and dates are formatted per the **tenant's culture/currency/timezone** (currency symbol, decimal/thousands separators, date format, UTC→tenant-tz conversion). Never show raw UTC or invariant formatting to end users.

3.4. Icons/arrows that imply direction must mirror in RTL. Charts and DataTables must respect direction.

---

## 4. Data Grids (DataTables)

4.1. All list screens use **DataTables** with **server-side processing** for large datasets — paging, sorting, filtering happen on the server via the API (**[API_RULES.md](./API_RULES.md)** §pagination), not by loading everything into the browser.

4.2. Standard grid features: column sort, global + per-column search, page size selector, total count, and export (respecting permissions). Persist user's page-size/sort where reasonable.

4.3. Currency/number/date columns render with tenant formatting; status columns render as badges; action columns render permission-filtered buttons (a user without `delete` never sees a delete button **and** the API still enforces it).

---

## 5. Charts (Chart.js)

5.1. Dashboards use **Chart.js** for KPIs (sales trend, top products, stock levels, returns rate). Data comes from dedicated read/report endpoints — never compute analytics in the browser from raw rows.

5.2. Charts are accessible: provide a title, legend, and an accessible text/table fallback of the same data. Respect tenant theme colors and RTL.

5.3. Charts handle empty/loading/error states explicitly (see §7).

---

## 6. AJAX Patterns

6.1. All AJAX calls hit the versioned API and handle the **standard response envelope** (**[API_RULES.md](./API_RULES.md)** §envelope): read `success`, `data`, `errors`, `correlationId`.

6.2. Every mutating request:
- sends the **anti-forgery token** (CSRF, see **[SECURITY_RULES.md](./SECURITY_RULES.md)**),
- sends the auth token/cookie,
- shows a loading state, disables the submit control to prevent double-submit,
- on success shows a toast + updates UI, on failure shows field-level and/or summary errors.

6.3. Use a **single shared HTTP helper** (wrapper around `fetch`) that: attaches auth + CSRF headers, parses the envelope, centralizes error handling (401 → re-auth, 403 → forbidden toast, 422 → field errors, 5xx → generic error with correlation id), and logs the correlation id for support.

6.4. Idempotency for critical POST actions (post sale, refund): disable button + send idempotency key so a double click can't double-post (**[API_RULES.md](./API_RULES.md)** §idempotency).

---

## 7. Loading, Empty & Error States (mandatory for every data view)

Every screen that loads data must render **all four** states — no blank screens, no infinite spinners:

- **Loading:** spinner/skeleton while fetching; controls disabled.
- **Empty:** friendly "no data yet" with a primary call-to-action (e.g. "Add your first product").
- **Error:** clear message + retry; show the `correlationId` for support; never dump raw stack traces.
- **Success:** the data, correctly formatted and paginated.

Forms show **inline field validation** (mirroring server FluentValidation), a summary of errors, and a clear success confirmation.

---

## 8. Error Handling in the UI

8.1. Map API errors to UX:
- `400/422` validation → highlight fields, show messages next to inputs.
- `401` → redirect to login / silent token refresh then retry.
- `403` → "you don't have permission" (do not reveal existence of forbidden resources beyond what policy allows).
- `409` (concurrency/conflict) → "this record changed, reload".
- `429` → "too many requests, slow down".
- `5xx` → generic apology + `correlationId`.

8.2. **Never** surface server internals, SQL, stack traces, or tenant/other-user data in the UI. **Never** trust client validation alone — it's UX sugar; the server is authoritative.

---

## 9. Tenant Theming

9.1. The layout reads tenant theme settings (primary color, logo, brand name, favicon, accent) and applies them via **CSS custom properties** (`--brand-primary`, etc.) set on `:root` per request. No per-tenant compiled CSS.

9.2. Logo, brand name, and colors come from tenant settings loaded server-side; fall back to a neutral default if unset. Never leak one tenant's branding to another.

9.3. Theming must keep **contrast/accessibility** valid (see §10) regardless of tenant colors — enforce a minimum contrast or auto-adjust text color.

---

## 10. Accessibility (a11y)

10.1. Semantic HTML: proper headings, `<label>` for every input, `<button>` for actions (not clickable `<div>`), landmark regions (`<nav>`, `<main>`).

10.2. **WCAG 2.1 AA**: color contrast ≥ 4.5:1 for text, focus-visible outlines, keyboard operability for all interactive elements (POS included), `aria-*` where needed (live regions for toasts, `aria-busy` while loading).

10.3. Forms: associate errors with fields via `aria-describedby`; announce async results to screen readers via `aria-live`.

10.4. Don't rely on color alone to convey meaning (add icon/text to status badges).

---

## 11. Component Consistency

11.1. Standardize shared components as partials/tag helpers: buttons, modals, toasts, confirmation dialogs, pagination, page headers, breadcrumbs, empty/error blocks, currency/date display. Reuse them everywhere.

11.2. One confirmation pattern for destructive actions, one toast system, one modal system, one form-validation display. No divergent one-off implementations.

11.3. Consistent spacing, typography scale, and iconography across all modules. A user moving from Sales to Inventory should feel the same app.

---

## 12. UI Checklist (per screen)

- [ ] Responsive from phone → desktop; POS is touch-first.
- [ ] RTL/LTR correct; numbers/currency/dates in tenant culture & timezone.
- [ ] Lists use server-side DataTables; charts use Chart.js from report endpoints.
- [ ] Loading / empty / error / success states all present.
- [ ] AJAX uses shared helper: auth + CSRF + envelope + centralized errors + correlation id.
- [ ] Actions permission-filtered in UI **and** enforced by API.
- [ ] Tenant theme applied via CSS vars; contrast/a11y (WCAG AA) preserved.
- [ ] Reuses shared components; text localized; no hard-coded strings.
