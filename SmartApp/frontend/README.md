# SmartApp — Frontend

A React + Vite + TypeScript single-page application for the SmartApp ASP.NET Core API.

## Why React + Vite + TypeScript

- **Best fit for an ASP.NET Core REST API:** the backend is a pure JWT-secured REST service; a
  decoupled SPA consumes it over HTTP without coupling to the .NET runtime, preserving the clean
  separation the backend was built around.
- **Maintainability & longevity:** React has the largest ecosystem and talent pool; Vite gives fast
  dev startup and builds.
- **Type safety:** TypeScript mirrors the backend's strictness — the response envelope and DTOs are
  modeled as types, catching integration errors at compile time.

## Structure

```
src/
├── api/          # Envelope types, axios client (JWT + refresh), typed endpoint modules, models
├── auth/         # AuthContext, token store, permission keys
├── components/   # Reusable UI (DataTable, Modal, Field, PageHeader, ...)
├── layout/       # AppLayout (sidebar + header + user menu), nav config
├── lib/          # useAsync hook, formatters
├── pages/        # Feature pages, grouped by module
├── styles/       # Theme + design system (light/dark, RTL)
├── App.tsx       # Routing (auth-gated shell)
└── main.tsx      # Entry point
```

## Key features

- **Authentication:** login page; JWT access + refresh tokens; a single-flight **refresh-on-401**
  interceptor that retries the original request; logout; session restored from `/profile` on reload.
- **Authorization:** the sidebar menu and in-page actions are **filtered by the user's permissions**
  (`/profile` permissions claim), matching the backend's `[HasPermission]` policies.
- **Layout:** responsive sidebar + header + user menu; RTL Arabic UI; light/dark theme via CSS
  variables.
- **Pages:** Dashboard, Reports, Profile, and CRUD/list screens for Administration (Users, Roles,
  Permissions, Tenant Settings), Catalog (Products, Categories, Units, Brands), Inventory
  (Warehouses, Stock), Purchasing (Suppliers, Purchase Invoices), Sales (Customers, Sales Invoices).
- **UX:** loading and error states everywhere, a unified error path (the API envelope's `error.code`
  / `error.message`), a reusable data table and modal, and typed API clients.

## Getting started

```bash
cd SmartApp/frontend
npm install
npm run dev        # http://localhost:5173 (proxies /api to the backend)
```

Configure the API base URL via `.env` (`VITE_API_BASE_URL`, default `/api/v1`). In development the
Vite proxy forwards `/api` to the backend (`VITE_API_PROXY_TARGET`, default `https://localhost:7000`).

## Build

```bash
npm run build      # type-checks (tsc) then bundles (vite) into dist/
npm run preview    # serve the production build locally
```
