# 12 — API Architecture & Contracts (معمارية الـ API والعقود)

> يشرح تصميم الـ REST API: التغليف الموحّد (Response Envelope)، الإصدار (Versioning)، معالجة الأخطاء (ProblemDetails)، الترقيم، والعقود لكل وحدة. يلتزم بـ [02-Solution-Architecture.md](02-Solution-Architecture.md) و[11-Security-Architecture.md](11-Security-Architecture.md).

---

## 1) مبادئ التصميم (API Principles)

| المبدأ | القرار |
|--------|--------|
| النمط | REST · موارد بالجمع (`/products`) · أفعال HTTP قياسية |
| الإصدار | في المسار: `/api/v1/...` |
| التغليف | Response Envelope موحّد لكل استجابة |
| الأخطاء | `ProblemDetails` (RFC 7807) موحّد |
| الترقيم | `page` + `pageSize` مع بيانات وصفية |
| المصادقة | `Authorization: Bearer <jwt>` |
| المحتوى | JSON فقط (`application/json`) · UTF-8 |
| التوثيق | Swagger/OpenAPI (معطّل في الإنتاج) |
| الوقت | كل التواريخ في الاستجابة **UTC ISO-8601** |

---

## 2) التغليف الموحّد (Response Envelope)

كل استجابة (نجاح أو فشل) بنفس الشكل:

### نجاح (بيانات مفردة)

```json
{
  "success": true,
  "data": { "id": 1042, "name": "حليب المراعي 1ل", "salePrice": 5.5000 },
  "error": null,
  "meta": { "correlationId": "a1b2c3d4", "timestamp": "2026-07-15T10:30:00Z" }
}
```

### نجاح (قائمة مرقّمة)

```json
{
  "success": true,
  "data": {
    "items": [ { "id": 1042, "name": "..." } ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 137,
    "totalPages": 7
  },
  "error": null,
  "meta": { "correlationId": "a1b2c3d4", "timestamp": "2026-07-15T10:30:00Z" }
}
```

### فشل

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "بيانات غير صحيحة",
    "details": [
      { "field": "name", "message": "الاسم مطلوب" },
      { "field": "salePrice", "message": "يجب أن يكون موجباً" }
    ]
  },
  "meta": { "correlationId": "a1b2c3d4", "timestamp": "2026-07-15T10:30:00Z" }
}
```

> يُبنى الـ Envelope من `Result<T>` / `PagedResult<T>` (في `SmartApp.Shared`) عبر فلتر/middleware في الـ API.

---

## 3) رموز الحالة (HTTP Status Codes)

| الرمز | متى |
|:-----:|-----|
| `200 OK` | نجاح قراءة/تعديل |
| `201 Created` | إنشاء مورد (+ `Location` header) |
| `204 No Content` | حذف/عملية بلا محتوى راجع |
| `400 Bad Request` | خطأ تحقّق (validation) |
| `401 Unauthorized` | لا توكن / توكن غير صالح |
| `403 Forbidden` | لا صلاحية · **أو مستأجر غير Active** |
| `404 Not Found` | المورد غير موجود (أو مخفيّ بالعزل) |
| `409 Conflict` | تعارض تزامن (ROWVERSION) / قاعدة أعمال |
| `422 Unprocessable` | قاعدة أعمال مُنتهَكة (اختياري) |
| `429 Too Many Requests` | تجاوز حدّ المعدّل |
| `500 Internal` | خطأ غير متوقّع (رسالة عامّة) |

---

## 4) معالجة الأخطاء الموحّدة (ProblemDetails)

`ExceptionHandlingMiddleware` يحوّل كل استثناء إلى استجابة موحّدة:

| الاستثناء (Application) | الرمز | error.code |
|-------------------------|:-----:|-----------|
| `ValidationException` | 400 | `VALIDATION_ERROR` |
| `NotFoundException` | 404 | `NOT_FOUND` |
| `ForbiddenException` | 403 | `FORBIDDEN` |
| `TenantInactiveException` | 403 | `TENANT_INACTIVE` |
| `BusinessRuleViolationException` | 409 | `BUSINESS_RULE_VIOLATION` |
| `DbUpdateConcurrencyException` | 409 | `CONCURRENCY_CONFLICT` |
| غير متوقّع | 500 | `INTERNAL_ERROR` (رسالة عامّة — التفاصيل في اللوج فقط) |

> رسائل الأخطاء **لا تكشف تفاصيل داخلية** (stack trace, SQL) للعميل — تُسجَّل داخلياً بـ correlation id.

---

## 5) الإصدار والترقيم (Versioning & Pagination)

### الإصدار

```
/api/v1/products      ← الإصدار في المسار
/api/v2/products      ← إصدار لاحق دون كسر v1
```

### الترقيم والفرز والتصفية

```
GET /api/v1/products?page=1&pageSize=20&search=milk&sortBy=name&sortDir=asc&categoryId=5
```

| المُعامل | الافتراضي | ملاحظات |
|----------|:---------:|---------|
| `page` | 1 | 1-based |
| `pageSize` | 20 | حدّ أقصى 100 |
| `search` | — | بحث نصّي |
| `sortBy` / `sortDir` | `id` / `desc` | حقل مفهرَس فقط |
| فلاتر مخصّصة | — | حسب المورد |

---

## 6) عقود الـ API لكل وحدة (Endpoint Contracts)

### 6.1 Authentication

```
POST /api/v1/auth/login
  Body:   { "userName": "ahmad", "password": "***" }
  200:    { accessToken, refreshToken, expiresIn }
  403:    TENANT_INACTIVE (المستأجر غير مُفعَّل)

POST /api/v1/auth/refresh   { refreshToken } → { accessToken, refreshToken }
POST /api/v1/auth/logout    { refreshToken } → 204
POST /api/v1/auth/change-password  { currentPassword, newPassword } → 204
```

### 6.2 Tenants (مالك النظام — `system.tenants.manage`)

```
GET    /api/v1/tenants                    → قائمة المستأجرين وحالاتهم
POST   /api/v1/tenants                    { name, code, contactEmail } → 201 (Status=Active)
GET    /api/v1/tenants/{id}
PUT    /api/v1/tenants/{id}/activate      → 200 (Status=1)
PUT    /api/v1/tenants/{id}/suspend       → 200 (Status=2)
PUT    /api/v1/tenants/{id}/disable       → 200 (Status=3)
```

### 6.3 Users & Roles

```
GET/POST/PUT  /api/v1/users               (users.*)
POST          /api/v1/users/{id}/roles    { roleIds:[...] }   (roles.manage)
GET/POST/PUT  /api/v1/roles               (roles.*)
PUT           /api/v1/roles/{id}/permissions { permissionKeys:[...] }  (roles.manage)
GET           /api/v1/permissions         (roles.view)
```

### 6.4 Catalog

```
GET    /api/v1/products?page&pageSize&search&categoryId   (products.view)
GET    /api/v1/products/{id}                              (products.view)
GET    /api/v1/products/by-barcode/{barcode}             (products.view)
POST   /api/v1/products                                   (products.create) → 201
PUT    /api/v1/products/{id}                              (products.update)
DELETE /api/v1/products/{id}                              (products.delete) → soft delete
GET/POST/PUT/DELETE  /api/v1/categories                  (categories.*)
```

### 6.5 Inventory

```
GET    /api/v1/inventory/stock?productId          (inventory.view)
GET    /api/v1/inventory/movements?productId&from&to  (inventory.view)
POST   /api/v1/inventory/adjustments              (inventory.adjust)
```

### 6.6 Partners

```
GET/POST/PUT/DELETE  /api/v1/customers            (customers.*)
GET/POST/PUT/DELETE  /api/v1/suppliers            (suppliers.*)
POST   /api/v1/customers/{id}/payments            (customers.update)
POST   /api/v1/suppliers/{id}/payments            (suppliers.update)
```

### 6.7 Sales & Purchases

```
GET    /api/v1/sales?page&pageSize&from&to&customerId  (sales.view)
GET    /api/v1/sales/{id}                              (sales.view)
POST   /api/v1/sales                                   (sales.create) → 201
POST   /api/v1/sales/{id}/returns                      (sales.return)
PUT    /api/v1/sales/{id}/cancel                       (sales.cancel)

GET    /api/v1/purchases ...                           (purchases.view)
POST   /api/v1/purchases                               (purchases.create)
POST   /api/v1/purchases/{id}/returns                  (purchases.return)
```

### 6.8 Reports · Audit · Settings

```
GET  /api/v1/reports/sales?from&to&groupBy       (reports.view)
GET  /api/v1/reports/inventory                   (reports.view)
GET  /api/v1/reports/customer-balances           (reports.view)
GET  /api/v1/audit?entityName&from&to&userId     (audit.view)
GET  /api/v1/settings                            (settings.view)
PUT  /api/v1/settings                            (settings.update)
```

---

## 7) مثال عقد كامل — إنشاء فاتورة بيع

```
POST /api/v1/sales
Authorization: Bearer <jwt>   (permission: sales.create)

Request:
{
  "customerId": 88,               // اختياري (null = بيع نقدي)
  "items": [
    { "productId": 1042, "quantity": 3, "unitPrice": 5.5, "discountAmount": 0 },
    { "productId": 1055, "quantity": 1, "unitPrice": 12.0, "discountAmount": 2.0 }
  ],
  "paidAmount": 28.5,
  "notes": "توصيل"
}

201 Created:
{
  "success": true,
  "data": {
    "id": 5012,
    "invoiceNumber": "INV-000173",
    "grandTotal": 28.5000,
    "status": "Confirmed"
  },
  "error": null,
  "meta": { "correlationId": "…", "timestamp": "2026-07-15T…Z" }
}
```

> خلف الكواليس (معاملة ذرّية واحدة): توليد رقم من `Sequences` · إنشاء الفاتورة والبنود بـ snapshot للأسعار · خصم المخزون + حركة `StockMovements` صادرة · تحديث رصيد العميل · كتابة `AuditLog`. أي فشل جزئي → rollback كامل.

---

## 8) قائمة تحقّق تصميم الـ Endpoint (API Checklist)

- [ ] المسار مورد بالجمع تحت `/api/v1/`.
- [ ] فعل HTTP صحيح ورمز حالة مناسب.
- [ ] `[HasPermission(...)]` مطبَّق.
- [ ] المُدخَل DTO مُتحقَّق منه (FluentValidation).
- [ ] المُخرَج DTO (لا كيان) داخل الـ Envelope.
- [ ] القوائم مرقّمة (`page`/`pageSize`) بحدّ أقصى.
- [ ] الأخطاء عبر ProblemDetails الموحّد.
- [ ] التواريخ UTC ISO-8601.
- [ ] موثّق في Swagger.

---

_يُكمّله [02-Solution-Architecture.md](02-Solution-Architecture.md) (تدفّق الطلب) و[10-Identity-RBAC.md](10-Identity-RBAC.md) (الصلاحيات) و[13-Development-Rules.md](13-Development-Rules.md)._
