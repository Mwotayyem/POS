# 10 — Suppliers (الموردون)

> وحدة الموردين تدير بيانات الموردين، رصيدهم المالي (Balance)، سجلّ المشتريات منهم، مدفوعاتهم، ومرتجعات الشراء إليهم. الرصيد يُحسب من مجموع فواتير الشراء ناقص المدفوعات ناقص المرتجعات، ويُعامَل الرصيد الموجب كـ **دائن (نحن ندين للمورّد)**.

---

## 1) الهدف من الصفحة (Purpose)

- إدارة قائمة الموردين (إنشاء/تعديل/أرشفة) على مستوى المستأجر.
- عرض **رصيد المورّد** الحالي وكشف حساب تفصيلي (Statement).
- تسجيل مدفوعات للموردين وربطها بالفواتير.
- عرض سجلّ المشتريات والمرتجعات لكل مورّد.

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية | الوصف |
|----------|-------|
| `suppliers.view` | عرض الموردين والأرصدة |
| `suppliers.create` | إضافة مورّد |
| `suppliers.edit` | تعديل مورّد |
| `suppliers.delete` | أرشفة مورّد |
| `suppliers.payment.create` | تسجيل دفعة لمورّد |
| `suppliers.statement.view` | عرض كشف الحساب |

---

## 3) تصميم الصفحة (Page Layout)

```
┌──────────────────────────────────────────────────────────────┐
│  Suppliers                       [ + New Supplier ] [Export ▾]│
├──────────────────────────────────────────────────────────────┤
│  [ Search: name / phone / tax# ]      [Status ▾]  Total: 42   │
├──────────────────────────────────────────────────────────────┤
│  Code   │ Name           │ Phone       │ Balance   │ Actions   │
│  SUP-01 │ Al-Noor Co.    │ 077xxxxxxx  │ 1,250.000 │ 👁 ✎ 💵    │
│  SUP-02 │ Gulf Import    │ 079xxxxxxx  │     0.000 │ 👁 ✎ 💵    │
├──────────────────────────────────────────────────────────────┤
│  Tabs (in details): Profile · Purchases · Payments · Returns  │
└──────────────────────────────────────────────────────────────┘
```

---

## 4) جميع الأزرار (Buttons)

| الزر | الوظيفة | الصلاحية |
|------|---------|----------|
| **+ New Supplier** | نموذج إنشاء | create |
| **Save** | حفظ | create/edit |
| **✎ Edit** | تعديل | edit |
| **🗑 Delete** | أرشفة (بشروط الرصيد) | delete |
| **💵 Add Payment** | تسجيل دفعة | payment.create |
| **👁 Statement** | كشف حساب PDF/عرض | statement.view |
| **Export ▾** | تصدير القائمة | view |

---

## 5) جميع الحقول (Fields)

### Profile
| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|:------:|---------|
| `Name` | نص (200) | ✔ | اسم المورّد |
| `Code` | نص (30) | — | رمز فريد داخل المستأجر (يُولَّد تلقائياً إن تُرك) |
| `Phone` | نص (30) | — | هاتف |
| `Email` | نص (150) | — | بريد |
| `TaxNumber` | نص (50) | — | الرقم الضريبي |
| `Address` | نص | — | العنوان |
| `ContactPerson` | نص (100) | — | مسؤول التواصل |
| `PaymentTermDays` | INT | — | مهلة السداد (Net 30 ...) |
| `OpeningBalance` | DECIMAL(18,4) | — | رصيد افتتاحي (دائن للمورّد) |
| `IsActive` | Boolean | ✔ | افتراضي `true` |

### حقول محسوبة (Read-only)
| الحقل | المصدر |
|-------|--------|
| `CurrentBalance` | محسوب (انظر §7) |
| `TotalPurchases` | مجموع فواتير الشراء |
| `TotalPaid` | مجموع المدفوعات |

---

## 6) التحقق (Validation)

- `Name` مطلوب (2..200).
- `Code` فريد داخل المستأجر إن أُدخل.
- `Email` بصيغة صحيحة إن أُدخل.
- `PaymentTermDays >= 0`.
- منع الحذف إذا كان `CurrentBalance <> 0` أو للمورّد فواتير مفتوحة.

---

## 7) حساب الرصيد وقواعده (Balance Calculation)

**قاعدة الإشارة:** الرصيد الموجب = مبلغ ندينه للمورّد (Payable / دائن).

```
CurrentBalance =
      OpeningBalance
    + Σ (PurchaseInvoices.NetTotal          حيث IsPosted=1)
    − Σ (SupplierPayments.Amount            حيث IsDeleted=0)
    − Σ (PurchaseReturns.NetTotal           حيث IsPosted=1)
```

- **فاتورة شراء (Posted):** ترفع الرصيد (نزداد ديناً).
- **دفعة للمورّد:** تخفض الرصيد.
- **مرتجع شراء:** يخفض الرصيد (لأننا أعدنا بضاعة → دَيننا يقلّ).
- الرصيد **لا يُخزَّن كمصدر وحيد للحقيقة**؛ يُحسب من الحركات (Ledger)، ويجوز الاحتفاظ بعمود مخزَّن `CachedBalance` يُحدَّث ضمن نفس Transaction لأغراض الأداء، مع إمكانية إعادة احتسابه (Reconcile) دورياً.
- كل حركة تُغلَّف في Transaction تُحدِّث `CachedBalance` ذرّياً لتفادي التعارض.

---

## 8) جداول قاعدة البيانات (Database Tables)

### 8.1 Suppliers

```sql
CREATE TABLE [dbo].[Suppliers]
(
    [Name]            NVARCHAR(200) NOT NULL,
    [Code]            NVARCHAR(30)  NULL,
    [Phone]           NVARCHAR(30)  NULL,
    [Email]           NVARCHAR(150) NULL,
    [TaxNumber]       NVARCHAR(50)  NULL,
    [Address]         NVARCHAR(500) NULL,
    [City]            NVARCHAR(100) NULL,
    [Country]         NVARCHAR(100) NULL,
    [IBAN]            NVARCHAR(34)  NULL,        -- رقم الآيبان للتحويل البنكي
    [BankName]        NVARCHAR(150) NULL,        -- اسم البنك
    [ContactPerson]   NVARCHAR(100) NULL,
    [PaymentTermDays] INT           NOT NULL CONSTRAINT DF_Suppliers_Term DEFAULT (0),
    [OpeningBalance]  DECIMAL(18,4) NOT NULL CONSTRAINT DF_Suppliers_Opening DEFAULT (0),
    [CachedBalance]   DECIMAL(18,4) NOT NULL CONSTRAINT DF_Suppliers_Cached DEFAULT (0),
    [IsActive]        BIT           NOT NULL CONSTRAINT DF_Suppliers_Active DEFAULT (1),
    [PublicId]        UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Suppliers_PublicId DEFAULT (NEWID()),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_Suppliers_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_Suppliers_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_Suppliers] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Suppliers_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_Suppliers_Store]  FOREIGN KEY ([StoreId])  REFERENCES [dbo].[Stores]([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Suppliers_Tenant_Code]
    ON [dbo].[Suppliers] ([TenantId], [Code]) WHERE [IsDeleted] = 0 AND [Code] IS NOT NULL;
GO
CREATE NONCLUSTERED INDEX [IX_Suppliers_Tenant_Name]
    ON [dbo].[Suppliers] ([TenantId], [Name]) WHERE [IsDeleted] = 0;
GO
```

### 8.2 SupplierPayments

```sql
CREATE TABLE [dbo].[SupplierPayments]
(
    [SupplierId]         BIGINT        NOT NULL,
    [PurchaseInvoiceId]  BIGINT        NULL,        -- دفعة مرتبطة بفاتورة (اختياري)
    [PaymentNumber]      NVARCHAR(30)  NOT NULL,     -- من Sequences
    [PaymentDate]        DATETIME2(3)  NOT NULL CONSTRAINT DF_SupPay_Date DEFAULT (SYSUTCDATETIME()),
    [Amount]             DECIMAL(18,4) NOT NULL,
    [PaymentMethod]      TINYINT       NOT NULL,     -- 1=Cash,2=Card,3=Transfer,4=Cheque
    [ReferenceNumber]    NVARCHAR(80)  NULL,         -- رقم الشيك/الحوالة
    [Notes]              NVARCHAR(500) NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_SupPay_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_SupPay_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_SupplierPayments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SupPay_Supplier] FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Suppliers]([Id]),
    CONSTRAINT [FK_SupPay_Invoice]  FOREIGN KEY ([PurchaseInvoiceId]) REFERENCES [dbo].[PurchaseInvoices]([Id]),
    CONSTRAINT [FK_SupPay_Tenant]   FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [CK_SupPay_Amount] CHECK ([Amount] > 0)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_SupPay_Tenant_Number]
    ON [dbo].[SupplierPayments] ([TenantId], [PaymentNumber]) WHERE [IsDeleted] = 0;
GO
CREATE NONCLUSTERED INDEX [IX_SupPay_Supplier_Date]
    ON [dbo].[SupplierPayments] ([SupplierId], [PaymentDate] DESC) WHERE [IsDeleted] = 0;
GO
```

---

## 9) الـ API

**Base:** `/api/v1/suppliers`

| Method | Endpoint | الوصف | Policy |
|--------|----------|-------|--------|
| GET | `/suppliers` | قائمة الموردين + الرصيد | `suppliers.view` |
| GET | `/suppliers/{id}` | تفاصيل مورّد | `suppliers.view` |
| GET | `/suppliers/{id}/statement?from=&to=` | كشف حساب | `suppliers.statement.view` |
| POST | `/suppliers` | إنشاء | `suppliers.create` |
| PUT | `/suppliers/{id}` | تعديل | `suppliers.edit` |
| DELETE | `/suppliers/{id}` | أرشفة | `suppliers.delete` |
| POST | `/suppliers/{id}/payments` | تسجيل دفعة | `suppliers.payment.create` |

### مثال: POST /suppliers/12/payments (Request)

```json
{
  "amount": 500.0000,
  "paymentMethod": "Transfer",
  "referenceNumber": "TRF-99812",
  "purchaseInvoiceId": 3345,
  "notes": "Partial payment of PUR-000120"
}
```

### Response (201)

```json
{
  "id": 781,
  "paymentNumber": "PAY-000781",
  "supplierId": 12,
  "amount": 500.0000,
  "supplierNewBalance": 750.0000,
  "paymentDate": "2026-07-13T11:20:00.000Z"
}
```

### مثال: GET /suppliers/12/statement (Response)

```json
{
  "supplierId": 12,
  "openingBalance": 0.0000,
  "lines": [
    { "date": "2026-06-01", "type": "PurchaseInvoice", "ref": "PUR-000120", "debit": 0, "credit": 1250.0000, "balance": 1250.0000 },
    { "date": "2026-07-13", "type": "Payment", "ref": "PAY-000781", "debit": 500.0000, "credit": 0, "balance": 750.0000 }
  ],
  "closingBalance": 750.0000
}
```

---

## 10) مخطط التدفّق — تسجيل دفعة (Add Payment)

```
 [POST /{id}/payments]
        │
        ▼
 [Validate amount > 0]
        │
        ▼
 [BEGIN TRANSACTION]
   ├─ Reserve PaymentNumber from Sequences (atomic UPDATE...OUTPUT)
   ├─ INSERT SupplierPayments
   ├─ UPDATE Suppliers.CachedBalance -= Amount
   ├─ (optional) allocate to PurchaseInvoice.PaidAmount
   └─ INSERT AuditLog
 [COMMIT] ──► [201 Created + newBalance]
```

---

## 11) ماذا يحدث عند: الحذف / التعديل / إلغاء دفعة

### الحذف (Supplier)
- يُمنع إن كان `CachedBalance <> 0` أو توجد فواتير مفتوحة → `409 SUPPLIER_HAS_BALANCE`.
- خلاف ذلك: Soft Delete.

### التعديل (Supplier)
- تعديل `OpeningBalance` **يعيد احتساب** `CachedBalance` ضمن Transaction ويُسجَّل في التدقيق (حقل حسّاس).
- بقيّة الحقول تعديل عادي مع فحص `ConcurrencyStamp`.

### إلغاء/حذف دفعة (Payment)
- Soft Delete للدفعة **يعكس** أثرها: `Suppliers.CachedBalance += Amount` داخل Transaction.
- إن كانت الدفعة مخصّصة لفاتورة، يُخصم المبلغ من `PurchaseInvoice.PaidAmount` وتُعاد حالة الفاتورة.

---

## 12) سجل التدقيق (Audit Log)

| الحدث | التسجيل |
|-------|---------|
| Create/Edit Supplier | diff للحقول |
| Change OpeningBalance | حقل حسّاس + الرصيد قبل/بعد |
| Add Payment | `Action=SupplierPayment, amount, method, ref` |
| Delete Payment | `Action=ReversePayment` + الرصيد المعاد |

---

## 13) الأخطاء المحتملة (Possible Errors)

| الكود | السبب |
|-------|-------|
| `409 CODE_DUPLICATE` | رمز مورّد مكرّر |
| `400 AMOUNT_INVALID` | مبلغ دفعة ≤ 0 |
| `409 SUPPLIER_HAS_BALANCE` | حذف مورّد له رصيد/فواتير |
| `409 PAYMENT_EXCEEDS_BALANCE` | دفعة تتجاوز المستحق (تحذير قابل للتجاوز حسب الإعداد) |
| `409 CONCURRENCY_CONFLICT` | تعارض تعديل الرصيد |

---

## 14) الأداء (Performance)

- `CachedBalance` يتجنّب حساب الرصيد من كل الحركات في كل عرض قائمة.
- كشف الحساب يُبنى باستعلام مُجمَّع (UNION للفواتير/المدفوعات/المرتجعات) مرتّب بالتاريخ، مع فهرس `(SupplierId, Date)`.
- إعادة المطابقة (Reconcile) الليلية عبر Hangfire تتحقّق من تطابق `CachedBalance` مع الـ Ledger.
- قوائم الموردين مرقّمة و`AsNoTracking`.

---

## 15) الأمان (Security)

- `TenantId` من الـ Token؛ لا يُقبل من العميل.
- تعديل الرصيد الافتتاحي وحذف المدفوعات صلاحيات حسّاسة تُدقَّق دائماً.
- تحديث الرصيد داخل Transaction مع `ConcurrencyStamp` لمنع التلاعب بالتزامن (Race Condition).
- كشف الحساب لا يكشف بيانات مورّدي مستأجرين آخرين (Global Query Filter).
