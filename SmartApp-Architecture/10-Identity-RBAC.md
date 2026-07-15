# 10 — Identity & RBAC (المصادقة والصلاحيات)

> يشرح المصادقة (JWT + Refresh Rotation)، وإدارة المستخدمين/الأدوار/الصلاحيات، ونموذج RBAC القائم على `resource.action`. يلتزم بـ [09-Multi-Tenant.md](09-Multi-Tenant.md) (كل مستخدم ينتمي لمستأجر) و[11-Security-Architecture.md](11-Security-Architecture.md).

---

## 1) المكوّنات (Components)

```
Identity على ASP.NET Core Identity (مُخصَّص):
   AppUser  : IdentityUser<long>    + TenantId, FullName, IsSystemOwner, ...
   AppRole  : IdentityRole<long>    + TenantId, Description, IsSystemRole
   Permission (مرجعي عالمي)         : Key, Resource, Action
   RolePermission (M:N)             : Role ↔ Permission
   UserRole (M:N)                   : User ↔ Role
   RefreshToken                     : جلسات التجديد (مُدوَّرة)
```

- المصادقة: **JWT access token** (قصير العمر) + **Refresh token** (طويل، مُدوَّر).
- التفويض: **Permission-based** (ليس role-name-based) — الأدوار حاويات للصلاحيات.

---

## 2) نموذج الصلاحيات (Permission Model): `resource.action`

كل صلاحية نصّ بصيغة `resource.action`:

| المورد (Resource) | الأفعال (Actions) | أمثلة المفاتيح |
|-------------------|-------------------|----------------|
| `products` | view, create, update, delete | `products.create`, `products.update` |
| `categories` | view, create, update, delete | `categories.view` |
| `customers` | view, create, update, delete | `customers.create` |
| `suppliers` | view, create, update, delete | `suppliers.view` |
| `inventory` | view, adjust | `inventory.adjust` |
| `sales` | view, create, return, cancel | `sales.create`, `sales.return` |
| `purchases` | view, create, return, cancel | `purchases.create` |
| `reports` | view | `reports.view` |
| `users` | view, create, update, delete | `users.create` |
| `roles` | view, create, update, delete | `roles.manage` |
| `settings` | view, update | `settings.update` |
| `audit` | view | `audit.view` |
| `system.tenants` | manage | `system.tenants.manage` *(مالك النظام فقط)* |

> **ثابتة في الكود:** تُعرَّف مفاتيح الصلاحيات كثوابت في `SmartApp.Shared/Constants/Permissions.cs` وتُزرَع في جدول `Permissions` عبر `PermissionSeeder`. لا تُنشأ صلاحيات ديناميكياً.

---

## 3) الأدوار الافتراضية (Default Roles)

تُزرَع لكل مستأجر جديد (قابلة للتخصيص من صاحب المستأجر):

| الدور | الوصف | الصلاحيات (مثال) |
|-------|-------|------------------|
| **Owner** | صاحب الحساب — كل الصلاحيات داخل مستأجره | كل صلاحيات المستأجر |
| **Manager** | مدير — إدارة الكتالوج والمخزون والتقارير | products.*, inventory.*, sales.*, purchases.*, reports.view |
| **Employee** | موظف — عمليات يومية محدودة | products.view, sales.create, sales.view, customers.* |
| **Cashier** | كاشير — بيع فقط | sales.create, sales.view, products.view |
| **Accountant** | محاسب — تقارير وأرصدة فقط | reports.view, customers.view, suppliers.view, audit.view |

> **مالك النظام (System Owner)** ليس دوراً داخل مستأجر — هو حساب فوقي (`IsSystemOwner=1`, `TenantId=NULL`) يملك `system.tenants.manage` لإدارة المستأجرين ([09-Multi-Tenant.md §5](09-Multi-Tenant.md)).

### مصفوفة صلاحيات مختصرة (Permission Matrix)

| الصلاحية | Owner | Manager | Employee | Cashier | Accountant |
|----------|:-----:|:-------:|:--------:|:-------:|:----------:|
| products.view | ✅ | ✅ | ✅ | ✅ | — |
| products.create/update/delete | ✅ | ✅ | — | — | — |
| sales.create | ✅ | ✅ | ✅ | ✅ | — |
| sales.return | ✅ | ✅ | — | — | — |
| purchases.* | ✅ | ✅ | — | — | — |
| inventory.adjust | ✅ | ✅ | — | — | — |
| reports.view | ✅ | ✅ | — | — | ✅ |
| users.* / roles.* | ✅ | — | — | — | — |
| settings.update | ✅ | — | — | — | — |
| audit.view | ✅ | — | — | — | ✅ |

---

## 4) المصادقة — JWT (Authentication)

### 4.1 محتوى التوكن (Claims)

```json
{
  "sub": "5012",                    // UserId
  "tenant_id": "1001",              // المستأجر — مصدر العزل
  "name": "أحمد",
  "is_system_owner": "false",
  "permissions": ["products.view","sales.create", "..."],
  "exp": 1731000000,                // انتهاء قصير (15 دقيقة)
  "iss": "SmartApp",
  "aud": "SmartApp.Clients"
}
```

- **`tenant_id`** مضمَّن ومُوقَّع — يُقرأ خادم-جانبياً للعزل (لا يُزوَّر بلا كسر التوقيع).
- **`permissions`** مضمَّنة لتفويض سريع بلا ضربة DB لكل طلب (مع عمر قصير للتوكن لتقليل نافذة التغيير).

### 4.2 عمر التوكنات

| التوكن | العمر | التخزين |
|--------|:-----:|---------|
| Access (JWT) | 15 دقيقة | لا يُخزَّن خادم-جانبياً (stateless) |
| Refresh | 7–30 يوم | `RefreshTokens` — يُخزَّن **hash** فقط (SHA-256) |

---

## 5) Refresh Token Rotation (التدوير وكشف إعادة الاستخدام)

```
Login ──► access(15m) + refresh#1 (يُخزَّن hash)
                          │
   بعد انتهاء الـ access:  │  POST /auth/refresh { refresh#1 }
                          ▼
   تحقّق: refresh#1 صالح وغير مُبطَل؟
     ├─ نعم ─► إصدار access جديد + refresh#2
     │         إبطال refresh#1، تسجيل ReplacedByHash = hash(refresh#2)
     │
     └─ لا / مُستخدَم سابقاً (reuse!) ─► ⚠ كشف سرقة:
              إبطال كل توكنات المستخدم (سلسلة كاملة) + تسجيل حادث أمني
```

- كل استخدام لـ refresh **يُصدر واحداً جديداً ويُبطِل القديم** (rotation).
- استخدام توكن مُبطَل سابقاً = **مؤشّر سرقة** → إبطال السلسلة كلها (reuse detection).
- عند تسجيل الخروج (`/auth/logout`): إبطال الـ refresh الحالي.
- عند تعطيل المستأجر: كل تجديد يُرفض ([09-Multi-Tenant.md §5.2](09-Multi-Tenant.md)).

---

## 6) التفويض — Permission-Based Authorization

### 6.1 الاستخدام على الـ Controller/Action

```csharp
[HttpPost]
[HasPermission(Permissions.Products.Create)]   // "products.create"
public async Task<IActionResult> Create(CreateProductCommand cmd)
    => Ok(await _mediator.Send(cmd));
```

### 6.2 كيف يعمل (Policy Provider ديناميكي)

```csharp
// SmartApp.API/Authorization/
// كل مفتاح صلاحية يتحوّل تلقائياً إلى Policy عبر PermissionPolicyProvider
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string p) => Permission = p;
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx, PermissionRequirement req)
    {
        // تحقّق أن claim permissions يحتوي المطلوب
        if (ctx.User.Claims.Any(c => c.Type == "permissions" && c.Value == req.Permission))
            ctx.Succeed(req);
        return Task.CompletedTask;
    }
}
```

- لا حاجة لتعريف Policy يدوياً لكل صلاحية — `PermissionPolicyProvider` ينشئها من اسم المفتاح.
- التفويض على مستوى **الصلاحية** لا اسم الدور → تغيير صلاحيات دور لا يتطلّب تغيير كود.

---

## 7) إدارة كلمات المرور (Password Management)

| العملية | الآلية |
|---------|--------|
| **التجزئة (Hashing)** | ASP.NET Core Identity `PasswordHasher` (PBKDF2، قابل للترقية لـ Argon2) |
| **السياسة** | حدّ أدنى 8، حروف كبيرة/صغيرة/رقم/رمز (قابلة للتهيئة) |
| **التغيير** | `POST /auth/change-password` — يتطلّب كلمة المرور الحالية |
| **قفل الحساب** | بعد N محاولات فاشلة → `LockoutEndUtc` (Brute-force protection) |
| **إعادة التعيين** | عبر البريد (اختياري — يتطلّب `EmailSender`) بتوكن مؤقت |

> **لا تُخزَّن كلمة مرور خام أبداً.** الـ Refresh tokens تُخزَّن كـ hash. الأسرار (JWT signing key) من الإعدادات/Secret store لا الكود.

---

## 8) عمليات Identity (API Surface)

```
POST /api/v1/auth/login             { userName, password } → { accessToken, refreshToken }
POST /api/v1/auth/refresh           { refreshToken }       → { accessToken, refreshToken }
POST /api/v1/auth/logout            { refreshToken }       → 204
POST /api/v1/auth/change-password   { current, new }       → 204

GET  /api/v1/users                  (users.view)
POST /api/v1/users                  (users.create)
PUT  /api/v1/users/{id}             (users.update)
POST /api/v1/users/{id}/roles       (roles.manage)   ← تعيين أدوار

GET  /api/v1/roles                  (roles.view)
POST /api/v1/roles                  (roles.manage)
PUT  /api/v1/roles/{id}/permissions (roles.manage)   ← تعيين صلاحيات

GET  /api/v1/permissions            (roles.view)     ← قائمة الصلاحيات المتاحة
```

تفصيل عقود الـ API في [12-API-Architecture.md](12-API-Architecture.md).

---

## 9) نقاط أمنية حاكمة (Security Invariants)

1. **تسجيل الدخول يفحص حالة المستأجر** — لا دخول إن كان `Tenant.Status != Active`.
2. **`tenant_id` في التوكن يُوقَّع** ولا يُقبل تعديله.
3. **الصلاحيات في التوكن + عمر قصير** — لتقليل نافذة الصلاحية القديمة.
4. **تفرّد اسم المستخدم داخل المستأجر لا عالمياً** — مستأجران قد يملكان `admin`.
5. **مالك النظام معزول** — لا ينتمي لمستأجر، ولا يملك صلاحيات أعمال داخل مستأجر إلا صراحةً.

---

_يُكمّله [09-Multi-Tenant.md](09-Multi-Tenant.md) (سياق المستأجر) و[11-Security-Architecture.md](11-Security-Architecture.md) (OWASP والحماية الشاملة) و[06-Tables-Definitions.md §2](06-Tables-Definitions.md) (جداول Identity)._
