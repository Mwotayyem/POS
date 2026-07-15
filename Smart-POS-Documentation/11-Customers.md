# 11 — Customers (العملاء، حدود الائتمان، نقاط الولاء)

> وحدة العملاء تدير بيانات العملاء، **حدّ الائتمان (Credit Limit)** الذي يمنع البيع الآجل عند تجاوزه، **نقاط الولاء (Loyalty Points)** بآلية احتساب وصرف، سجلّ المشتريات، والرصيد المستحق على العميل (Receivable). الرصيد الموجب = مبلغ **يدين به العميل لنا (مدين)**.

---

## 1) الهدف من الصفحة (Purpose)

- إدارة قائمة العملاء (إنشاء/تعديل/أرشفة).
- ضبط **حدّ الائتمان** ومراقبة الرصيد المستحق ومنع تجاوز الحد.
- إدارة **نقاط الولاء**: كسب عند البيع، صرف كخصم، انتهاء صلاحية.
- عرض سجلّ المشتريات، المدفوعات، وكشف الحساب.

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية | الوصف |
|----------|-------|
| `customers.view` | عرض العملاء والأرصدة |
| `customers.create` | إضافة عميل |
| `customers.edit` | تعديل عميل |
| `customers.delete` | أرشفة عميل |
| `customers.creditlimit.edit` | تعديل حدّ الائتمان (حسّاس) |
| `customers.payment.create` | تسجيل دفعة من عميل |
| `customers.loyalty.manage` | تعديل/تسوية نقاط الولاء يدوياً |

---

## 3) تصميم الصفحة (Page Layout)

```
┌──────────────────────────────────────────────────────────────┐
│  Customers                       [ + New Customer ] [Export ▾]│
├──────────────────────────────────────────────────────────────┤
│  [ Search: name / phone / card# ]     [Group ▾]  Total: 1,204 │
├──────────────────────────────────────────────────────────────┤
│  Code   │ Name        │ Phone      │ Balance │ Credit │ Points │
│  CUS-01 │ Sami Ali    │ 077xxxxxxx │ 120.000 │ 500.00 │  340   │
│  CUS-02 │ Rana Store  │ 079xxxxxxx │ 480.000 │ 500.00 │ 1,120  │
├──────────────────────────────────────────────────────────────┤
│  Tabs: Profile · Purchases · Payments · Loyalty · Statement   │
└──────────────────────────────────────────────────────────────┘
```

---

## 4) جميع الأزرار (Buttons)

| الزر | الوظيفة | الصلاحية |
|------|---------|----------|
| **+ New Customer** | نموذج إنشاء | create |
| **Save** | حفظ | create/edit |
| **✎ Edit** | تعديل | edit |
| **🗑 Delete** | أرشفة (بشروط) | delete |
| **💵 Add Payment** | تسجيل تحصيل | payment.create |
| **⭐ Adjust Points** | تسوية نقاط يدوية | loyalty.manage |
| **👁 Statement** | كشف حساب | view |

---

## 5) جميع الحقول (Fields)

### Profile
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|:------:|---------|
| `Name` | نص (200) | ✔ | اسم العميل |
| `Code` | نص (30) | — | فريد داخل المستأجر (auto إن تُرك) |
| `Phone` | نص (30) | — | هاتف (يُستخدم للبحث في POS) |
| `Email` | نص (150) | — | بريد |
| `TaxNumber` | نص (50) | — | الرقم الضريبي (للفواتير الضريبية) |
| `Address` | نص | — | العنوان |
| `CustomerGroupId` | قائمة | — | مجموعة (تسعير/خصم) |
| `LoyaltyCardNumber` | نص (40) | — | رقم بطاقة الولاء |
| `OpeningBalance` | DECIMAL(18,4) | — | رصيد افتتاحي مدين |
| `CreditLimit` | DECIMAL(18,4) | — | 0 = نقدي فقط (لا آجل) |
| `IsActive` | Boolean | ✔ | افتراضي `true` |

### حقول محسوبة (Read-only)
| الحقل | المصدر |
|-------|--------|
| `CurrentBalance` | محسوب (Receivable) |
| `AvailableCredit` | `CreditLimit − CurrentBalance` |
| `LoyaltyPointsBalance` | مجموع حركات الولاء الصالحة |

---

## 6) التحقق (Validation)

- `Name` مطلوب (2..200).
- `Code` / `LoyaltyCardNumber` فريدان داخل المستأجر إن أُدخلا.
- `Email` بصيغة صحيحة إن أُدخل.
- `CreditLimit >= 0`.
- منع الحذف إذا كان `CurrentBalance <> 0` أو توجد فواتير آجلة مفتوحة.
- تسوية النقاط اليدوية تتطلّب سبباً (`Reason`) وصلاحية `customers.loyalty.manage`.

---

## 7) قواعد العمل (Business Rules)

### 7.1 حساب الرصيد (Receivable)
```
CurrentBalance =
      OpeningBalance
    + Σ (SalesInvoices.NetTotal   حيث IsPosted=1 AND PaymentType=Credit)
    − Σ (CustomerPayments.Amount  حيث IsDeleted=0)
    − Σ (SalesReturns.RefundToBalance)
```
- الموجب = العميل مدين لنا. يُخزَّن `CachedBalance` ويُحدَّث ذرّياً ضمن كل حركة.

### 7.2 حدّ الائتمان (Credit Limit) — منع البيع عند التجاوز
- قبل ترحيل فاتورة **آجلة**، يُفحص:
  `CurrentBalance + InvoiceNetTotal <= CreditLimit`.
- إن تجاوز → يُرفض الترحيل بـ `409 CREDIT_LIMIT_EXCEEDED`، ما لم يملك المستخدم صلاحية تجاوز (`sales.override.creditlimit`) فيُسمح مع تسجيل تدقيق.
- `CreditLimit = 0` يعني **لا بيع آجل** (نقدي فقط).
- الفحص يتم **داخل معاملة الفاتورة** لتفادي سباق يسمح بتجاوز الحد عبر فاتورتين متزامنتين.

### 7.3 نقاط الولاء (Loyalty) — الاحتساب والصرف
- **الكسب (Earn):** لكل عملة مُنفَقة تُمنح نقاط حسب معدّل قابل للإعداد (مثال: `1 point لكل 1.000 من صافي الفاتورة`). تُسجَّل حركة `Earn` مرتبطة بالفاتورة بعد ترحيلها.
- **الصرف (Redeem):** يمكن استبدال النقاط بخصم حسب معدّل تحويل (مثال: `100 point = 1.000 خصم`)، بحدّ أقصى قابل للإعداد لكل فاتورة. تُسجَّل حركة `Redeem` بقيمة سالبة.
- **الرصيد:** `LoyaltyPointsBalance = Σ(Earn) − Σ(Redeem) − Σ(Expired) ± Σ(Adjust)`.
- **الانتهاء (Expiry):** نقاط الكسب لها `ExpiryDate` (مثل بعد 12 شهراً)؛ مهمّة Hangfire ليلية تُنشئ حركات `Expire` للنقاط المنتهية (FIFO — الأقدم أولاً).
- **الإلغاء عند المرتجع:** إرجاع فاتورة يعكس نقاط الكسب المرتبطة بها (حركة `Reverse`).

---

## 8) جداول قاعدة البيانات (Database Tables)

### 8.1 Customers

```sql
CREATE TABLE [dbo].[Customers]
(
    [Name]               NVARCHAR(200) NOT NULL,
    [Code]               NVARCHAR(30)  NULL,
    [Phone]              NVARCHAR(30)  NULL,
    [Email]              NVARCHAR(150) NULL,
    [NationalId]         NVARCHAR(30)  NULL,        -- الرقم الوطني/الهوية
    [TaxNumber]          NVARCHAR(50)  NULL,        -- الرقم الضريبي
    [Address]            NVARCHAR(500) NULL,
    [City]               NVARCHAR(100) NULL,
    [Country]            NVARCHAR(100) NULL,
    [CustomerGroupId]    BIGINT        NULL,
    [LoyaltyCardNumber]  NVARCHAR(40)  NULL,
    [OpeningBalance]     DECIMAL(18,4) NOT NULL CONSTRAINT DF_Customers_Opening DEFAULT (0),
    [CachedBalance]      DECIMAL(18,4) NOT NULL CONSTRAINT DF_Customers_Cached DEFAULT (0),
    [CreditLimit]        DECIMAL(18,4) NOT NULL CONSTRAINT DF_Customers_Credit DEFAULT (0),
    [LoyaltyPoints]      DECIMAL(18,4) NOT NULL CONSTRAINT DF_Customers_Points DEFAULT (0), -- رصيد نقاط مخزَّن (Cache)
    [IsActive]           BIT           NOT NULL CONSTRAINT DF_Customers_Active DEFAULT (1),
    [PublicId]           UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Customers_PublicId DEFAULT (NEWID()),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_Customers_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_Customers_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_Customers] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Customers_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_Customers_Store]  FOREIGN KEY ([StoreId])  REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_Customers_Group]  FOREIGN KEY ([CustomerGroupId]) REFERENCES [dbo].[CustomerGroups]([Id]),
    CONSTRAINT [CK_Customers_Credit] CHECK ([CreditLimit] >= 0)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Customers_Tenant_Code]
    ON [dbo].[Customers] ([TenantId], [Code]) WHERE [IsDeleted] = 0 AND [Code] IS NOT NULL;
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Customers_Tenant_Card]
    ON [dbo].[Customers] ([TenantId], [LoyaltyCardNumber]) WHERE [IsDeleted] = 0 AND [LoyaltyCardNumber] IS NOT NULL;
GO
CREATE NONCLUSTERED INDEX [IX_Customers_Tenant_Phone]
    ON [dbo].[Customers] ([TenantId], [Phone]) WHERE [IsDeleted] = 0;
GO
```

### 8.2 CustomerPayments

```sql
CREATE TABLE [dbo].[CustomerPayments]
(
    [CustomerId]      BIGINT        NOT NULL,
    [SalesInvoiceId]  BIGINT        NULL,          -- تحصيل مرتبط بفاتورة (اختياري)
    [PaymentNumber]   NVARCHAR(30)  NOT NULL,       -- من Sequences
    [PaymentDate]     DATETIME2(3)  NOT NULL CONSTRAINT DF_CusPay_Date DEFAULT (SYSUTCDATETIME()),
    [Amount]          DECIMAL(18,4) NOT NULL,
    [PaymentMethod]   TINYINT       NOT NULL,       -- 1=Cash,2=Card,3=Transfer,4=Cheque
    [ReferenceNumber] NVARCHAR(80)  NULL,
    [Notes]           NVARCHAR(500) NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_CusPay_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_CusPay_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_CustomerPayments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_CusPay_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers]([Id]),
    CONSTRAINT [FK_CusPay_Invoice]  FOREIGN KEY ([SalesInvoiceId]) REFERENCES [dbo].[SalesInvoices]([Id]),
    CONSTRAINT [FK_CusPay_Tenant]   FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [CK_CusPay_Amount] CHECK ([Amount] > 0)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_CusPay_Tenant_Number]
    ON [dbo].[CustomerPayments] ([TenantId], [PaymentNumber]) WHERE [IsDeleted] = 0;
GO
CREATE NONCLUSTERED INDEX [IX_CusPay_Customer_Date]
    ON [dbo].[CustomerPayments] ([CustomerId], [PaymentDate] DESC) WHERE [IsDeleted] = 0;
GO
```

### 8.3 LoyaltyTransactions

```sql
CREATE TABLE [dbo].[LoyaltyTransactions]
(
    [CustomerId]     BIGINT        NOT NULL,
    [TxnType]        TINYINT       NOT NULL,   -- 1=Earn,2=Redeem,3=Expire,4=Adjust,5=Reverse
    [Points]         DECIMAL(18,4) NOT NULL,   -- موجبة للكسب/التسوية+، سالبة للصرف/الانتهاء
    [SourceType]     VARCHAR(30)   NULL,       -- 'SALES_INVOICE','MANUAL','RETURN'
    [SourceId]       BIGINT        NULL,        -- SalesInvoiceId مثلاً
    [MonetaryValue]  DECIMAL(18,4) NULL,        -- قيمة الخصم عند الصرف
    [ExpiryDate]     DATE          NULL,        -- تُملأ لحركات Earn فقط
    [BalanceAfter]   DECIMAL(18,4) NOT NULL,    -- رصيد النقاط بعد الحركة (تدقيق)
    [Reason]         NVARCHAR(300) NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_Loyalty_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_Loyalty_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_LoyaltyTransactions] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Loyalty_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers]([Id]),
    CONSTRAINT [FK_Loyalty_Tenant]   FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_Loyalty_Customer_Date]
    ON [dbo].[LoyaltyTransactions] ([CustomerId], [CreatedDate] DESC) WHERE [IsDeleted] = 0;
GO
-- فهرس لمعالجة انتهاء الصلاحية (Earn غير المستهلكة قبل تاريخ)
CREATE NONCLUSTERED INDEX [IX_Loyalty_Expiry]
    ON [dbo].[LoyaltyTransactions] ([TenantId], [ExpiryDate]) WHERE [TxnType] = 1 AND [IsDeleted] = 0;
GO
```

---

## 9) الـ API

**Base:** `/api/v1/customers`

| Method | Endpoint | الوصف | Policy |
|--------|----------|-------|--------|
| GET | `/customers` | قائمة + رصيد + نقاط | `customers.view` |
| GET | `/customers/{id}` | تفاصيل عميل | `customers.view` |
| GET | `/customers/{id}/statement?from=&to=` | كشف حساب | `customers.view` |
| GET | `/customers/{id}/loyalty` | سجلّ نقاط الولاء | `customers.view` |
| POST | `/customers` | إنشاء | `customers.create` |
| PUT | `/customers/{id}` | تعديل | `customers.edit` |
| PATCH | `/customers/{id}/credit-limit` | تعديل الحد | `customers.creditlimit.edit` |
| POST | `/customers/{id}/payments` | تحصيل | `customers.payment.create` |
| POST | `/customers/{id}/loyalty/redeem` | صرف نقاط | `customers.loyalty.manage` |
| POST | `/customers/{id}/loyalty/adjust` | تسوية يدوية | `customers.loyalty.manage` |

### مثال: POST /customers/55/loyalty/redeem (Request/Response)

```json
// Request — صرف 500 نقطة على فاتورة
{ "points": 500, "salesInvoiceId": 90210 }

// Response 200
{
  "customerId": 55,
  "redeemedPoints": 500,
  "discountValue": 5.0000,
  "pointsBalanceAfter": 620,
  "txnId": 14877
}
```

### مثال: خطأ تجاوز حدّ الائتمان (من فاتورة آجلة)

```json
// Response 409
{
  "error": "CREDIT_LIMIT_EXCEEDED",
  "message": "Customer credit limit exceeded.",
  "creditLimit": 500.0000,
  "currentBalance": 480.0000,
  "attemptedInvoiceTotal": 75.0000,
  "availableCredit": 20.0000
}
```

---

## 10) مخطط التدفّق — فحص حدّ الائتمان عند البيع الآجل

```
 [POST Sales Invoice (Credit)]
            │
            ▼
 [BEGIN TRANSACTION]
   │
   ▼
 [Read Customer.CachedBalance + CreditLimit  (UPDLOCK)]
   │
   ▼
 [ (Balance + InvoiceTotal) <= CreditLimit ? ]
   │                         │
  yes                        no
   │                         │
   ▼                         ▼
 [Post invoice]     [ Has override perm? ]
   │                    │           │
   │                   yes          no
   │                    │           │
   ▼                    ▼           ▼
 [Update Balance]  [Post + audit  [ROLLBACK]
 [Earn loyalty]     override]      [409 CREDIT_LIMIT_EXCEEDED]
   │                    │
   └──────────┬─────────┘
              ▼
        [COMMIT] ──► [201]
```

---

## 11) ماذا يحدث عند: الحذف / التعديل / تغيير حدّ الائتمان / صرف النقاط

### الحذف (Customer)
- يُمنع إن كان `CachedBalance <> 0` أو للعميل فواتير آجلة مفتوحة → `409 CUSTOMER_HAS_BALANCE`.
- خلاف ذلك: Soft Delete. نقاط الولاء تبقى مؤرشفة (لا تُصرف).

### التعديل (Customer)
- تعديل `OpeningBalance` يعيد احتساب `CachedBalance` (حقل حسّاس، تدقيق).
- بقيّة الحقول تعديل عادي مع فحص `ConcurrencyStamp`.

### تغيير حدّ الائتمان (Credit Limit)
- عبر `PATCH /credit-limit` بصلاحية مستقلّة، ويُسجَّل القديم/الجديد في التدقيق.
- خفض الحدّ **لا يلغي فواتير آجلة قائمة**، لكنه يمنع فواتير جديدة تتجاوز الحد الجديد.

### صرف النقاط (Redeem) — والتراجع
- الصرف يُنشئ حركة `Redeem` سالبة ويطبّق الخصم على الفاتورة داخل نفس Transaction.
- إلغاء الفاتورة أو إرجاعها يعكس: حركة `Reverse` تُعيد النقاط المصروفة، وتُلغى نقاط الكسب المرتبطة.
- النقاط المنتهية (`Expire`) لا يمكن استرجاعها.

---

## 12) سجل التدقيق (Audit Log)

| الحدث | التسجيل |
|-------|---------|
| Create/Edit Customer | diff الحقول |
| Change CreditLimit | قديم/جديد + المستخدم |
| Add Payment | `Action=CustomerPayment, amount, method` |
| Loyalty Earn/Redeem/Expire/Adjust | نوع الحركة، النقاط، الرصيد بعدها، المرجع |
| Credit Limit Override | من تجاوز الحد ومقدار التجاوز |

---

## 13) الأخطاء المحتملة (Possible Errors)

| الكود | السبب |
|-------|-------|
| `409 CREDIT_LIMIT_EXCEEDED` | فاتورة آجلة تتجاوز الحد |
| `409 CODE_DUPLICATE` / `CARD_DUPLICATE` | رمز/بطاقة مكرّرة |
| `400 INSUFFICIENT_POINTS` | صرف نقاط أكثر من الرصيد |
| `400 REDEEM_LIMIT_EXCEEDED` | تجاوز حدّ الصرف لكل فاتورة |
| `409 CUSTOMER_HAS_BALANCE` | حذف عميل له رصيد |
| `409 CONCURRENCY_CONFLICT` | تعارض تعديل الرصيد/النقاط |

---

## 14) الأداء (Performance)

- `CachedBalance` و`LoyaltyPoints` مخزَّنان لتفادي حساب الحركات في كل عرض.
- بحث POS عبر `Phone`/`LoyaltyCardNumber` على فهارس فريدة → seek فوري.
- معالجة انتهاء النقاط دفعة ليلية (Batch) عبر Hangfire باستخدام فهرس `IX_Loyalty_Expiry`.
- كشف الحساب باستعلام مُجمَّع مفهرس على `(CustomerId, Date)`.
- إعادة مطابقة (Reconcile) دورية للتأكد من تطابق الأرصدة المخزَّنة مع الـ Ledger.

---

## 15) الأمان (Security)

- `TenantId` من الـ Token؛ لا يُقبل من العميل (منع IDOR/تسريب بين المستأجرين).
- تعديل حدّ الائتمان، تجاوزه، وتسوية النقاط اليدوية صلاحيات حسّاسة تُدقَّق دائماً.
- فحص حدّ الائتمان وتحديث الرصيد/النقاط داخل Transaction مع `UPDLOCK` و`ConcurrencyStamp` لمنع سباق يسمح بتجاوز الحد عبر عمليات متزامنة.
- نقاط الولاء تُحتسب خادمياً فقط بناءً على إعدادات المستأجر، ولا تُقبل قيمها من العميل.
