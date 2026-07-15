# 18 — POS (نقطة البيع: الكاشير، الشفت، End Of Day)

> شاشة **نقطة البيع (Point of Sale)** هي قلب النظام التشغيلي في المتجر. تعمل بسرعة عالية، بلمسة/باركود، وتُصدر فواتير فورية مرتبطة بالمخزون والمحاسبة والشفت. هذا الملف يلتزم حرفياً بمعايير [04-Database-Design.md](04-Database-Design.md): الأعمدة المشتركة، `DECIMAL(18,4)` للأموال، `DATETIME2(3)` بتوقيت UTC، Soft Delete، عزل `TenantId`.

---

## 1) الهدف (Purpose)

توفير واجهة بيع سريعة (Touch/Barcode) للكاشير تُمكّنه من:

- إصدار فاتورة بيع فورية بأقل عدد نقرات (زمن استجابة مستهدف < 100ms لإضافة بند).
- البيع بالباركود، البحث السريع، البيع بالكيلو (Weight) وبالقطعة (Piece).
- تطبيق الخصومات والضريبة والعروض تلقائياً.
- المرتجع الفوري من نفس الشاشة.
- تعليق الفاتورة (Suspend) واستئنافها (Resume) لخدمة عميل آخر بينهما.
- استقبال الدفع النقدي، بالفيزا، أو المختلط (Split).
- إدارة **الشفت (Shift)**: فتح، بيع، إغلاق، جرد الكاش (Cash Count)، ثم **End Of Day**.
- التحكم بدرج النقد (Cash Drawer) وطباعة الإيصال الحراري.

**غير أهداف الشاشة:** إدارة المنتجات، الشراء، التقارير التحليلية المعمّقة (لها شاشاتها المستقلة).

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية (Permission Key) | الوصف |
|---------------------------|-------|
| `POS.Access` | فتح شاشة نقطة البيع |
| `POS.Shift.Open` | فتح شفت جديد |
| `POS.Shift.Close` | إغلاق الشفت وجرد الكاش |
| `POS.Sale.Create` | إصدار فاتورة بيع |
| `POS.Sale.Discount` | تطبيق خصم يدوي على البند/الفاتورة |
| `POS.Sale.Discount.Override` | تجاوز الحد الأقصى للخصم (Manager) |
| `POS.Return.Create` | تنفيذ مرتجع |
| `POS.Suspend` | تعليق/استئناف فاتورة |
| `POS.CashDrawer.Open` | فتح الدرج يدوياً (No Sale) |
| `POS.CashDrawer.PayIn` / `POS.CashDrawer.PayOut` | إيداع/سحب نقدي من الدرج |
| `POS.EndOfDay` | تنفيذ إقفال اليوم |
| `POS.Price.Override` | تعديل سعر البند يدوياً |

> كل صلاحية تُفحص عبر `[Authorize(Policy = "...")]` على مستوى الـ Endpoint، **بالإضافة** إلى فحص `TenantId` و `StoreId` من الـ Claims. الكاشير مقيّد بالفرع (`StoreId`) الذي فُتح فيه الشفت.

---

## 3) تصميم الصفحة (Page Layout)

```
┌───────────────────────────────────────────────────────────────────────────┐
│  Smart POS   | Store: فرع الرئيسي | Cashier: Ahmad | Shift #S-000123  🟢    │
├───────────────────────────────┬───────────────────────────────────────────┤
│  [ 🔍 Barcode / Search ...  ]  │   Cart (سلة الفاتورة)                     │
│                                │  ┌─────────────────────────────────────┐  │
│  ┌──────┐ ┌──────┐ ┌──────┐    │  │ # │ Item      │ Qty │ Price │ Total │  │
│  │ Cat1 │ │ Cat2 │ │ Cat3 │    │  ├───┼───────────┼─────┼───────┼───────┤  │
│  └──────┘ └──────┘ └──────┘    │  │ 1 │ حليب 1L   │  2  │ 1.00  │ 2.00  │  │
│  ┌────┐┌────┐┌────┐┌────┐      │  │ 2 │ تفاح /kg  │0.750│ 2.00  │ 1.50  │  │
│  │ P1 ││ P2 ││ P3 ││ P4 │      │  └───┴───────────┴─────┴───────┴───────┘  │
│  └────┘└────┘└────┘└────┘      │   Subtotal:            3.50               │
│  ┌────┐┌────┐┌────┐┌────┐      │   Discount:           -0.35               │
│  │ P5 ││ P6 ││ P7 ││ P8 │      │   Tax (16%):           0.50               │
│  └────┘└────┘└────┘└────┘      │   ─────────────────────────               │
│                                │   TOTAL:                3.65               │
├───────────────────────────────┴───────────────────────────────────────────┤
│ [Pay Cash] [Pay Card] [Split] [Suspend] [Resume] [Return] [Discount] [Void] │
│ [No Sale/Drawer] [Pay In] [Pay Out]           [Close Shift] [End Of Day]     │
└───────────────────────────────────────────────────────────────────────────┘
```

- **يسار:** شبكة التصنيفات/المنتجات المفضّلة + حقل الباركود/البحث (له الـ focus دائماً).
- **يمين:** سلة الفاتورة الحيّة + ملخّص المبالغ (Subtotal / Discount / Tax / Total).
- **أسفل:** شريط الأزرار السريعة (يدعم اختصارات لوحة المفاتيح F-Keys).

---

## 4) الأزرار (Buttons)

| الزر | الاختصار | الوظيفة |
|------|----------|---------|
| Pay Cash | F2 | فتح شاشة الدفع النقدي وحساب الباقي |
| Pay Card | F3 | دفع بالفيزا (POS Terminal / يدوي) |
| Split | F4 | دفع مختلط (نقد + بطاقة + ...) |
| Suspend | F6 | تعليق الفاتورة الحالية |
| Resume | F7 | استعراض واستئناف فاتورة معلّقة |
| Return | F8 | بدء عملية مرتجع (بحث بالفاتورة الأصلية) |
| Discount | F9 | خصم على بند أو على الفاتورة كاملة |
| Void Line | Del | حذف بند من السلة الحالية |
| No Sale | F10 | فتح الدرج بلا بيع (يُسجَّل بالتدقيق) |
| Pay In / Pay Out | — | إيداع/سحب نقدي من الدرج مع سبب |
| Close Shift | — | إغلاق الشفت وبدء جرد الكاش |
| End Of Day | — | إقفال اليوم بعد إغلاق كل الشفتات |

> كل زر عملياتي يُنفَّذ عبر API مع فحص الصلاحية والشفت المفتوح. زر **Pay** معطّل ما لم يكن هناك شفت مفتوح (`PosShifts.Status = 'Open'`).

---

## 5) الحقول (Fields)

| الحقل | النوع | مصدره | ملاحظات |
|-------|-------|-------|---------|
| Barcode | نص | إدخال/ماسح | يبحث في `ProductBarcodes` |
| Search Term | نص | إدخال | اسم/كود/باركود جزئي |
| Qty | رقم | إدخال/افتراضي 1 | للكيلو: عشري (0.750) |
| Weight | DECIMAL(18,3) | ميزان/يدوي | للمنتجات `SellByWeight = 1` |
| Unit Price | DECIMAL(18,4) | المنتج | قابل للتعديل بصلاحية `POS.Price.Override` |
| Line Discount | DECIMAL(18,4) / % | إدخال | بحدود قصوى من الإعدادات |
| Invoice Discount | DECIMAL(18,4) / % | إدخال | يُوزَّع على البنود للضريبة |
| Tax Rate | DECIMAL(9,4) | المنتج/الإعداد | 16% افتراضياً |
| Customer | بحث | `Customers` | اختياري (بيع نقدي = عميل نقدي افتراضي) |
| Tendered (المدفوع) | DECIMAL(18,4) | إدخال | لحساب الباقي |
| Change (الباقي) | DECIMAL(18,4) | محسوب | Tendered − Total |

---

## 6) التحقق (Validation)

- **Barcode:** يجب أن يوجد في `ProductBarcodes` ضمن نفس `TenantId` وغير محذوف؛ وإلا رسالة "منتج غير معروف".
- **Qty > 0** دائماً؛ للكيلو `Weight > 0` وبثلاث خانات عشرية كحد أقصى.
- **المخزون:** إن كان المنتج `TrackStock = 1` ومنع البيع بالسالب مفعّل (`AllowNegativeStock = 0`)، يُرفض تجاوز الرصيد المتاح في مستودع الفرع.
- **الخصم:** لا يتجاوز `MaxDiscountPercent` من الإعدادات إلا بصلاحية `POS.Sale.Discount.Override`.
- **الدفع:** `SUM(Payments) = Invoice.Total` (بهامش تقريب ±0.005) قبل الترحيل. الدفع النقدي يسمح بـ Tendered ≥ Total.
- **الشفت:** لا يُقبل أي بيع/مرتجع دون شفت `Open` مملوك لنفس الكاشير والفرع.
- **المرتجع:** الكمية المرتجعة ≤ (المُباعة − المرتجعة سابقاً) من الفاتورة الأصلية.

---

## 7) قواعد العمل (Business Rules)

1. **BR-01 — شفت واحد نشط لكل كاشير/جهاز:** لا يُسمح بفتح شفت جديد قبل إغلاق السابق على نفس `RegisterId`.
2. **BR-02 — رصيد الافتتاح (Opening Float):** يُدخَل عند فتح الشفت ويُثبَّت في `PosShifts.OpeningFloat`.
3. **BR-03 — الترحيل الذرّي:** إصدار الفاتورة يُغلَّف في معاملة واحدة (فاتورة + بنود + دفعات + حركة مخزون + حركة درج) عبر Unit of Work.
4. **BR-04 — رقم الفاتورة:** يُولَّد من `Sequences` (DocType='POS_SALE') ذرياً — لا يعتمد على `Id`.
5. **BR-05 — البيع بالوزن:** `LineTotal = Weight × UnitPricePerKg`؛ يُخزَّن الوزن الفعلي في البند.
6. **BR-06 — الضريبة:** تُحسب بعد الخصم (Discount-then-Tax) ما لم تُضبط الإعدادة على العكس. الضريبة تُخزَّن على مستوى البند.
7. **BR-07 — أثر الدرج:** كل دفعة نقدية (Cash) تولّد حركة `CashDrawerMovements` (Sale=+، Change=−، Refund=−).
8. **BR-08 — المرتجع النقدي** يخصم من الدرج ويُسجَّل كحركة `Refund`.
9. **BR-09 — لا حذف فعلي:** إلغاء بند من فاتورة **مرحّلة** = فاتورة مرتجع/تعديل، لا حذف صف.
10. **BR-10 — إغلاق الشفت** يتطلب جرد الكاش وحساب الفرق قبل السماح بـ End Of Day.

---

## 8) جداول قاعدة البيانات (Database Tables — SQL كامل)

> جميع الجداول ترث الأعمدة المشتركة من [04-Database-Design.md](04-Database-Design.md) (§2). ما يلي هو الـ SQL الكامل الفعلي على SQL Server 2022.

### 8.1) `PosShifts` — الشفتات

الشفت هو الفترة الممتدة من فتح الكاشير للصندوق حتى إغلاقه وجرده.

```sql
CREATE TABLE [dbo].[PosShifts]
(
    -- ===== الأعمدة الخاصة =====
    [ShiftNumber]      VARCHAR(30)  NOT NULL,   -- 'S-000123' من Sequences
    [RegisterId]       BIGINT       NOT NULL,   -- الصندوق/الجهاز
    [CashierUserId]    BIGINT       NOT NULL,   -- الكاشير الذي فتح الشفت
    [Status]           VARCHAR(15)  NOT NULL,   -- 'Open' | 'Closed' | 'Reconciled'
    [OpenedAt]         DATETIME2(3) NOT NULL,
    [ClosedAt]         DATETIME2(3) NULL,
    [OpeningFloat]     DECIMAL(18,4) NOT NULL CONSTRAINT DF_PosShifts_OpeningFloat DEFAULT (0),
    -- المتوقّع (Expected) يُحسب لحظة الإغلاق ويُثبَّت للتدقيق:
    [ExpectedCash]     DECIMAL(18,4) NULL,      -- OpeningFloat + CashSales - CashRefunds + PayIn - PayOut
    [CountedCash]      DECIMAL(18,4) NULL,      -- ما أدخله الكاشير بالجرد الفعلي
    [CashVariance]     DECIMAL(18,4) NULL,      -- CountedCash - ExpectedCash (+ زيادة / - عجز)
    [TotalSales]       DECIMAL(18,4) NOT NULL CONSTRAINT DF_PosShifts_TotalSales DEFAULT (0),
    [TotalReturns]     DECIMAL(18,4) NOT NULL CONSTRAINT DF_PosShifts_TotalReturns DEFAULT (0),
    [TotalCard]        DECIMAL(18,4) NOT NULL CONSTRAINT DF_PosShifts_TotalCard DEFAULT (0),
    [InvoiceCount]     INT           NOT NULL CONSTRAINT DF_PosShifts_InvoiceCount DEFAULT (0),
    [ClosingNotes]     NVARCHAR(500) NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NOT NULL,   -- الشفت دائماً مرتبط بفرع
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_PosShifts_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_PosShifts_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_PosShifts] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PosShifts_Tenant]   FOREIGN KEY ([TenantId])     REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_PosShifts_Store]    FOREIGN KEY ([StoreId])      REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_PosShifts_Cashier]  FOREIGN KEY ([CashierUserId]) REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT [CK_PosShifts_Status]   CHECK ([Status] IN ('Open','Closed','Reconciled'))
);
GO

-- شفت مفتوح واحد فقط لكل صندوق (Register) داخل المستأجر
CREATE UNIQUE NONCLUSTERED INDEX [UX_PosShifts_OpenRegister]
    ON [dbo].[PosShifts] ([TenantId], [RegisterId])
    WHERE [Status] = 'Open' AND [IsDeleted] = 0;
GO

CREATE NONCLUSTERED INDEX [IX_PosShifts_Tenant_Store]
    ON [dbo].[PosShifts] ([TenantId], [StoreId], [OpenedAt] DESC)
    WHERE [IsDeleted] = 0;
GO
```

### 8.2) `PosSessions` — جلسات اليوم (End Of Day)

الجلسة (Session) = يوم عمل واحد للفرع؛ تجمع عدّة شفتات وتُختَم بـ End Of Day.

```sql
CREATE TABLE [dbo].[PosSessions]
(
    [SessionNumber]    VARCHAR(30)  NOT NULL,   -- 'EOD-2026-07-13-001'
    [BusinessDate]     DATE         NOT NULL,   -- يوم العمل (قد يختلف عن التاريخ الفعلي)
    [Status]           VARCHAR(15)  NOT NULL,   -- 'Open' | 'Closed'
    [OpenedAt]         DATETIME2(3) NOT NULL,
    [ClosedAt]         DATETIME2(3) NULL,
    [ClosedByUserId]   BIGINT       NULL,
    [ShiftCount]       INT           NOT NULL CONSTRAINT DF_PosSessions_ShiftCount DEFAULT (0),
    [TotalSales]       DECIMAL(18,4) NOT NULL CONSTRAINT DF_PosSessions_TotalSales DEFAULT (0),
    [TotalReturns]     DECIMAL(18,4) NOT NULL CONSTRAINT DF_PosSessions_TotalReturns DEFAULT (0),
    [TotalCash]        DECIMAL(18,4) NOT NULL CONSTRAINT DF_PosSessions_TotalCash DEFAULT (0),
    [TotalCard]        DECIMAL(18,4) NOT NULL CONSTRAINT DF_PosSessions_TotalCard DEFAULT (0),
    [TotalVariance]    DECIMAL(18,4) NOT NULL CONSTRAINT DF_PosSessions_TotalVariance DEFAULT (0),

    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NOT NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_PosSessions_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_PosSessions_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_PosSessions] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PosSessions_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_PosSessions_Store]  FOREIGN KEY ([StoreId])  REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [CK_PosSessions_Status] CHECK ([Status] IN ('Open','Closed'))
);
GO

-- جلسة واحدة لكل فرع/يوم عمل
CREATE UNIQUE NONCLUSTERED INDEX [UX_PosSessions_Store_Date]
    ON [dbo].[PosSessions] ([TenantId], [StoreId], [BusinessDate])
    WHERE [IsDeleted] = 0;
GO
```

> ربط الشفت بالجلسة: يُضاف `SessionId BIGINT NULL` إلى `PosShifts` (FK إلى `PosSessions`) عند فتح الشفت ضمن جلسة اليوم النشطة.

### 8.3) `CashDrawerMovements` — حركات درج النقد

كل تدفّق نقدي داخل/خارج الدرج يُسجَّل هنا (مصدر الحقيقة لجرد الكاش).

```sql
CREATE TABLE [dbo].[CashDrawerMovements]
(
    [ShiftId]          BIGINT       NOT NULL,   -- ينتمي لشفت
    [MovementType]     VARCHAR(15)  NOT NULL,   -- 'OpeningFloat'|'Sale'|'Change'|'Refund'|'PayIn'|'PayOut'|'NoSale'
    [Amount]           DECIMAL(18,4) NOT NULL,  -- موجب دائماً؛ الاتجاه من Direction
    [Direction]        CHAR(1)      NOT NULL,   -- '+' داخل الدرج | '-' خارج الدرج
    [ReferenceType]    VARCHAR(20)  NULL,       -- 'SalesInvoice'|'SalesReturn'|null
    [ReferenceId]      BIGINT       NULL,       -- معرّف المستند المرتبط
    [Reason]           NVARCHAR(300) NULL,      -- سبب PayIn/PayOut/NoSale
    [OccurredAt]       DATETIME2(3) NOT NULL,

    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NOT NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_CashDrawerMovements_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_CashDrawerMovements_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_CashDrawerMovements] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_CDM_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_CDM_Store]  FOREIGN KEY ([StoreId])  REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_CDM_Shift]  FOREIGN KEY ([ShiftId])  REFERENCES [dbo].[PosShifts]([Id]),
    CONSTRAINT [CK_CDM_Type]   CHECK ([MovementType] IN ('OpeningFloat','Sale','Change','Refund','PayIn','PayOut','NoSale')),
    CONSTRAINT [CK_CDM_Dir]    CHECK ([Direction] IN ('+','-')),
    CONSTRAINT [CK_CDM_Amount] CHECK ([Amount] >= 0)
);
GO

CREATE NONCLUSTERED INDEX [IX_CDM_Shift]
    ON [dbo].[CashDrawerMovements] ([TenantId], [ShiftId], [OccurredAt])
    INCLUDE ([MovementType], [Amount], [Direction])
    WHERE [IsDeleted] = 0;
GO
```

### 8.4) `SuspendedInvoices` — الفواتير المعلّقة

سلة بيع مُجمّدة مؤقتاً لخدمة عميل آخر ثم استئنافها. تُخزَّن كـ snapshot JSON (مسموح صراحةً في §8 من مرجع التصميم لأنها **غير مرحّلة** ومؤقتة).

```sql
CREATE TABLE [dbo].[SuspendedInvoices]
(
    [SuspendCode]      VARCHAR(30)  NOT NULL,   -- كود استرجاع قصير 'H-0042'
    [ShiftId]          BIGINT       NOT NULL,
    [CashierUserId]    BIGINT       NOT NULL,
    [CustomerId]       BIGINT       NULL,
    [ItemsCount]       INT          NOT NULL CONSTRAINT DF_SuspendedInvoices_ItemsCount DEFAULT (0),
    [SubTotal]         DECIMAL(18,4) NOT NULL CONSTRAINT DF_SuspendedInvoices_SubTotal DEFAULT (0),
    [DiscountTotal]    DECIMAL(18,4) NOT NULL CONSTRAINT DF_SuspendedInvoices_DiscountTotal DEFAULT (0),
    [TaxTotal]         DECIMAL(18,4) NOT NULL CONSTRAINT DF_SuspendedInvoices_TaxTotal DEFAULT (0),
    [GrandTotal]       DECIMAL(18,4) NOT NULL CONSTRAINT DF_SuspendedInvoices_GrandTotal DEFAULT (0),
    -- Snapshot كامل لبنود السلة (Cart) — JSON مؤقّت غير محاسبي:
    [CartSnapshot]     NVARCHAR(MAX) NOT NULL
        CONSTRAINT CK_SuspendedInvoices_Json CHECK (ISJSON([CartSnapshot]) = 1),
    [SuspendedAt]      DATETIME2(3) NOT NULL,
    [Status]           VARCHAR(12)  NOT NULL,   -- 'Suspended' | 'Resumed' | 'Discarded'
    [ResumedAt]        DATETIME2(3) NULL,

    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NOT NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_SuspendedInvoices_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_SuspendedInvoices_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_SuspendedInvoices] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SI_Tenant]  FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_SI_Store]   FOREIGN KEY ([StoreId])  REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_SI_Shift]   FOREIGN KEY ([ShiftId])  REFERENCES [dbo].[PosShifts]([Id]),
    CONSTRAINT [CK_SI_Status]  CHECK ([Status] IN ('Suspended','Resumed','Discarded'))
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_SuspendedInvoices_Code]
    ON [dbo].[SuspendedInvoices] ([TenantId], [StoreId], [SuspendCode])
    WHERE [Status] = 'Suspended' AND [IsDeleted] = 0;
GO
```

---

## 9) الـ API (Endpoints)

| Method | Route | الوصف | الصلاحية |
|--------|-------|-------|----------|
| POST | `/api/pos/shifts/open` | فتح شفت (RegisterId + OpeningFloat) | `POS.Shift.Open` |
| POST | `/api/pos/shifts/{id}/close` | إغلاق الشفت + جرد الكاش | `POS.Shift.Close` |
| GET  | `/api/pos/shifts/current` | الشفت المفتوح الحالي وملخّصه | `POS.Access` |
| GET  | `/api/pos/products/lookup?barcode=` | جلب منتج بالباركود | `POS.Access` |
| GET  | `/api/pos/products/search?q=` | بحث سريع (اسم/كود) | `POS.Access` |
| POST | `/api/pos/sales` | إصدار فاتورة بيع كاملة | `POS.Sale.Create` |
| POST | `/api/pos/returns` | تنفيذ مرتجع | `POS.Return.Create` |
| POST | `/api/pos/suspend` | تعليق السلة الحالية | `POS.Suspend` |
| GET  | `/api/pos/suspend` | قائمة المعلّقات للشفت | `POS.Suspend` |
| POST | `/api/pos/suspend/{code}/resume` | استئناف فاتورة معلّقة | `POS.Suspend` |
| POST | `/api/pos/drawer/no-sale` | فتح الدرج بلا بيع | `POS.CashDrawer.Open` |
| POST | `/api/pos/drawer/pay-in` | إيداع نقدي | `POS.CashDrawer.PayIn` |
| POST | `/api/pos/drawer/pay-out` | سحب نقدي | `POS.CashDrawer.PayOut` |
| POST | `/api/pos/eod` | تنفيذ End Of Day للفرع | `POS.EndOfDay` |

**نموذج طلب إصدار فاتورة (POST /api/pos/sales):**

```json
{
  "shiftId": 123,
  "customerId": null,
  "lines": [
    { "productId": 88,  "barcode": "6251001", "qty": 2,     "unitPrice": 1.0000, "lineDiscount": 0 },
    { "productId": 140, "barcode": "2100001", "weight": 0.750, "unitPrice": 2.0000, "lineDiscount": 0 }
  ],
  "invoiceDiscount": { "type": "Percent", "value": 10 },
  "payments": [
    { "method": "Cash", "amount": 2.00, "tendered": 5.00 },
    { "method": "Card", "amount": 1.65, "cardRef": "APPROVED-7781" }
  ]
}
```

**الاستجابة (201):** رقم الفاتورة، الإجماليات، الباقي (Change)، أمر فتح الدرج، وحمولة الطباعة. تُبثّ إشارة SignalR `SaleCompleted` إلى لوحة الفرع لتحديث المبيعات الحيّة.

---

## 10) مخطط التدفّق — دورة الشفت (Shift Lifecycle Flow Chart)

```
                         ┌──────────────────────┐
                         │   دخول الكاشير (Login) │
                         └──────────┬───────────┘
                                    │
                          ┌─────────▼──────────┐
                          │  هل يوجد شفت مفتوح؟  │
                          └───┬────────────┬───┘
                          لا  │            │ نعم
                    ┌─────────▼──┐    ┌────▼──────────────┐
                    │ فتح شفت جديد │    │ استئناف نفس الشفت  │
                    │ + OpeningFloat│   └────┬──────────────┘
                    └───────┬──────┘        │
                            └───────┬────────┘
                                    │
                    ╔═══════════════▼════════════════╗
                    ║        حلقة البيع (Selling)      ║
                    ║  Scan/Search → Add → Discount    ║
                    ║  → Tax → Pay(Cash/Card/Split)    ║
                    ║  → Print → Drawer → (Suspend?)   ║
                    ║  → Return? (مرتجع)                ║
                    ╚═══════════════┬════════════════╝
                                    │ نهاية الوردية
                          ┌─────────▼──────────┐
                          │   Close Shift       │
                          │ حساب ExpectedCash    │
                          └─────────┬──────────┘
                                    │
                          ┌─────────▼──────────┐
                          │  Cash Count (الجرد)  │
                          │ إدخال CountedCash    │
                          │ Variance = Counted-  │
                          │           Expected   │
                          └─────────┬──────────┘
                                    │  Status = 'Reconciled'
                          ┌─────────▼──────────┐
                          │  آخر شفت في اليوم؟   │
                          └───┬────────────┬───┘
                          لا  │            │ نعم
                    ┌─────────▼──┐    ┌────▼──────────────┐
                    │ شفت تالٍ...  │    │  End Of Day (EOD) │
                    └────────────┘    │ إقفال الجلسة+تقارير │
                                      │ Session='Closed'   │
                                      └───────────────────┘
```

---

## 11) ماذا يحدث عند: الحذف / التعديل / تغيير السعر / انتهاء العرض

### عند الحذف (Delete)
- **بند من سلة غير مرحّلة:** يُزال من الذاكرة/الـ Snapshot فقط، لا أثر في قاعدة البيانات.
- **بند من فاتورة مرحّلة:** ممنوع الحذف الفعلي (BR-09). الطريق الوحيد = **فاتورة مرتجع** تُعيد الكمية للمخزون وتُخصم من الدرج نقداً، مع ربط `SalesReturns.OriginalInvoiceId`.
- **شفت:** لا يُحذف أبداً بعد أي حركة؛ Soft Delete فقط لشفت فُتح خطأً وبلا مبيعات.

### عند التعديل (Edit)
- تعديل الكمية/الوزن قبل الترحيل يُعيد حساب الإجماليات فوراً في الواجهة.
- تعديل فاتورة **مرحّلة** غير مسموح؛ يُعالَج بمرتجع + فاتورة جديدة (سلامة السجل المحاسبي).

### عند تغيير السعر (Price Change)
- يُطبَّق سعر المنتج **لحظة الإضافة** ويُجمَّد في البند (`UnitPrice` snapshot). تغيير سعر المنتج لاحقاً في الكتالوج **لا** يؤثر على الفواتير المُصدرة.
- تجاوز السعر يدوياً يتطلب `POS.Price.Override` ويُسجَّل في Audit مع السعر الأصلي والجديد.

### عند انتهاء العرض (Offer Expiry)
- محرّك العروض (تفاصيله في [19-Offers-And-Promotions.md](19-Offers-And-Promotions.md)) يُقيَّم **لحظة إضافة البند**؛ العرض المنتهي (`EndDate < UtcNow` أو خارج `Happy Hour`) لا يُطبَّق ولا يظهر خصمه.
- الفواتير المُصدرة أثناء سريان العرض تحتفظ بخصمها (snapshot) ولا تتغيّر بعد انتهائه.

---

## 12) سجل التدقيق (Audit Log)

كل عملية حسّاسة تُكتب في `AuditLogs` (Append-only) مع `TenantId, StoreId, UserId, Action, EntityType, EntityId, OldValue, NewValue, IpAddress, OccurredAt`:

| الحدث | يُسجَّل |
|-------|--------|
| فتح/إغلاق الشفت | Register, OpeningFloat, Expected/Counted/Variance |
| كل فاتورة/مرتجع | الرقم، الإجمالي، طريقة الدفع، الكاشير |
| فتح الدرج No Sale | المستخدم، الوقت، السبب |
| Pay In / Pay Out | المبلغ، السبب، المستخدم |
| تجاوز الخصم/السعر | القيمة الأصلية والجديدة، المُخوِّل (Manager PIN) |
| فرق جرد الكاش (Variance ≠ 0) | تنبيه للمدير + قيد في Audit |
| End Of Day | ملخّص الجلسة، من نفّذها |

---

## 13) قواعد جرد الكاش (Cash Count Rules)

عند إغلاق الشفت يُحسب النقد المتوقّع من حركات الدرج ثم يُقارَن بالفعلي:

```
ExpectedCash =  OpeningFloat
              + Σ(Sale cash payments)          -- المبيعات النقدية
              − Σ(Change given)                 -- الباقي المُعاد للزبائن
              − Σ(Cash refunds)                 -- مرتجعات نقدية
              + Σ(PayIn)                         -- إيداعات
              − Σ(PayOut)                        -- سحوبات

CountedCash  =  Σ(فئات النقد المُدخَلة يدوياً في الجرد)

CashVariance =  CountedCash − ExpectedCash
```

| النتيجة | التفسير | الإجراء |
|---------|---------|---------|
| `Variance = 0` | تطابق تام | إغلاق مباشر → `Reconciled` |
| `Variance > 0` | **زيادة (Over)** — نقد فائض | يُقبل بملاحظة إلزامية + تنبيه |
| `Variance < 0` | **عجز (Short)** — نقص نقد | يتطلب سبباً + قد يحتاج موافقة مدير حسب `MaxAllowedVariance` |

- الجرد يُدخَل بالفئات النقدية (Denominations) لدقّة أعلى؛ مجموعها = `CountedCash`.
- لا يُسمح بـ End Of Day قبل أن تصبح **كل** شفتات اليوم `Reconciled`.

---

## 14) الأخطاء المحتملة (Possible Errors)

| الكود | الرسالة | السبب | المعالجة |
|-------|---------|-------|----------|
| `POS_NO_OPEN_SHIFT` | لا يوجد شفت مفتوح | محاولة بيع بلا شفت | فتح شفت أولاً |
| `POS_SHIFT_ALREADY_OPEN` | صندوق مشغول بشفت مفتوح | UX_PosShifts_OpenRegister | إغلاق الشفت السابق |
| `POS_PRODUCT_NOT_FOUND` | باركود غير معروف | لا مطابقة في ProductBarcodes | بحث يدوي/تصحيح |
| `POS_INSUFFICIENT_STOCK` | رصيد غير كافٍ | AllowNegativeStock=0 | تعديل الكمية/موافقة |
| `POS_DISCOUNT_LIMIT` | تجاوز حد الخصم | > MaxDiscountPercent | Manager Override |
| `POS_PAYMENT_MISMATCH` | مجموع الدفعات ≠ الإجمالي | خطأ Split | تصحيح الدفعات |
| `POS_RETURN_QTY_EXCEEDS` | كمية المرتجع تتجاوز المُباع | تحقّق المرتجع | تقليل الكمية |
| `POS_CONCURRENCY` | تعارض تحديث | ConcurrencyStamp تغيّر | إعادة تحميل |
| `POS_EOD_OPEN_SHIFTS` | شفتات غير مُقفلة | محاولة EOD مبكرة | إغلاق كل الشفتات |

---

## 15) الأداء (Performance)

- **بحث الباركود:** يعتمد على `UX_ProductBarcodes_Tenant_Barcode` (Unique filtered) — بحث O(log n) بلا مسح جدول.
- **الكاش:** كتالوج المنتجات المُباعة كثيراً يُخزَّن في Redis/ذاكرة العميل لتقليل ذهاب-وإياب الشبكة.
- **الترحيل الذرّي:** معاملة واحدة قصيرة (فاتورة+بنود+دفعات+مخزون+درج)؛ مع `READ COMMITTED SNAPSHOT` لتقليل الأقفال.
- **Sequences:** تحديث ذرّي عبر `UPDATE ... OUTPUT` لتفادي أقفال طويلة على رقم الفاتورة.
- **SignalR:** بثّ ملخّص خفيف فقط (لا حمولات كبيرة) للوحة الفرع الحيّة.
- **الفهارس المُرشَّحة** `WHERE IsDeleted=0` تُبقي الفهارس صغيرة وسريعة رغم Soft Delete.
- **الطباعة** لا-متزامنة (لا تحجب إغلاق الفاتورة).

---

## 16) الأمان (Security)

- **العزل:** كل استعلام يبدأ بـ `TenantId` (Global Query Filter) + تقييد `StoreId` من Claims؛ الكاشير لا يرى فروعاً أخرى.
- **الصلاحيات:** كل Endpoint محميّ بـ Policy؛ تجاوز الخصم/السعر يتطلب صلاحية مرتفعة + Manager PIN.
- **عدم كشف المعرّفات:** أرقام الفواتير من `Sequences` لا من `Id` (منع IDOR).
- **التدقيق الكامل:** فتح الدرج، Pay In/Out، فروق الجرد — كلها Append-only في `AuditLogs`.
- **مقاومة التلاعب:** الفواتير المرحّلة غير قابلة للحذف/التعديل؛ التصحيح بمرتجع فقط.
- **حماية الدفع بالبطاقة:** لا تُخزَّن أرقام البطاقات (PAN)؛ يُحفظ مرجع الموافقة فقط (PCI-DSS scope minimization).
- **التزامن:** `ConcurrencyStamp ROWVERSION` يمنع دفعتين متزامنتين على نفس الفاتورة.
- **الحدّ من محاولات الاحتيال:** فروق الجرد المتكررة لكاشير تُرفع كتنبيه إداري عبر [22-Notifications.md](22-Notifications.md).

---

_يلتزم هذا الملف بالمعايير الحاكمة في [04-Database-Design.md](04-Database-Design.md). العروض والخصومات التلقائية مفصّلة في [19-Offers-And-Promotions.md](19-Offers-And-Promotions.md)._
