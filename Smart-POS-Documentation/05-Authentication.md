# 05 — Authentication (المصادقة، JWT، Refresh Token، OTP)

> يوثّق هذا الملف طبقة **المصادقة (Authentication)** بالكامل: كيف يُثبِت المستخدم هويته، وكيف تُصدَر الرموز (Tokens) وتُدوَّر وتُبطَل، وكيف نحمي النظام من هجمات القوة الغاشمة (Brute Force). يلتزم بمعايير [04-Database-Design.md](04-Database-Design.md): كل الجداول متعدّدة المستأجرين، Soft Delete، توقيت UTC، `DECIMAL`/`DATETIME2(3)`، والأعمدة المشتركة.
>
> **التمييز الحاكم:** *Authentication* (مَن أنت؟) موثّق هنا. *Authorization* (ماذا يحقّ لك؟) موثّق في [06-Roles-And-Permissions.md](06-Roles-And-Permissions.md).

---

## 1) المبادئ الحاكمة (Authentication Principles)

| المبدأ | القرار | السبب |
|--------|--------|-------|
| Password Hashing | `ASP.NET Identity PasswordHasher` (PBKDF2-HMAC-SHA512، 100,000 iteration، salt عشوائي 128-bit) | لا تُخزَّن كلمات مرور بنص صريح أبداً |
| Access Token | **JWT** قصير العمر (15 دقيقة) موقّع `HS256`/`RS256` | تقليل نافذة الاستغلال عند التسريب |
| Refresh Token | Opaque random 256-bit، مُخزَّن **مُجزَّأً (hashed)** في DB، عمره 7–30 يوماً | لا يُقرأ محتواه، ويُبطَل مركزياً |
| Rotation | تدوير إجباري لكل استخدام + كشف إعادة الاستخدام (Reuse Detection) | إبطال السلسلة كلها عند السرقة |
| Isolation | كل عملية مصادقة مربوطة بـ `TenantId` | منع تسجيل دخول عابر للمستأجرين |
| Lockout | قفل الحساب بعد 5 محاولات فاشلة لمدّة متصاعدة | مقاومة Brute Force |
| Transport | HTTPS فقط + `Secure; HttpOnly; SameSite=Strict` للكوكيز | منع MITM و XSS-token-theft |

---

## 2) جداول المصادقة (Authentication Tables)

### 2.1 `Users` (يمتدّ من ASP.NET Identity)

نستخدم `AspNetUsers` كأساس مع أعمدة مخصّصة للـ multi-tenancy. الجدول يحمل الأعمدة المشتركة إضافةً لأعمدة Identity القياسية.

```sql
CREATE TABLE [dbo].[Users]
(
    -- ===== أعمدة ASP.NET Identity القياسية =====
    [UserName]             NVARCHAR(256)  NOT NULL,
    [NormalizedUserName]   NVARCHAR(256)  NOT NULL,
    [Email]                NVARCHAR(256)  NOT NULL,
    [NormalizedEmail]      NVARCHAR(256)  NOT NULL,
    [EmailConfirmed]       BIT            NOT NULL CONSTRAINT DF_Users_EmailConfirmed DEFAULT (0),
    [PasswordHash]         NVARCHAR(MAX)  NULL,       -- PBKDF2 hash (Identity v3)
    [SecurityStamp]        NVARCHAR(MAX)  NULL,       -- يتغيّر عند تغيير كلمة المرور/الصلاحيات
    [PhoneNumber]          NVARCHAR(20)   NULL,
    [PhoneNumberConfirmed] BIT            NOT NULL CONSTRAINT DF_Users_PhoneConfirmed DEFAULT (0),
    [TwoFactorEnabled]     BIT            NOT NULL CONSTRAINT DF_Users_2FA DEFAULT (0),
    [LockoutEnd]           DATETIMEOFFSET NULL,        -- نهاية القفل الحالي
    [LockoutEnabled]       BIT            NOT NULL CONSTRAINT DF_Users_LockoutEnabled DEFAULT (1),
    [AccessFailedCount]    INT            NOT NULL CONSTRAINT DF_Users_AccessFailed DEFAULT (0),

    -- ===== أعمدة مخصّصة =====
    [FullName]             NVARCHAR(150)  NOT NULL,
    [PublicId]             UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Users_PublicId DEFAULT (NEWID()),
    [IsActive]             BIT            NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    [LastLoginDate]        DATETIME2(3)   NULL,
    [PasswordChangedDate]  DATETIME2(3)   NULL,
    [PreferredLanguage]    VARCHAR(5)     NOT NULL CONSTRAINT DF_Users_Lang DEFAULT ('ar'),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,     -- الفرع الافتراضي للمستخدم (NULL = وصول لكل الفروع)
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_Users_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Users_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_Users_Store]  FOREIGN KEY ([StoreId])  REFERENCES [dbo].[Stores]([Id])
);
GO

-- البريد فريد داخل المستأجر فقط (نفس البريد قد يُستخدم في مستأجرَين مختلفين)
CREATE UNIQUE NONCLUSTERED INDEX [UX_Users_Tenant_Email]
    ON [dbo].[Users] ([TenantId], [NormalizedEmail])
    WHERE [IsDeleted] = 0;
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Users_PublicId] ON [dbo].[Users] ([PublicId]);
GO
```

> **ملاحظة عزل:** التفرّد على `(TenantId, NormalizedEmail)` وليس على البريد وحده — هذا يسمح بأن يمتلك نفس البريد حسابات في شركات مختلفة، وهو سلوك SaaS متوقّع.

### 2.2 `RefreshTokens`

```sql
CREATE TABLE [dbo].[RefreshTokens]
(
    [UserId]           BIGINT         NOT NULL,
    [TokenHash]        VARBINARY(32)  NOT NULL,   -- SHA-256 للرمز الخام (لا نخزّن الخام)
    [ExpiresAt]        DATETIME2(3)   NOT NULL,
    [CreatedByIp]      VARCHAR(45)    NULL,        -- IPv4/IPv6
    [UserAgent]        NVARCHAR(400)  NULL,
    [RevokedAt]        DATETIME2(3)   NULL,
    [RevokedByIp]      VARCHAR(45)    NULL,
    [ReplacedByHash]   VARBINARY(32)  NULL,        -- الرمز الذي حلّ محلّه (سلسلة التدوير)
    [ReasonRevoked]    VARCHAR(50)    NULL,        -- 'ROTATED','LOGOUT','REUSE_DETECTED','LOCKOUT'

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_RefreshTokens_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_RefreshTokens_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_RefreshTokens_User]   FOREIGN KEY ([UserId])   REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT [FK_RefreshTokens_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_RefreshTokens_Hash] ON [dbo].[RefreshTokens] ([TokenHash]);
GO
CREATE NONCLUSTERED INDEX [IX_RefreshTokens_User_Active]
    ON [dbo].[RefreshTokens] ([UserId], [ExpiresAt])
    INCLUDE ([RevokedAt]) WHERE [IsDeleted] = 0;
GO
```

> **قاعدة أمنية:** الرمز الخام لا يُخزَّن أبداً — نخزّن `SHA-256` منه فقط. عند التحقّق نجزّئ الرمز الوارد ونطابقه. تسريب قاعدة البيانات لا يمنح المهاجم رموزاً صالحة.

### 2.3 `EmailVerificationTokens`

```sql
CREATE TABLE [dbo].[EmailVerificationTokens]
(
    [UserId]           BIGINT         NOT NULL,
    [TokenHash]        VARBINARY(32)  NOT NULL,   -- SHA-256 للرمز المُرسَل بالرابط
    [Purpose]          VARCHAR(30)    NOT NULL,   -- 'EMAIL_CONFIRM','PASSWORD_RESET','EMAIL_CHANGE'
    [NewEmail]         NVARCHAR(256)  NULL,        -- عند تغيير البريد
    [ExpiresAt]        DATETIME2(3)   NOT NULL,
    [ConsumedAt]       DATETIME2(3)   NULL,        -- وقت الاستهلاك (لمرّة واحدة فقط)
    [RequestedByIp]    VARCHAR(45)    NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_EmailVerTokens_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_EmailVerTokens_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_EmailVerificationTokens] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_EmailVerTokens_User]   FOREIGN KEY ([UserId])   REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT [FK_EmailVerTokens_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_EmailVerTokens_Hash] ON [dbo].[EmailVerificationTokens] ([TokenHash]);
GO
CREATE NONCLUSTERED INDEX [IX_EmailVerTokens_User_Purpose]
    ON [dbo].[EmailVerificationTokens] ([UserId], [Purpose], [ExpiresAt]) WHERE [IsDeleted] = 0;
GO
```

### 2.4 `OtpCodes`

```sql
CREATE TABLE [dbo].[OtpCodes]
(
    [UserId]           BIGINT         NULL,        -- قد يكون NULL قبل إنشاء الحساب (OTP للتسجيل)
    [Destination]      NVARCHAR(256)  NOT NULL,    -- رقم الهاتف أو البريد المُرسَل إليه
    [Channel]          VARCHAR(10)    NOT NULL,    -- 'SMS','EMAIL','WHATSAPP'
    [Purpose]          VARCHAR(30)    NOT NULL,    -- 'LOGIN_2FA','PHONE_VERIFY','RESET'
    [CodeHash]         VARBINARY(32)  NOT NULL,    -- SHA-256 للرمز الرقمي (6 خانات)
    [ExpiresAt]        DATETIME2(3)   NOT NULL,    -- عادةً 5 دقائق
    [AttemptCount]     TINYINT        NOT NULL CONSTRAINT DF_OtpCodes_Attempts DEFAULT (0),
    [MaxAttempts]      TINYINT        NOT NULL CONSTRAINT DF_OtpCodes_MaxAttempts DEFAULT (5),
    [ConsumedAt]       DATETIME2(3)   NULL,
    [RequestedByIp]    VARCHAR(45)    NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_OtpCodes_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_OtpCodes_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_OtpCodes] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_OtpCodes_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO
CREATE NONCLUSTERED INDEX [IX_OtpCodes_Dest_Purpose]
    ON [dbo].[OtpCodes] ([TenantId], [Destination], [Purpose], [ExpiresAt]) WHERE [IsDeleted] = 0;
GO
```

---

## 3) بنية JWT (JWT Structure)

الـ Access Token هو **JWT** موقّع. نستخدم `RS256` في الإنتاج (مفتاح خاص للتوقيع، عام للتحقّق) و`HS256` في التطوير. المدّة: **15 دقيقة**.

### 3.1 الـ Claims

```jsonc
{
  // ===== Standard Claims =====
  "iss": "https://api.smart-erp-pos.com",   // المُصدِر
  "aud": "smart-erp-pos-web",               // الجمهور
  "sub": "10245",                            // UserId (BIGINT)
  "jti": "8f3c...-uuid",                      // معرّف الرمز الفريد (لإبطال محدّد)
  "iat": 1752345600,                          // وقت الإصدار (epoch)
  "exp": 1752346500,                          // انتهاء الصلاحية (iat + 900s)
  "nbf": 1752345600,

  // ===== Custom Claims (العزل والصلاحيات) =====
  "tenant_id": "42",                          // ★ المستأجر — أساس العزل
  "store_id": "7",                            // الفرع الحالي (قد يتبدّل)
  "user_id": "10245",
  "full_name": "أحمد الكاشير",
  "email": "ahmad@tenant42.com",
  "security_stamp": "A1B2C3",                 // يجب أن يطابق DB وإلا يُرفض الرمز
  "roles": ["Cashier"],                       // الأدوار
  "perm": [                                   // ★ الصلاحيات المُسطّحة (Resource.Action)
    "Sales.Create", "Sales.Read",
    "Products.Read", "Customers.Read", "Pos.Operate"
  ]
}
```

> **قرار تصميمي:** نضع الصلاحيات (`perm`) داخل الـ JWT مباشرةً لتفادي استعلام DB في كل طلب. إن تجاوز حجم الرمز الحدّ (كثرة الصلاحيات لدور Company Owner)، نضع بدلاً منها `perm_ref` (معرّف نسخة صلاحيات مُخزَّن في Redis) ونحمّلها من الكاش. التفاصيل في [06-Roles-And-Permissions.md](06-Roles-And-Permissions.md).

### 3.2 التحقّق من الرمز (Token Validation)

```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,           ValidIssuer = _cfg["Jwt:Issuer"],
    ValidateAudience = true,         ValidAudience = _cfg["Jwt:Audience"],
    ValidateLifetime = true,         ClockSkew = TimeSpan.FromSeconds(30),
    ValidateIssuerSigningKey = true, IssuerSigningKey = _signingKey,
    RoleClaimType = ClaimTypes.Role, NameClaimType = "user_id"
};
// طبقة إضافية: التحقّق من security_stamp مقابل DB عند العمليات الحسّاسة
```

---

## 4) الميزات — التدفّق والـ Endpoints

### 4.1 Login (تسجيل الدخول)

**Endpoint:** `POST /api/auth/login`

```jsonc
// Request
{ "email": "ahmad@tenant42.com", "password": "••••••", "rememberMe": true,
  "tenantSlug": "tenant42" }   // أو يُستنتج من الـ subdomain
```

**التدفّق:**
1. استنتاج `TenantId` من `tenantSlug`/subdomain. لا مستأجر ⇒ `404` عام (لا نكشف الوجود).
2. جلب المستخدم بـ `(TenantId, NormalizedEmail)`. غير موجود ⇒ نُنفّذ **dummy hash** (لتوحيد زمن الاستجابة ومنع User Enumeration) ثم نُرجِع `401` عام.
3. فحص `IsActive` و`IsDeleted` و`LockoutEnd`. مقفول ⇒ `423 Locked`.
4. `PasswordHasher.VerifyHashedPassword`. فشل ⇒ زيادة `AccessFailedCount`، وعند بلوغ 5 ⇒ ضبط `LockoutEnd`.
5. نجاح + `TwoFactorEnabled = 1` ⇒ إصدار **OTP** والانتقال لمسار OTP (لا نُصدِر JWT بعد).
6. نجاح بلا 2FA ⇒ تصفير `AccessFailedCount`، تحديث `LastLoginDate`، إصدار **Access JWT + Refresh Token**.

```jsonc
// Response 200
{ "accessToken": "eyJ...", "expiresIn": 900,
  "refreshToken": "opaque-256bit",   // أو يُرسَل ككوكي HttpOnly
  "tokenType": "Bearer" }
```

### 4.2 Logout (تسجيل الخروج)

**Endpoint:** `POST /api/auth/logout`
- إبطال Refresh Token الحالي: `RevokedAt = now`, `ReasonRevoked = 'LOGOUT'`.
- (اختياري) إضافة `jti` للـ Access Token إلى **Blocklist** في Redis حتى `exp` لإبطاله فوراً.
- مسح الكوكيز (`Set-Cookie` بتاريخ منتهٍ).
- **Logout All Devices:** إبطال كل `RefreshTokens` للمستخدم + تدوير `SecurityStamp` (يُبطِل كل JWT الحالية عند فحص الطابع).

### 4.3 Forgot Password / Reset Password

**`POST /api/auth/forgot-password`** ⇒ `{ email }`
- ننشئ رمزاً عشوائياً، نخزّن `SHA-256` منه في `EmailVerificationTokens` (Purpose = `PASSWORD_RESET`, صلاحية 60 دقيقة)، ونُرسِل رابطاً بالبريد.
- **دائماً** نُرجِع `200` بنفس الرسالة سواء وُجد البريد أم لا (منع User Enumeration).

**`POST /api/auth/reset-password`** ⇒ `{ token, newPassword }`
- نجزّئ الرمز الوارد ونطابقه، نفحص `ExpiresAt` و`ConsumedAt IS NULL`.
- نجاح ⇒ `PasswordHasher.HashPassword` للجديد، تحديث `PasswordChangedDate`، **تدوير `SecurityStamp`** (يُبطِل كل الجلسات)، وضبط `ConsumedAt` للرمز، وإبطال كل `RefreshTokens`.

### 4.4 Refresh Token (تدوير الرمز)

**Endpoint:** `POST /api/auth/refresh` ⇒ `{ refreshToken }`

آلية **Rotation with Reuse Detection**:
1. نجزّئ الرمز الوارد ونبحث بـ `TokenHash`.
2. **غير موجود** ⇒ `401`.
3. موجود لكن `RevokedAt IS NOT NULL` ⇒ **إعادة استخدام رمز مُبطَل!** = مؤشّر سرقة ⇒ إبطال **كل السلسلة/كل رموز المستخدم**، `ReasonRevoked = 'REUSE_DETECTED'`، وتنبيه أمني.
4. منتهٍ (`ExpiresAt < now`) ⇒ `401`.
5. صالح ⇒ إبطاله (`RevokedAt = now`, `ReasonRevoked = 'ROTATED'`)، إنشاء رمز جديد، وربط القديم بالجديد عبر `ReplacedByHash`. ثم إصدار Access JWT جديد.

```
سلسلة التدوير:  RT1 ──rotated──▶ RT2 ──rotated──▶ RT3 (النشط)
لو استُخدم RT1 مرّة أخرى ⇒ REUSE_DETECTED ⇒ إبطال RT1..RT3 كلها.
```

### 4.5 Remember Me

- عند `rememberMe = true` ⇒ عمر Refresh Token = **30 يوماً** (بدلاً من 7)، والكوكي `Persistent`.
- عند `false` ⇒ Session Cookie (يُمسَح بإغلاق المتصفّح) وعمر أقصر.
- لا يُطيل عمر الـ Access JWT إطلاقاً (يبقى 15 دقيقة) — فقط عمر التدوير.

### 4.6 Email Verification

**`POST /api/auth/send-verification`** ⇒ ينشئ رمزاً (Purpose = `EMAIL_CONFIRM`) ويُرسِل رابطاً.
**`GET /api/auth/verify-email?token=...`** ⇒ يطابق الـ hash، يضبط `Users.EmailConfirmed = 1`، ويستهلك الرمز.
- الحسابات غير المؤكّدة قد تُقيَّد (مثلاً لا تُصدَر لها فواتير) حسب سياسة المستأجر.

### 4.7 OTP (رمز لمرّة واحدة / 2FA)

**`POST /api/auth/otp/request`** ⇒ `{ purpose, channel }`
- توليد رقم 6 خانات (`RandomNumberGenerator`)، تخزين `SHA-256` منه في `OtpCodes` (صلاحية 5 دقائق)، وإرسال عبر SMS/Email/WhatsApp.
- **Rate limit:** رمز واحد كل 60 ثانية، وحدّ أقصى 5 طلبات/ساعة لكل وجهة.

**`POST /api/auth/otp/verify`** ⇒ `{ code, purpose }`
- زيادة `AttemptCount`. تجاوز `MaxAttempts` ⇒ إبطال الرمز فوراً.
- مطابقة الـ hash + فحص `ExpiresAt` و`ConsumedAt IS NULL` ⇒ نجاح ⇒ ضبط `ConsumedAt` وإصدار الـ JWT (في مسار 2FA).

---

## 5) مخطّط تدفّق تسجيل الدخول مع OTP (ASCII Flow)

```
┌──────────┐  POST /login (email,password,tenant)   ┌─────────────────┐
│  Client  │ ─────────────────────────────────────▶ │  Auth Endpoint  │
└──────────┘                                         └────────┬────────┘
                                                              │ resolve TenantId
                                                              ▼
                                                   ┌─────────────────────┐
                                                   │ Load User by        │
                                                   │ (TenantId, Email)   │
                                                   └──────────┬──────────┘
                                     not found / inactive     │  found & active
                                  ┌────────────────────────── ┤
                                  ▼                            ▼
                         ┌───────────────┐          ┌────────────────────┐
                         │ dummy-hash +  │          │ Check LockoutEnd   │
                         │ 401 (generic) │          └─────────┬──────────┘
                         └───────────────┘             locked │  not locked
                                                   ┌───────────┤
                                                   ▼           ▼
                                          ┌────────────┐  ┌──────────────────┐
                                          │423 Locked  │  │ Verify Password  │
                                          └────────────┘  └────────┬─────────┘
                                                       fail         │  ok
                                            ┌─────────────────────  ┤
                                            ▼                       ▼
                                 ┌────────────────────┐   ┌──────────────────────┐
                                 │ AccessFailedCount++ │  │ TwoFactorEnabled?     │
                                 │ if>=5 ⇒ set Lockout │  └───────┬───────────┬───┘
                                 │ 401 (generic)       │      no  │           │ yes
                                 └────────────────────┘          ▼           ▼
                                                     ┌───────────────┐  ┌──────────────────┐
                                                     │ Issue JWT +   │  │ Generate OTP     │
                                                     │ Refresh Token │  │ store hash, send │
                                                     └───────┬───────┘  │ SMS/Email        │
                                                             │          └────────┬─────────┘
                                                             │                   │ 200 {otpRequired}
                                                             │                   ▼
                                                             │        ┌──────────────────────┐
                                                             │        │ POST /otp/verify     │
                                                             │        │ match hash, attempts │
                                                             │        └───────┬──────────────┘
                                                             │           ok   │
                                                             ▼◀───────────────┘
                                                   ┌───────────────────┐
                                                   │ 200 {accessToken, │
                                                   │ refreshToken}     │
                                                   └───────────────────┘
```

---

## 6) الحماية من القوة الغاشمة و Rate Limiting

| الطبقة | الإعداد | الأداة |
|--------|---------|--------|
| Account Lockout | 5 محاولات فاشلة ⇒ قفل 5 دقائق، ثم 15، ثم 30 (متصاعد) | ASP.NET Identity `LockoutEnd` |
| IP Rate Limiting | 10 محاولات login/دقيقة لكل IP ⇒ `429` | .NET 9 `RateLimiter` (Fixed/Sliding Window) |
| Per-Account Throttle | تأخير تصاعدي (exponential backoff) بعد كل فشل | Middleware |
| OTP Throttle | رمز/60ث، 5 محاولات تحقّق كحدّ أقصى | `OtpCodes.AttemptCount` |
| CAPTCHA | يُفعَّل بعد 3 محاولات فاشلة من نفس IP | reCAPTCHA/Turnstile |
| Password Reset | لا كشف للبريد، وحدّ 3 طلبات/ساعة | Rate Limiter + رسالة موحّدة |

```csharp
// .NET 9 Rate Limiting لنقاط المصادقة
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("auth", opt => {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
        opt.QueueLimit = 0;
    });
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
// app.MapControllers()… [EnableRateLimiting("auth")] على AuthController
```

---

## 7) تخزين كلمات المرور (Password Storage)

- **الخوارزمية:** ASP.NET Identity `PasswordHasher<TUser>` — الافتراضي `IdentityV3` = **PBKDF2-HMAC-SHA512**، 100,000 تكرار، salt عشوائي 128-bit، مخرجات 256-bit.
- **صيغة التخزين:** بايت واحد للنسخة + معلومات KDF + salt + subkey (Base64 في `PasswordHash`).
- **سياسة كلمة المرور:** 8 خانات كحدّ أدنى، حرف كبير + صغير + رقم + رمز، فحص مقابل قائمة كلمات مسرّبة (HaveIBeenPwned k-anonymity اختياري).
- **الترقية التلقائية:** عند نجاح تسجيل الدخول، إن كانت النسخة قديمة يُعيد Identity الـ hashing بالمعايير الحالية (`NeedsRehash`).
- **ممنوع:** MD5/SHA1 المجرّد، أو تخزين نصّ صريح، أو salt ثابت مشترك.

---

## 8) الأخطاء المعيارية (Standard Auth Errors)

| الحالة | HTTP | الرمز | ملاحظة |
|--------|------|-------|--------|
| بيانات خاطئة | 401 | `AUTH_INVALID_CREDENTIALS` | رسالة عامة موحّدة |
| حساب مقفول | 423 | `AUTH_ACCOUNT_LOCKED` | مع `Retry-After` |
| حساب معطّل | 403 | `AUTH_ACCOUNT_DISABLED` | `IsActive = 0` |
| بريد غير مؤكّد | 403 | `AUTH_EMAIL_NOT_CONFIRMED` | حسب سياسة المستأجر |
| OTP خاطئ/منتهٍ | 400 | `AUTH_OTP_INVALID` | مع عدّاد المحاولات |
| رمز تدوير مُعاد | 401 | `AUTH_REFRESH_REUSE` | إبطال السلسلة كلّها |
| تجاوز المعدّل | 429 | `AUTH_RATE_LIMITED` | مع `Retry-After` |

---

## 9) سجل التدقيق (Auth Audit)

كل حدث مصادقة يُسجَّل في `AuditLogs` (انظر [27-Security.md](27-Security.md)): `LOGIN_SUCCESS`, `LOGIN_FAILED`, `LOCKOUT`, `LOGOUT`, `PASSWORD_RESET`, `OTP_SENT`, `OTP_VERIFIED`, `REFRESH_ROTATED`, `REFRESH_REUSE_DETECTED` — مع `UserId`, `TenantId`, `IP`, `UserAgent`, `Timestamp (UTC)`. تُستخدم لكشف الأنماط المشبوهة وتنبيهات SIEM.

---

_يلتزم هذا الملف بمعايير [04-Database-Design.md](04-Database-Design.md). التفويض (Authorization) في [06-Roles-And-Permissions.md](06-Roles-And-Permissions.md)._
