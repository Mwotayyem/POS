# 09 — Categories (التصنيفات الشجرية)

> التصنيفات تنظّم المنتجات في **بنية شجرية هرمية** (Parent-Child) غير محدودة العمق عملياً. تُستخدم في التصفية، التقارير، الصلاحيات، وقوائم POS. يعتمد التصميم على مرجع ذاتي (`ParentId`) مع منع تكوّن الحلقات (Cycles).

---

## 1) الهدف من الصفحة (Purpose)

- إنشاء وتعديل وأرشفة تصنيفات المنتجات ضمن شجرة هرمية.
- عرض الشجرة (Tree View) بالسحب والإفلات (Drag & Drop) لإعادة الترتيب/النقل.
- ربط كل تصنيف بمنتجات، وتصفية المنتجات حسب الفرع وأبنائه.

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية | الوصف |
|----------|-------|
| `categories.view` | عرض الشجرة |
| `categories.create` | إضافة تصنيف |
| `categories.edit` | تعديل/نقل تصنيف |
| `categories.delete` | أرشفة تصنيف |

---

## 3) تصميم الصفحة (Page Layout)

```
┌───────────────────────────────┬──────────────────────────────┐
│  Category Tree   [+ Root]     │  Details / Edit               │
├───────────────────────────────┤  Name:   [ Beverages       ]  │
│ ▾ Beverages            (120)  │  Parent: [ — Root —      ▾ ]  │
│   • Hot Drinks          (40)  │  Code:   [ BEV             ]  │
│   ▾ Cold Drinks         (80)  │  Sort:   [ 1 ]  Active [✓]    │
│       • Juices          (35)  │  Image:  [ upload ]           │
│       • Sodas           (45)  │                               │
│ ▾ Food                 (300)  │  [ Save ]  [ Delete ]         │
│   • Snacks              (90)  │                               │
└───────────────────────────────┴──────────────────────────────┘
```

الرقم بين قوسين = عدد المنتجات في الفرع وأبنائه (Rollup count).

---

## 4) جميع الأزرار (Buttons)

| الزر | الوظيفة | الصلاحية |
|------|---------|----------|
| **+ Root** | إضافة تصنيف جذري (`ParentId = NULL`) | create |
| **+ Child** (زر سياقي على العقدة) | إضافة تصنيف فرعي | create |
| **Save** | حفظ التصنيف | create/edit |
| **Delete** | أرشفة بعد التحقّق من الأبناء/المنتجات | delete |
| **Drag & Drop** | نقل عقدة إلى أب آخر (يستدعي PATCH move) | edit |
| **Expand/Collapse All** | فتح/طيّ الشجرة | view |

---

## 5) جميع الحقول (Fields)

| الحقل | النوع | إلزامي | ملاحظات |
|-------|------|:------:|---------|
| `Name` | نص (150) | ✔ | فريد بين الإخوة (نفس الأب) |
| `ParentId` | قائمة/شجرة | — | `NULL` = جذر |
| `Code` | نص (30) | — | رمز مختصر اختياري فريد داخل المستأجر |
| `Description` | نص | — | وصف |
| `ImageUrl` | ملف/رابط | — | أيقونة التصنيف |
| `SortOrder` | INT | — | ترتيب العرض بين الإخوة |
| `IsActive` | Boolean | ✔ | افتراضي `true` |

---

## 6) التحقق (Validation)

- `Name` مطلوب وفريد بين الأشقّاء (`ParentId + Name` فريد داخل المستأجر).
- `ParentId` (إن وُجد) يجب أن يكون تصنيفاً قائماً غير محذوف ومن نفس المستأجر.
- **منع الحلقات (Cycle Prevention):** لا يجوز جعل عقدة أباً لأحد أسلافها، ولا أباً لنفسها.
- عمق الشجرة الموصى به ≤ 6 مستويات (تحذير عند التجاوز، لا منع صارم).

---

## 7) قواعد العمل (Business Rules)

1. **الجذر:** `ParentId = NULL` يعني تصنيفاً جذرياً.
2. **مسار مادّي (Materialized Path):** يُخزَّن عمود `Path` (مثل `/1/15/40/`) و`Depth` لتسريع الاستعلامات الشجرية وحساب الأسلاف/الأحفاد دون Recursion متكرّر.
3. **إعادة النقل (Move):** عند تغيير `ParentId` يُعاد بناء `Path` و`Depth` للعقدة **ولكل أحفادها** داخل Transaction واحد.
4. **العدّ التراكمي (Rollup):** عدد منتجات الفرع = منتجات العقدة + منتجات كل الأحفاد (عبر `Path LIKE`).
5. **التصفية:** فلترة منتجات تصنيف تشمل افتراضياً كل الأحفاد (Descendants).

---

## 8) جداول قاعدة البيانات (Database Tables)

### 8.1 Categories

```sql
CREATE TABLE [dbo].[Categories]
(
    [Name]        NVARCHAR(150) NOT NULL,
    [ParentId]    BIGINT        NULL,          -- مرجع ذاتي (Self-reference)
    [Code]        NVARCHAR(30)  NULL,
    [Description] NVARCHAR(500) NULL,
    [ImageUrl]    NVARCHAR(500) NULL,
    [Path]        NVARCHAR(900) NOT NULL CONSTRAINT DF_Categories_Path DEFAULT ('/'), -- Materialized Path مثل '/1/15/40/'
    [Depth]       INT           NOT NULL CONSTRAINT DF_Categories_Depth DEFAULT (0),
    [SortOrder]   INT           NOT NULL CONSTRAINT DF_Categories_Sort DEFAULT (0),
    [IsActive]    BIT           NOT NULL CONSTRAINT DF_Categories_Active DEFAULT (1),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_Categories_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_Categories_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_Categories] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Categories_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_Categories_Store]  FOREIGN KEY ([StoreId])  REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [FK_Categories_Parent] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[Categories]([Id]),
    CONSTRAINT [CK_Categories_NotSelfParent] CHECK ([ParentId] IS NULL OR [ParentId] <> [Id])
);
GO

-- تفرّد الاسم بين الأشقّاء داخل المستأجر (ParentId قد يكون NULL للجذور)
CREATE UNIQUE NONCLUSTERED INDEX [UX_Categories_Tenant_Parent_Name]
    ON [dbo].[Categories] ([TenantId], [ParentId], [Name]) WHERE [IsDeleted] = 0;
GO
CREATE NONCLUSTERED INDEX [IX_Categories_Parent]
    ON [dbo].[Categories] ([TenantId], [ParentId]) WHERE [IsDeleted] = 0;
GO
-- فهرس المسار لاستعلامات الأحفاد (Path LIKE '/1/15/%')
CREATE NONCLUSTERED INDEX [IX_Categories_Path]
    ON [dbo].[Categories] ([TenantId], [Path]) WHERE [IsDeleted] = 0;
GO
```

> **ملاحظة FK:** المرجع الذاتي يُعرَّف بـ `ON DELETE NO ACTION` (الحذف soft) لتفادي مسارات حذف تعاقبي (cascade cycles) التي يرفضها SQL Server.

---

## 9) الـ API

**Base:** `/api/v1/categories`

| Method | Endpoint | الوصف | Policy |
|--------|----------|-------|--------|
| GET | `/categories/tree` | الشجرة الكاملة (مُتداخلة) | `categories.view` |
| GET | `/categories/{id}` | تفاصيل تصنيف + أبناؤه المباشرون | `categories.view` |
| POST | `/categories` | إنشاء تصنيف | `categories.create` |
| PUT | `/categories/{id}` | تعديل الحقول | `categories.edit` |
| PATCH | `/categories/{id}/move` | نقل إلى أب جديد | `categories.edit` |
| DELETE | `/categories/{id}` | أرشفة (بشروط) | `categories.delete` |

### مثال: GET /categories/tree (Response)

```json
[
  {
    "id": 1, "name": "Beverages", "parentId": null, "depth": 0, "productCount": 120,
    "children": [
      { "id": 15, "name": "Hot Drinks", "parentId": 1, "depth": 1, "productCount": 40, "children": [] },
      { "id": 16, "name": "Cold Drinks", "parentId": 1, "depth": 1, "productCount": 80,
        "children": [
          { "id": 40, "name": "Juices", "parentId": 16, "depth": 2, "productCount": 35, "children": [] }
        ] }
    ]
  }
]
```

### مثال: PATCH /categories/40/move (Request/Response)

```json
// Request — نقل "Juices" لتصبح تحت "Beverages" مباشرة
{ "newParentId": 1 }

// Response 200
{ "id": 40, "parentId": 1, "path": "/1/40/", "depth": 1, "affectedDescendants": 3 }

// Response 409 — محاولة إنشاء حلقة
{ "error": "CATEGORY_CYCLE", "message": "Cannot move a category under one of its own descendants." }
```

---

## 10) مخطط التدفّق — النقل ومنع الحلقات (Move & Cycle Guard)

```
 [PATCH /{id}/move  newParentId=P]
            │
            ▼
 [id == P ?] ──yes──► [409 CATEGORY_CYCLE]
            │no
            ▼
 [Is P a descendant of id ?]         (فحص: P.Path LIKE id.Path + '%')
            │
     yes ───┴─── no
      │            │
      ▼            ▼
 [409 CYCLE]  [BEGIN TRANSACTION]
                 ├─ Update node.ParentId = P
                 ├─ Rebuild node.Path, node.Depth
                 ├─ Update ALL descendants' Path/Depth
                 │     (REPLACE old prefix with new prefix)
                 └─ INSERT AuditLog
              [COMMIT] ──► [200 OK]
```

**خوارزمية كشف الحلقة:** العقدة الهدف `P` لا يجوز أن تكون العقدة نفسها ولا أحد أحفادها. الأحفاد يُعرَفون بأن `Path` الخاص بهم يبدأ بـ `Path` العقدة المنقولة. فإن كان `P.Path` يحتوي `/{id}/` رُفض النقل.

---

## 11) ماذا يحدث عند: الحذف / التعديل / النقل

### الحذف (Delete)
- **يُمنع الحذف إن كان للتصنيف أبناء غير محذوفين** → `409 CATEGORY_HAS_CHILDREN`. يجب نقل/حذف الأبناء أولاً، أو استخدام حذف تعاقبي صريح (اختياري وبتأكيد ثانٍ).
- **يُمنع الحذف إن كان للتصنيف منتجات مرتبطة** → `409 CATEGORY_HAS_PRODUCTS`. الخيارات: إعادة تصنيف المنتجات، أو نقلها لتصنيف "غير مصنّف" (Uncategorized).
- الحذف soft فقط (`IsDeleted=1`).

### التعديل (Edit)
- تغيير الاسم يتحقّق من تفرّده بين الأشقّاء.
- تغيير `SortOrder` يعيد ترتيب العرض فقط.
- التحقق من `ConcurrencyStamp`.

### النقل (Move) — إعادة بناء المسار
- عند تغيير الأب، يُعاد حساب `Path` و`Depth` للعقدة وكل أحفادها في عملية واحدة عبر تحديث بادئة المسار:
  `SET Path = REPLACE(Path, @oldPrefix, @newPrefix)` لكل صف حيث `Path LIKE @oldPrefix + '%'`.

---

## 12) سجل التدقيق (Audit Log)

| الحدث | التسجيل |
|-------|---------|
| Create | `Action=Create, After={name, parentId}` |
| Edit | diff للحقول |
| Move | `Action=Move, oldParentId → newParentId, affectedDescendants` |
| Delete | `Action=Delete` + عدد المنتجات المعاد تصنيفها |

---

## 13) الأخطاء المحتملة (Possible Errors)

| الكود | السبب |
|-------|-------|
| `409 CATEGORY_CYCLE` | محاولة نقل عقدة تحت أحد أحفادها |
| `409 CATEGORY_HAS_CHILDREN` | حذف تصنيف له أبناء |
| `409 CATEGORY_HAS_PRODUCTS` | حذف تصنيف له منتجات |
| `409 NAME_DUPLICATE_SIBLING` | اسم مكرّر بين الأشقّاء |
| `404 PARENT_NOT_FOUND` | الأب المحدّد غير موجود |
| `409 CONCURRENCY_CONFLICT` | تعارض تعديل متزامن |

---

## 14) الأداء (Performance)

- **Materialized Path + Depth** يلغي الحاجة إلى استعلامات تكرارية (Recursive CTE) الثقيلة عند القراءة؛ الأحفاد عبر `Path LIKE '/1/15/%'` (فهرس على `Path`).
- بناء الشجرة المتداخلة يتم بقراءة مسطّحة واحدة (Flat) ثم تجميعها في الذاكرة بالكود.
- عدّ المنتجات (Rollup) يُخزَّن مؤقتاً (Cache) ويُعاد حسابه عند تغيّر ربط المنتجات.
- شجرة التصنيفات مرشّحة قوية للـ Caching (نادرة التغيّر) — يُبطَل الكاش عند أي create/move/delete.

---

## 15) الأمان (Security)

- كل عملية مفلترة بـ `TenantId` — لا يمكن رؤية أو نقل تصنيف مستأجر آخر.
- التحقق من أن `newParentId` ينتمي لنفس المستأجر قبل النقل.
- `Path` يُبنى خادمياً فقط ولا يُقبل من العميل (منع تزوير التسلسل الهرمي).
- عمليات النقل الجماعية مغلّفة في Transaction لتفادي شجرة غير متّسقة عند الفشل.
