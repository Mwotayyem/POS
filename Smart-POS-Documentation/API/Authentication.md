# API — Authentication (المصادقة)

> توثيق endpoints المصادقة في **Smart ERP POS**. تعتمد **ASP.NET Identity + JWT + Refresh Token**. كل الطلبات تحت `/api/v1/auth`. الاستجابات بصيغة JSON، والتوثيق يلتزم بـ [05-Authentication.md](../05-Authentication.md).

---

## 1) المبادئ العامة

| البند | القرار |
|-------|--------|
| Access Token | JWT قصير العمر (15 دقيقة) — يحمل `sub`, `tenantId`, `storeId`, `roles`, `permissions` |
| Refresh Token | طويل العمر (7 أيام)، يُخزَّن في `RefreshTokens`، يُدوَّر عند كل استخدام (Rotation) |
| Tenant Resolution | عبر `X-Tenant-Slug` header أو subdomain |
| كلمة المرور | `PasswordHash` عبر Identity (PBKDF2) — لا تُخزَّن نصّاً |
| رموز الحالة | 200 نجاح، 400 تحقّق، 401 مصادقة، 403 صلاحية، 429 محاولات كثيرة |

**رأس المصادقة لكل طلب محمي:**
```
Authorization: Bearer <access_token>
X-Tenant-Slug: acme-market
```

---

## 2) POST /api/v1/auth/login — تسجيل الدخول

**Request**
```json
{
  "email": "cashier@acme.com",
  "password": "P@ssw0rd!",
  "storeId": 12,
  "rememberMe": true
}
```

**Response 200 OK**
```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "b1f3c9d2-7a4e-4c8f-9e21-0a5b6c7d8e9f",
    "expiresIn": 900,
    "tokenType": "Bearer",
    "user": {
      "id": 4501,
      "fullName": "Ahmad Ali",
      "email": "cashier@acme.com",
      "tenantId": 3,
      "storeId": 12,
      "roles": ["Cashier"],
      "permissions": ["sales.create", "pos.access"]
    }
  }
}
```

**Response 401 Unauthorized**
```json
{ "success": false, "error": { "code": "INVALID_CREDENTIALS", "message": "البريد أو كلمة المرور غير صحيحة" } }
```

**Response 429** — عند تجاوز 5 محاولات فاشلة خلال دقيقة (Rate Limiting / Lockout).

---

## 3) POST /api/v1/auth/refresh — تحديث الرمز

**Request**
```json
{ "refreshToken": "b1f3c9d2-7a4e-4c8f-9e21-0a5b6c7d8e9f" }
```

**Response 200 OK** — يُصدر Access Token جديداً و**Refresh Token جديداً** (الرمز القديم يُبطَل — Token Rotation).
```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiI...",
    "refreshToken": "c2e4d0a1-8b5f-4d9a-af32-1b6c7d8e9f0a",
    "expiresIn": 900,
    "tokenType": "Bearer"
  }
}
```

**Response 401** — الرمز منتهٍ أو مُبطَل أو مُعاد استخدامه (يُبطِل كل رموز المستخدم — كشف سرقة).
```json
{ "success": false, "error": { "code": "INVALID_REFRESH_TOKEN", "message": "جلسة منتهية، الرجاء تسجيل الدخول من جديد" } }
```

---

## 4) POST /api/v1/auth/logout — تسجيل الخروج

**Request** (Header: `Authorization: Bearer ...`)
```json
{ "refreshToken": "c2e4d0a1-8b5f-4d9a-af32-1b6c7d8e9f0a" }
```

**Response 200 OK** — يضبط `RevokedAt` على الرمز.
```json
{ "success": true, "data": { "message": "تم تسجيل الخروج بنجاح" } }
```

---

## 5) POST /api/v1/auth/forgot-password — طلب إعادة تعيين

**Request**
```json
{ "email": "owner@acme.com" }
```

**Response 200 OK** — دائماً 200 (لا نكشف وجود البريد — منع Enumeration).
```json
{ "success": true, "data": { "message": "إذا كان البريد مسجّلاً، ستصلك رسالة إعادة التعيين" } }
```
> يُرسَل رمز `resetToken` صالح 30 دقيقة عبر البريد (Hangfire Job).

---

## 6) POST /api/v1/auth/reset-password — تنفيذ إعادة التعيين

**Request**
```json
{
  "email": "owner@acme.com",
  "resetToken": "CfDJ8N...encoded",
  "newPassword": "N3wP@ssw0rd!",
  "confirmPassword": "N3wP@ssw0rd!"
}
```

**Response 200 OK**
```json
{ "success": true, "data": { "message": "تم تغيير كلمة المرور بنجاح" } }
```

**Response 400**
```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "فشل التحقق",
    "details": [
      { "field": "resetToken", "message": "الرمز منتهٍ أو غير صالح" },
      { "field": "newPassword", "message": "يجب 8 أحرف على الأقل مع رمز ورقم" }
    ]
  }
}
```

---

## 7) POST /api/v1/auth/verify-email — تأكيد البريد

**Request**
```json
{ "userId": 4501, "token": "CfDJ8N...emailtoken" }
```

**Response 200 OK**
```json
{ "success": true, "data": { "emailConfirmed": true } }
```
> يضبط `Users.EmailConfirmed = 1`.

---

## 8) POST /api/v1/auth/otp/send — إرسال رمز التحقق (2FA)

**Request**
```json
{ "userId": 4501, "channel": "sms" }
```

**Response 200 OK**
```json
{ "success": true, "data": { "expiresIn": 120, "channel": "sms", "maskedTarget": "•••• 4589" } }
```

## POST /api/v1/auth/otp/verify — تأكيد الرمز

**Request**
```json
{ "userId": 4501, "code": "483920" }
```

**Response 200 OK** — يُصدر Access + Refresh Token (نفس بنية login).
```json
{ "success": true, "data": { "accessToken": "eyJ...", "refreshToken": "...", "expiresIn": 900 } }
```

**Response 401**
```json
{ "success": false, "error": { "code": "INVALID_OTP", "message": "رمز غير صحيح أو منتهٍ" } }
```

---

## 9) GET /api/v1/auth/me — بيانات المستخدم الحالي

**Response 200 OK**
```json
{
  "success": true,
  "data": {
    "id": 4501, "fullName": "Ahmad Ali", "email": "cashier@acme.com",
    "tenantId": 3, "storeId": 12,
    "roles": ["Cashier"], "permissions": ["sales.create", "pos.access"],
    "lastLoginAt": "2026-07-13T08:22:11Z"
  }
}
```

---

## 10) جدول رموز الحالة (Status Codes)

| الرمز | المعنى | الحالات |
|-------|--------|---------|
| 200 | نجاح | login, refresh, logout, verify |
| 400 | خطأ تحقّق | كلمة مرور ضعيفة، حقول ناقصة |
| 401 | فشل مصادقة | بيانات خاطئة، رمز منتهٍ/مبطَل |
| 403 | ممنوع | حساب معطّل، بريد غير مؤكَّد |
| 423 | مقفل | Lockout بعد محاولات كثيرة |
| 429 | محاولات كثيرة | Rate limiting |

---

## 11) الأمان (Security Notes)

1. كل الطلبات عبر **HTTPS** فقط.
2. Refresh Tokens تُدوَّر عند كل استخدام؛ إعادة استخدام رمز مُبطَل تُبطِل كل جلسات المستخدم (كشف سرقة).
3. `tenantId` داخل الـ JWT يُطابَق مع `X-Tenant-Slug` — منع Cross-Tenant.
4. Lockout بعد 5 محاولات فاشلة (`AccessFailedCount` في Identity).
5. كلمات المرور: PBKDF2 عبر Identity، لا تُخزَّن نصّاً أبداً.
