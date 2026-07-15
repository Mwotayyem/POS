# 19 — Offers & Promotions (العروض والترويج)

> محرّك عروض مرن (Offer Engine) يُطبَّق تلقائياً على نقطة البيع وفواتير البيع، **يُفعَّل ويُنتهى تلقائياً** دون تدخّل يدوي، ويقيس أداء كل عرض **بعد خصم المرتجعات**. يلتزم هذا الملف حرفياً بمعايير [04-Database-Design.md](04-Database-Design.md): الأعمدة المشتركة، `DECIMAL(18,4)`، `DATETIME2(3)` UTC، Soft Delete، عزل `TenantId`. يتكامل مباشرة مع [18-POS.md](18-POS.md) و [15-Sales-Invoices.md](15-Sales-Invoices.md) و [16-Sales-Returns.md](16-Sales-Returns.md).

---

## 1) الهدف (Purpose)

بناء نظام عروض تجاري كامل يدعم:

- **أنواع العروض:** نسبة مئوية (Percentage)، مبلغ ثابت (Fixed)، Buy X Get Y، Mix & Match، Happy Hour.
- **شروط التطبيق (Conditions):** حسب التاريخ / الوقت / الفرع / العميل (أو شريحته) / الكمية / التصنيف / المنتج.
- **التفعيل التلقائي** فور بلوغ تاريخ/وقت البدء، و**الانتهاء التلقائي** فور تجاوز تاريخ/وقت النهاية أو استنفاد الحد الأقصى للاستخدام — **بلا أي إجراء يدوي**.
- **محرّك تقييم (Evaluation Engine)** يختار العرض الأمثل عند تعدّد العروض حسب أولوية محدّدة.
- **تقرير أداء دقيق** يخصم المرتجعات من نتائج العرض (Net Units / Net Discount / Profit After Offer).

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية | الوصف |
|----------|-------|
| `Offers.View` | عرض قائمة العروض وتفاصيلها |
| `Offers.Create` | إنشاء عرض جديد |
| `Offers.Edit` | تعديل عرض (قبل بدء سريانه أساساً) |
| `Offers.Activate` | تفعيل/تعطيل يدوي (Override) |
| `Offers.Delete` | حذف (Soft) عرض |
| `Offers.Report` | عرض تقارير أداء العروض |

> التطبيق على الفاتورة **لا** يحتاج صلاحية للكاشير — يحدث تلقائياً؛ الصلاحيات أعلاه للإدارة (Back-office) فقط.

---

## 3) تصميم الصفحة (Page Layout)

```
┌────────────────────────────────────────────────────────────────────────┐
│  Offers & Promotions                             [ + New Offer ]          │
├────────────────────────────────────────────────────────────────────────┤
│ Filters: [Type ▾] [Status ▾: Active/Scheduled/Expired] [Store ▾] [🔍]     │
├────┬──────────────────┬──────────┬────────────┬───────────┬─────────────┤
│ #  │ Name             │ Type     │ Period      │ Priority  │ Status       │
├────┼──────────────────┼──────────┼────────────┼───────────┼─────────────┤
│ 1  │ عيد الفطر 20%     │ Percent  │ 07/01–07/10 │   100     │ 🟢 Active   │
│ 2  │ اشترِ 2 خذ 1      │ BuyXGetY │ 07/05–07/20 │    90     │ 🟢 Active   │
│ 3  │ Happy Hour مساءً  │ HappyHour│ يومياً 18-21 │    80     │ 🟡 Scheduled│
│ 4  │ صيف 5 دنانير      │ Fixed    │ 06/01–06/30 │    50     │ 🔴 Expired  │
└────┴──────────────────┴──────────┴────────────┴───────────┴─────────────┘
      [ Performance Report ]  [ Duplicate ]  [ Deactivate ]
```

نموذج إنشاء العرض متعدّد الأقسام: (Basic) النوع والفترة والأولوية · (Conditions) قواعد التطبيق · (Targets) المنتجات/التصنيفات · (Reward) الفائدة · (Limits) حدود الاستخدام.

---

## 4) الأزرار (Buttons)

| الزر | الوظيفة |
|------|---------|
| New Offer | إنشاء عرض جديد بالمعالج (Wizard) |
| Save | حفظ العرض (يُجدوَل تلقائياً حسب التواريخ) |
| Activate / Deactivate | تفعيل/تعطيل يدوي (Override اختياري) |
| Duplicate | نسخ عرض كقالب |
| Add Rule | إضافة قاعدة شرط (Condition Rule) |
| Add Target | إضافة منتج/تصنيف مستهدف |
| Performance Report | فتح تقرير الأداء (Net of Returns) |
| Delete | حذف Soft |

---

## 5) الحقول (Fields)

| الحقل | النوع | ملاحظات |
|-------|-------|---------|
| Name | نص | اسم العرض المعروض |
| OfferType | قائمة | Percentage/Fixed/BuyXGetY/MixMatch/HappyHour |
| DiscountValue | DECIMAL(18,4) | نسبة أو مبلغ حسب النوع |
| StartDate / EndDate | DATETIME2(3) UTC | نافذة السريان |
| DailyStartTime / DailyEndTime | TIME | لـ Happy Hour (يومي) |
| DaysOfWeek | VARCHAR | 'Mon,Tue,...' اختياري |
| Priority | INT | الأعلى يفوز عند التعارض |
| Stackable | BIT | هل يُجمع مع عروض أخرى |
| MaxUsageTotal / MaxUsagePerCustomer | INT | حدود الاستخدام |
| MinQty / MinAmount | DECIMAL | شرط الحد الأدنى |
| BuyQty / GetQty / GetDiscountPercent | INT/DEC | لـ Buy X Get Y |
| CustomerSegment | نص | 'VIP','Wholesale',... |
| StoreScope | قائمة | كل الفروع أو فروع محدّدة |

---

## 6) التحقق (Validation)

- `EndDate > StartDate`؛ ولـ Happy Hour `DailyEndTime > DailyStartTime`.
- `DiscountValue > 0`؛ ولـ Percentage `≤ 100`.
- Buy X Get Y: `BuyQty ≥ 1` و `GetQty ≥ 1`.
- لا تداخل متناقض بين قواعد المنتج نفسه ما لم يكن `Stackable = 1`.
- `MaxUsageTotal ≥ 0` (0 = بلا حد).
- يجب وجود هدف واحد على الأقل (منتج/تصنيف/كل السلة).

---

## 7) قواعد العمل (Business Rules)

1. **BR-01 — تفعيل تلقائي:** العرض يصبح `Active` تلقائياً عند `StartDate ≤ UtcNow` — لا حاجة لضغط زر.
2. **BR-02 — انتهاء تلقائي:** يصبح `Expired` فور `EndDate < UtcNow` أو استنفاد `MaxUsageTotal` — **دون تدخّل يدوي** (يُنفَّذ عبر التقييم اللحظي + وظيفة Hangfire دورية تُحدِّث الحالة).
3. **BR-03 — Snapshot عند البيع:** الخصم المُطبَّق يُثبَّت في بند الفاتورة و `OfferUsage`؛ تغيير/انتهاء العرض لاحقاً **لا** يمسّ الفواتير الصادرة.
4. **BR-04 — الأولوية:** عند تطابق أكثر من عرض غير قابل للتجميع، يفوز الأعلى `Priority`؛ عند التساوي، الأحدث (`StartDate`)، ثم الأكبر خصماً للعميل.
5. **BR-05 — التجميع (Stacking):** فقط العروض `Stackable = 1` تُجمَع، وبترتيب: خصم البند ثم خصم السلة، مع سقف إجمالي `MaxTotalDiscountPercent`.
6. **BR-06 — Happy Hour:** يُقيَّم بوقت الخادم (UTC محوّل لتوقيت الفرع) مقابل `DailyStart/End` و`DaysOfWeek`.
7. **BR-07 — خصم المرتجعات من الأداء:** أي وحدة تُرتجَع تُخصَم من `Net Units` و`Net Discount` و`Profit After Offer` عبر ربط المرتجع بـ `OfferUsage`.
8. **BR-08 — حدود الاستخدام** تُفحَص ذرّياً لحظة الترحيل لمنع تجاوز `MaxUsageTotal` تحت التزامن.

---

## 8) جداول قاعدة البيانات (Database Tables — SQL كامل)

> جميع الجداول ترث الأعمدة المشتركة من [04-Database-Design.md](04-Database-Design.md) §2.

### 8.1) `Offers` — رأس العرض

```sql
CREATE TABLE [dbo].[Offers]
(
    [Name]                  NVARCHAR(200) NOT NULL,
    [OfferType]             VARCHAR(15)  NOT NULL,   -- 'Percentage'|'Fixed'|'BuyXGetY'|'MixMatch'|'HappyHour'
    [DiscountValue]         DECIMAL(18,4) NOT NULL CONSTRAINT DF_Offers_DiscountValue DEFAULT (0),
    [Priority]              INT          NOT NULL CONSTRAINT DF_Offers_Priority DEFAULT (0),
    [Stackable]             BIT          NOT NULL CONSTRAINT DF_Offers_Stackable DEFAULT (0),
    [StartDate]             DATETIME2(3) NOT NULL,   -- UTC
    [EndDate]               DATETIME2(3) NOT NULL,   -- UTC
    [DailyStartTime]        TIME(0)      NULL,       -- Happy Hour
    [DailyEndTime]          TIME(0)      NULL,
    [DaysOfWeek]            VARCHAR(30)  NULL,        -- 'Mon,Tue,Wed'
    -- Buy X Get Y:
    [BuyQty]                INT          NULL,
    [GetQty]                INT          NULL,
    [GetDiscountPercent]    DECIMAL(9,4) NULL,        -- 100 = مجاناً
    -- شروط عامة:
    [MinQty]                DECIMAL(18,3) NULL,
    [MinAmount]             DECIMAL(18,4) NULL,
    [MaxDiscountAmount]     DECIMAL(18,4) NULL,       -- سقف للخصم النسبي
    -- الحدود:
    [MaxUsageTotal]         INT          NOT NULL CONSTRAINT DF_Offers_MaxUsageTotal DEFAULT (0),   -- 0 = بلا حد
    [MaxUsagePerCustomer]   INT          NOT NULL CONSTRAINT DF_Offers_MaxUsagePerCustomer DEFAULT (0),
    [CurrentUsageCount]     INT          NOT NULL CONSTRAINT DF_Offers_CurrentUsageCount DEFAULT (0),
    -- الحالة (تُحدَّث تلقائياً — لا يدوياً):
    [Status]                VARCHAR(12)  NOT NULL CONSTRAINT DF_Offers_Status DEFAULT ('Scheduled'),
                                                    -- 'Scheduled'|'Active'|'Expired'|'Disabled'
    [ManualOverride]        BIT          NOT NULL CONSTRAINT DF_Offers_ManualOverride DEFAULT (0),
    [Description]           NVARCHAR(500) NULL,

    -- ===== الأعمدة المشتركة =====
    [Id]                    BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]              BIGINT       NOT NULL,
    [StoreId]               BIGINT       NULL,       -- NULL = كل الفروع
    [CreatedDate]           DATETIME2(3) NOT NULL CONSTRAINT DF_Offers_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]             BIGINT       NULL,
    [ModifiedDate]          DATETIME2(3) NULL,
    [ModifiedBy]            BIGINT       NULL,
    [DeletedDate]           DATETIME2(3) NULL,
    [DeletedBy]             BIGINT       NULL,
    [IsDeleted]             BIT          NOT NULL CONSTRAINT DF_Offers_IsDeleted DEFAULT (0),
    [ConcurrencyStamp]      ROWVERSION   NOT NULL,

    CONSTRAINT [PK_Offers] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Offers_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_Offers_Store]  FOREIGN KEY ([StoreId])  REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [CK_Offers_Type]   CHECK ([OfferType] IN ('Percentage','Fixed','BuyXGetY','MixMatch','HappyHour')),
    CONSTRAINT [CK_Offers_Status] CHECK ([Status] IN ('Scheduled','Active','Expired','Disabled')),
    CONSTRAINT [CK_Offers_Dates]  CHECK ([EndDate] > [StartDate])
);
GO

-- فهرس التقييم اللحظي: العروض السارية زمنياً داخل المستأجر/الفرع
CREATE NONCLUSTERED INDEX [IX_Offers_Active_Window]
    ON [dbo].[Offers] ([TenantId], [StoreId], [Status], [StartDate], [EndDate])
    INCLUDE ([OfferType], [Priority], [Stackable])
    WHERE [IsDeleted] = 0;
GO
```

### 8.2) `OfferRules` — قواعد الشروط (النموذج المرن)

نموذج قواعد مرن يسمح ببناء أي شرط دون تعديل المخطط: كل قاعدة = (نوع، مُشغّل، قيمة).

```sql
CREATE TABLE [dbo].[OfferRules]
(
    [OfferId]          BIGINT       NOT NULL,
    [RuleType]         VARCHAR(20)  NOT NULL,   -- 'Category'|'Product'|'Customer'|'Segment'|'Quantity'|'Amount'|'DayOfWeek'|'TimeRange'|'Store'
    [Operator]         VARCHAR(10)  NOT NULL,   -- '='|'!='|'>='|'<='|'IN'|'BETWEEN'
    [ValueText]        NVARCHAR(400) NULL,      -- للقيم النصية/القوائم 'VIP' أو '10,12,15'
    [ValueNumber]      DECIMAL(18,4) NULL,      -- للقيم الرقمية (كمية/مبلغ)
    [ValueNumber2]     DECIMAL(18,4) NULL,      -- الطرف الثاني لـ BETWEEN
    [LogicGroup]       TINYINT      NOT NULL CONSTRAINT DF_OfferRules_LogicGroup DEFAULT (1),  -- قواعد نفس المجموعة = AND، بين المجموعات = OR
    [IsRequired]       BIT          NOT NULL CONSTRAINT DF_OfferRules_IsRequired DEFAULT (1),

    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_OfferRules_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_OfferRules_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_OfferRules] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_OfferRules_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_OfferRules_Store]  FOREIGN KEY ([StoreId])  REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_OfferRules_Offer]  FOREIGN KEY ([OfferId])  REFERENCES [dbo].[Offers]([Id]),
    CONSTRAINT [CK_OfferRules_Type]   CHECK ([RuleType] IN ('Category','Product','Customer','Segment','Quantity','Amount','DayOfWeek','TimeRange','Store')),
    CONSTRAINT [CK_OfferRules_Op]     CHECK ([Operator] IN ('=','!=','>=','<=','IN','BETWEEN'))
);
GO

CREATE NONCLUSTERED INDEX [IX_OfferRules_Offer]
    ON [dbo].[OfferRules] ([TenantId], [OfferId])
    WHERE [IsDeleted] = 0;
GO
```

> **مثال قواعد مرنة لعرض "خصم 20% على منتجات الألبان لعملاء VIP، كمية ≥ 3":**
> - Rule1: `Category IN '5,7'` (LogicGroup=1)
> - Rule2: `Segment = 'VIP'` (LogicGroup=1)
> - Rule3: `Quantity >= 3` (LogicGroup=1)
> جميعها LogicGroup=1 ⇒ AND. لإضافة بديل (OR) نضع قاعدة في LogicGroup=2.

### 8.3) `OfferProducts` — المنتجات/التصنيفات المستهدفة

```sql
CREATE TABLE [dbo].[OfferProducts]
(
    [OfferId]          BIGINT       NOT NULL,
    [TargetType]       VARCHAR(10)  NOT NULL,   -- 'Product' | 'Category'
    [ProductId]        BIGINT       NULL,
    [CategoryId]       BIGINT       NULL,
    [Role]             VARCHAR(10)  NOT NULL CONSTRAINT DF_OfferProducts_Role DEFAULT ('Trigger'),
                                                -- 'Trigger' (المشترى) | 'Reward' (المجاني/المخفّض) | 'Both'
    [MixMatchGroup]    TINYINT      NULL,       -- تجميع Mix & Match
    -- ===== ضبط تعارض العروض (منع تراكب الخصومات) =====
    [Priority]         INT          NOT NULL CONSTRAINT DF_OfferProducts_Priority DEFAULT (0),
                                                -- أولوية تطبيق هذا البند؛ الأعلى يُطبَّق أولاً عند تعدّد العروض على نفس المنتج
    [MaxDiscount]      DECIMAL(18,4) NULL,       -- سقف الخصم لهذا البند (NULL = بلا سقف) — يمنع خصماً مبالغاً
    [MaxQty]           DECIMAL(18,4) NULL,       -- أقصى كمية يشملها العرض لكل فاتورة (NULL = بلا حد)

    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_OfferProducts_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_OfferProducts_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_OfferProducts] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_OfferProducts_Tenant]   FOREIGN KEY ([TenantId])   REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_OfferProducts_Store]    FOREIGN KEY ([StoreId])    REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_OfferProducts_Offer]    FOREIGN KEY ([OfferId])    REFERENCES [dbo].[Offers]([Id]),
    CONSTRAINT [FK_OfferProducts_Product]  FOREIGN KEY ([ProductId])  REFERENCES [dbo].[Products]([Id]),
    CONSTRAINT [FK_OfferProducts_Category] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories]([Id]),
    CONSTRAINT [CK_OfferProducts_Target]   CHECK ([TargetType] IN ('Product','Category')),
    CONSTRAINT [CK_OfferProducts_Role]     CHECK ([Role] IN ('Trigger','Reward','Both')),
    CONSTRAINT [CK_OfferProducts_Ref]      CHECK (
        ([TargetType]='Product'  AND [ProductId]  IS NOT NULL AND [CategoryId] IS NULL) OR
        ([TargetType]='Category' AND [CategoryId] IS NOT NULL AND [ProductId]  IS NULL)),
    CONSTRAINT [CK_OfferProducts_MaxDiscount] CHECK ([MaxDiscount] IS NULL OR [MaxDiscount] >= 0),
    CONSTRAINT [CK_OfferProducts_MaxQty]      CHECK ([MaxQty] IS NULL OR [MaxQty] > 0)
);
GO

CREATE NONCLUSTERED INDEX [IX_OfferProducts_Offer]
    ON [dbo].[OfferProducts] ([TenantId], [OfferId], [Role])
    INCLUDE ([ProductId], [CategoryId], [TargetType])
    WHERE [IsDeleted] = 0;
GO
```

### 8.4) `OfferUsage` — سجل تطبيق العرض (مصدر تقرير الأداء)

كل تطبيق فعلي للعرض على بند فاتورة يُسجَّل هنا؛ وهو الجسر بين العرض والمرتجعات.

```sql
CREATE TABLE [dbo].[OfferUsage]
(
    [OfferId]              BIGINT       NOT NULL,
    [SalesInvoiceId]       BIGINT       NOT NULL,
    [SalesInvoiceItemId]   BIGINT       NOT NULL,
    [ProductId]            BIGINT       NOT NULL,
    [CustomerId]           BIGINT       NULL,
    [QtyApplied]           DECIMAL(18,3) NOT NULL,   -- الكمية التي طُبِّق عليها العرض
    [QtyReturned]          DECIMAL(18,3) NOT NULL CONSTRAINT DF_OfferUsage_QtyReturned DEFAULT (0), -- تُحدَّث عند المرتجع
    [UnitCost]             DECIMAL(18,4) NOT NULL,   -- تكلفة الوحدة (لحساب الربح)
    [OriginalUnitPrice]    DECIMAL(18,4) NOT NULL,   -- السعر قبل العرض
    [DiscountPerUnit]      DECIMAL(18,4) NOT NULL,   -- خصم العرض للوحدة
    [DiscountAmount]       DECIMAL(18,4) NOT NULL,   -- QtyApplied × DiscountPerUnit (snapshot)
    [AppliedAt]            DATETIME2(3) NOT NULL,

    [Id]                   BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]             BIGINT       NOT NULL,
    [StoreId]              BIGINT       NOT NULL,
    [CreatedDate]          DATETIME2(3) NOT NULL CONSTRAINT DF_OfferUsage_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]            BIGINT       NULL,
    [ModifiedDate]         DATETIME2(3) NULL,
    [ModifiedBy]           BIGINT       NULL,
    [DeletedDate]          DATETIME2(3) NULL,
    [DeletedBy]            BIGINT       NULL,
    [IsDeleted]            BIT          NOT NULL CONSTRAINT DF_OfferUsage_IsDeleted DEFAULT (0),
    [ConcurrencyStamp]     ROWVERSION   NOT NULL,

    CONSTRAINT [PK_OfferUsage] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_OfferUsage_Tenant]   FOREIGN KEY ([TenantId])           REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_OfferUsage_Store]    FOREIGN KEY ([StoreId])            REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_OfferUsage_Offer]    FOREIGN KEY ([OfferId])            REFERENCES [dbo].[Offers]([Id]),
    CONSTRAINT [FK_OfferUsage_Invoice]  FOREIGN KEY ([SalesInvoiceId])     REFERENCES [dbo].[SalesInvoices]([Id]),
    CONSTRAINT [FK_OfferUsage_Item]     FOREIGN KEY ([SalesInvoiceItemId]) REFERENCES [dbo].[SalesInvoiceItems]([Id]),
    CONSTRAINT [FK_OfferUsage_Product]  FOREIGN KEY ([ProductId])          REFERENCES [dbo].[Products]([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_OfferUsage_Offer]
    ON [dbo].[OfferUsage] ([TenantId], [OfferId], [AppliedAt])
    INCLUDE ([QtyApplied], [QtyReturned], [DiscountAmount], [OriginalUnitPrice], [UnitCost])
    WHERE [IsDeleted] = 0;
GO
```

---

## 9) الـ API (Endpoints)

| Method | Route | الوصف | الصلاحية |
|--------|-------|-------|----------|
| GET  | `/api/offers` | قائمة العروض (فلترة بالحالة/النوع) | `Offers.View` |
| POST | `/api/offers` | إنشاء عرض + قواعده + أهدافه | `Offers.Create` |
| PUT  | `/api/offers/{id}` | تعديل عرض | `Offers.Edit` |
| POST | `/api/offers/{id}/activate` | تفعيل/تعطيل يدوي (Override) | `Offers.Activate` |
| DELETE | `/api/offers/{id}` | حذف Soft | `Offers.Delete` |
| POST | `/api/offers/evaluate` | تقييم العروض لسلة (يُستدعى من POS) | داخلي/`POS.Access` |
| GET  | `/api/offers/{id}/performance` | تقرير الأداء (Net of Returns) | `Offers.Report` |

**نموذج طلب التقييم (POST /api/offers/evaluate) — يُستدعى لحظياً من POS:**

```json
{
  "storeId": 3,
  "customerId": 55,
  "customerSegment": "VIP",
  "atUtc": "2026-07-13T18:30:00Z",
  "lines": [
    { "productId": 88,  "categoryId": 5, "qty": 3, "unitPrice": 2.0000 },
    { "productId": 140, "categoryId": 7, "qty": 1, "unitPrice": 1.5000 }
  ]
}
```

**الاستجابة:** لكل بند، العرض الفائز ومقدار الخصم، وأي خصم سلة إجمالي، مع أسباب الاختيار (للشفافية/التدقيق).

---

## 10) محرّك تقييم العروض (Offer Evaluation Engine)

المحرّك يُستدعى لحظياً عند كل تغيير في السلة على POS، ويُنفَّذ بخطوات محدّدة:

```
                    ┌─────────────────────────────────────┐
                    │  استدعاء evaluate(cart, ctx)          │
                    └───────────────────┬─────────────────┘
                                        │
             ┌──────────────────────────▼───────────────────────────┐
             │ 1) جلب العروض المرشّحة (Candidate Offers):             │
             │    Status='Active' AND StartDate ≤ now ≤ EndDate      │
             │    AND (StoreId = ctx.Store OR StoreId IS NULL)       │
             │    AND (Happy Hour: now ضمن Daily+DaysOfWeek)         │
             └──────────────────────────┬───────────────────────────┘
                                        │
             ┌──────────────────────────▼───────────────────────────┐
             │ 2) تصفية بالقواعد (OfferRules):                        │
             │    تقييم كل قاعدة (Category/Product/Segment/Qty/Amount)│
             │    داخل المجموعة = AND ، بين المجموعات = OR            │
             └──────────────────────────┬───────────────────────────┘
                                        │
             ┌──────────────────────────▼───────────────────────────┐
             │ 3) فحص الحدود (Limits):                                │
             │    CurrentUsageCount < MaxUsageTotal                  │
             │    واستخدام العميل < MaxUsagePerCustomer               │
             └──────────────────────────┬───────────────────────────┘
                                        │
             ┌──────────────────────────▼───────────────────────────┐
             │ 4) حساب فائدة كل عرض مطابق لكل بند (Reward Calc):      │
             │    Percentage/Fixed/BuyXGetY/MixMatch                 │
             └──────────────────────────┬───────────────────────────┘
                                        │
             ┌──────────────────────────▼───────────────────────────┐
             │ 5) حل التعارض (Conflict Resolution):                   │
             │    إن كان Stackable=0 → اختر الأعلى Priority           │
             │    (ثم الأحدث، ثم الأكبر خصماً للعميل)                 │
             │    إن كان Stackable=1 → اجمع بترتيب: بند ثم سلة        │
             │    مع سقف MaxTotalDiscountPercent                     │
             └──────────────────────────┬───────────────────────────┘
                                        │
             ┌──────────────────────────▼───────────────────────────┐
             │ 6) إرجاع النتيجة + الأسباب (winning offer per line)    │
             │    تُثبَّت في الفاتورة و OfferUsage عند الترحيل         │
             └──────────────────────────────────────────────────────┘
```

**ترتيب الأولوية عند تعدّد العروض (Priority Ordering):**

1. **الأعلى `Priority`** يفوز أولاً.
2. عند التساوي: **الأحدث `StartDate`** (العرض الأجدد يُفترض أنه الحملة الحالية).
3. عند التساوي: **الأكبر خصماً للعميل** (مصلحة العميل).
4. العروض `Stackable=1` فقط تُجمَع؛ غير القابلة للتجميع تُقصي بعضها ويبقى الفائز الواحد.

---

## 11) ماذا يحدث عند: الحذف / التعديل / تغيير السعر / **انتهاء العرض**

### عند الحذف (Delete)
- **Soft Delete** فقط؛ العرض يختفي من التقييم فوراً، لكن `OfferUsage` التاريخي يبقى سليماً لأغراض التقارير والتدقيق. الفواتير الصادرة لا تتأثر (الخصم snapshot).

### عند التعديل (Edit)
- تعديل عرض **قبل** بدء سريانه: يُطبَّق فوراً.
- تعديل عرض **ساري**: يُنصَح بإيقافه وإنشاء نسخة جديدة (للحفاظ على وضوح الأداء)؛ التعديل المباشر يؤثر فقط على الفواتير **اللاحقة**، ولا يمسّ ما صدر.

### عند تغيير السعر (Price Change)
- المحرّك يعمل على `unitPrice` الحالي لحظة التقييم؛ تغيير سعر المنتج في الكتالوج يغيّر قاعدة حساب الخصم للفواتير **القادمة** فقط. `OriginalUnitPrice` يُثبَّت في `OfferUsage` لكل بيع لضمان دقّة تقرير الربح.

### عند **انتهاء العرض (Offer Expiry)** — القسم المحوري
الانتهاء **تلقائي بالكامل** ويحدث عبر مسارين متكاملين:

1. **الانتهاء اللحظي (Lazy / Real-time):** في كل استدعاء `evaluate`، أي عرض `EndDate < UtcNow` (أو خارج نافذة Happy Hour، أو `CurrentUsageCount ≥ MaxUsageTotal`) **يُستبعَد فوراً** من المرشّحين — حتى لو بقيت حالته `Active` في الجدول للحظات. النتيجة: **لا يُطبَّق عرض منتهٍ أبداً** على أي فاتورة، بلا تدخّل بشري.

2. **الانتهاء المُجدوَل (Eager / Background):** وظيفة **Hangfire** دورية (كل دقيقة) تُنفّذ:
   ```sql
   UPDATE [dbo].[Offers]
      SET [Status] = 'Expired', [ModifiedDate] = SYSUTCDATETIME()
    WHERE [IsDeleted] = 0
      AND [Status] = 'Active'
      AND ([EndDate] < SYSUTCDATETIME()
           OR ([MaxUsageTotal] > 0 AND [CurrentUsageCount] >= [MaxUsageTotal]));
   ```
   وبالمثل تُرقّي `Scheduled → Active` عند بلوغ `StartDate`. هذا يُبقي لوحات الحالة دقيقة ويُطلق إشعار [22-Notifications.md](22-Notifications.md).

**آثار الانتهاء المضمونة:**
- الفواتير الصادرة **أثناء** سريان العرض تحتفظ بخصمها (snapshot في البند و`OfferUsage`) ولا يتغيّر شيء رجعياً.
- المرتجعات لبنود عرض منتهٍ **ما زالت تُخصَم** من أداء العرض عبر `QtyReturned` (المرتجع لا "ينتهي" بانتهاء العرض).
- السعر يعود للسعر الأصلي تلقائياً في السلة التالية (لا خصم يظهر) — بلا أي فعل من الكاشير أو المدير.
- تقرير الأداء يُجمَّد على القيم النهائية بعد صافي المرتجعات.

---

## 12) تقرير أداء العرض (Offer Performance — Net of Returns)

المقاييس تُحسب من `OfferUsage` مع خصم `QtyReturned` (الذي يُحدَّث من [16-Sales-Returns.md](16-Sales-Returns.md)):

| المقياس | المعادلة |
|---------|----------|
| **Units Sold** | `Σ QtyApplied` |
| **Units Returned** | `Σ QtyReturned` |
| **Net Units** | `Σ (QtyApplied − QtyReturned)` |
| **Original Revenue** | `Σ (QtyApplied − QtyReturned) × OriginalUnitPrice` |
| **Discount Amount** | `Σ (QtyApplied − QtyReturned) × DiscountPerUnit` |
| **Net Revenue** | `Original Revenue − Discount Amount` |
| **Profit Before Offer** | `Σ (QtyApplied − QtyReturned) × (OriginalUnitPrice − UnitCost)` |
| **Profit After Offer** | `Σ (QtyApplied − QtyReturned) × (OriginalUnitPrice − DiscountPerUnit − UnitCost)` |
| **Offer Effectiveness** | `Net Units / max(1, Σ QtyApplied)` — نسبة الوحدات الصافية بعد المرتجعات؛ ومؤشر ثانٍ = `(Profit After − Profit Before) / |Discount Amount|` لقياس مردود كل دينار خصم |

**SQL التقرير (صافي المرتجعات مُطبَّق):**

```sql
SELECT
    o.[Id]                                                          AS OfferId,
    o.[Name],
    SUM(u.[QtyApplied])                                             AS UnitsSold,
    SUM(u.[QtyReturned])                                            AS UnitsReturned,
    SUM(u.[QtyApplied] - u.[QtyReturned])                          AS NetUnits,
    SUM((u.[QtyApplied]-u.[QtyReturned]) * u.[OriginalUnitPrice])  AS OriginalRevenue,
    SUM((u.[QtyApplied]-u.[QtyReturned]) * u.[DiscountPerUnit])    AS DiscountAmount,
    SUM((u.[QtyApplied]-u.[QtyReturned]) *
        (u.[OriginalUnitPrice] - u.[UnitCost]))                    AS ProfitBeforeOffer,
    SUM((u.[QtyApplied]-u.[QtyReturned]) *
        (u.[OriginalUnitPrice] - u.[DiscountPerUnit] - u.[UnitCost])) AS ProfitAfterOffer
FROM [dbo].[OfferUsage] u
JOIN [dbo].[Offers] o ON o.[Id] = u.[OfferId] AND o.[IsDeleted] = 0
WHERE u.[TenantId] = @TenantId
  AND u.[IsDeleted] = 0
  AND u.[OfferId]   = @OfferId
GROUP BY o.[Id], o.[Name];
```

> **مبدأ حاسم:** المرتجع يُخفّض النتائج الحقيقية للعرض. عرض يبدو ناجحاً بمبيعات عالية قد يكون فاشلاً بعد خصم مرتجعات كبيرة — لذلك **كل** مقاييس التقرير تستخدم `(QtyApplied − QtyReturned)` وليس `QtyApplied` وحده.

---

## 13) سجل التدقيق (Audit Log)

| الحدث | يُسجَّل في `AuditLogs` |
|-------|----------------------|
| إنشاء/تعديل عرض | القيم القديمة والجديدة، المستخدم |
| تفعيل/تعطيل يدوي | ManualOverride، السبب، المُخوِّل |
| انتقال حالة تلقائي | Scheduled→Active→Expired (بواسطة System/Hangfire) |
| تطبيق العرض على فاتورة | OfferId، القيمة، البند (عبر OfferUsage) |
| بلوغ حد الاستخدام | تنبيه + قيد Audit |

---

## 14) الأخطاء المحتملة (Possible Errors)

| الكود | الرسالة | السبب |
|-------|---------|-------|
| `OFFER_INVALID_DATES` | نهاية قبل بداية | EndDate ≤ StartDate |
| `OFFER_PERCENT_RANGE` | نسبة > 100 | Percentage خاطئ |
| `OFFER_NO_TARGET` | لا هدف للعرض | لا منتج/تصنيف |
| `OFFER_USAGE_EXCEEDED` | استُنفد الحد | CurrentUsageCount ≥ Max |
| `OFFER_NOT_APPLICABLE` | لا يطابق الشروط | فشل تقييم القواعد |
| `OFFER_STACK_CONFLICT` | تعارض تجميع | عروض غير Stackable تتصادم |
| `OFFER_EXPIRED` | العرض منتهٍ | خارج النافذة (يُستبعَد صمتاً في POS) |

---

## 15) الأداء (Performance)

- **جلب المرشّحين** يعتمد على `IX_Offers_Active_Window` — يقلّص المجموعة لعروض ساريّة فقط قبل تقييم القواعد.
- **الكاش:** العروض النشطة للفرع تُخزَّن في Redis وتُبطَّل عند أي تعديل/انتقال حالة؛ التقييم في POS يعمل غالباً من الذاكرة (< 5ms).
- **تقييم القواعد** خفيف (in-memory) على مجموعة صغيرة بعد التصفية الزمنية.
- **تقرير الأداء** يستفيد من فهرس `IX_OfferUsage_Offer` مع `INCLUDE` للأعمدة المجمّعة (Covering Index) — بلا Key Lookups.
- **Hangfire** الدوري خفيف (UPDATE مُفهرَس على حالة/تاريخ فقط).
- **صافي المرتجعات** محسوب من عمود مُحدَّث `QtyReturned` بدل JOIN ثقيل مع جداول المرتجعات وقت التقرير.

---

## 16) الأمان (Security)

- **العزل:** كل عرض/قاعدة/استخدام مُقيَّد بـ `TenantId` عبر Global Query Filter؛ لا تسرّب عبر المستأجرين.
- **الصلاحيات:** الإنشاء/التعديل/التفعيل/التقارير محميّة بـ Policies منفصلة؛ التطبيق التلقائي لا يمنح الكاشير أي امتياز إداري.
- **سلامة الأداء:** `OfferUsage` يُكتب ضمن معاملة الفاتورة (Atomic)؛ لا يمكن تزوير خصم دون سجل مطابق.
- **منع تجاوز الحدود:** فحص `MaxUsageTotal` ذرّي تحت التزامن (`UPDATE ... WHERE CurrentUsageCount < MaxUsageTotal`) لمنع السباق (Race Condition).
- **عدم رجعية الخصم:** الخصومات snapshot؛ لا يمكن لتغيير عرض أن يعيد كتابة فواتير ماضية.
- **التدقيق الكامل:** كل انتقال حالة (يدوي أو تلقائي) وكل تطبيق مسجَّل — شفافية كاملة لسبب كل خصم.

---

_يلتزم هذا الملف بالمعايير الحاكمة في [04-Database-Design.md](04-Database-Design.md). يتكامل مع محرّك POS في [18-POS.md](18-POS.md) وتقارير المرتجعات في [16-Sales-Returns.md](16-Sales-Returns.md)._
