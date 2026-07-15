# 06 — Roles & Permissions (الأدوار والصلاحيات والتفويض)

> يوثّق هذا الملف طبقة **التفويض (Authorization)**: مَن يحقّ له فعل ماذا. المصادقة (مَن أنت؟) في [05-Authentication.md](05-Authentication.md). النموذج المعتمد هجين: **RBAC** (أدوار) + **Permission-Based** (صلاحيات دقيقة `Resource.Action`). يلتزم بمعايير [04-Database-Design.md](04-Database-Design.md).

---

## 1) المبدأ الحاكم: RBAC + Permissions

| النموذج | الوصف | لماذا |
|---------|-------|-------|
| **Role-Based (RBAC)** | تجميع الصلاحيات في أدوار جاهزة (Cashier, Manager…) | سهولة التعيين وإدارة الفرق |
| **Permission-Based** | فحص فعلي على مستوى `Resource.Action` (مثل `Products.Create`) | حبيبية دقيقة ومرونة لكل مستأجر |
| **الجمع بينهما** | الدور = حاوية صلاحيات. الكود يفحص **الصلاحية** لا الدور | تغيير محتوى الدور دون تعديل الكود |

**القاعدة الذهبية:** الكود **لا يفحص الأدوار مباشرةً** (`if role == "Manager"`) بل يفحص **الصلاحيات** (`[HasPermission("Products.Delete")]`). هذا يجعل الأدوار قابلة للتخصيص لكل مستأجر دون لمس الكود.

---

## 2) نموذج الصلاحيات (Permission Model: `Resource.Action`)

كل صلاحية سلسلة `Resource.Action`. الأفعال القياسية (CRUD + عمليات نطاقية):

| Action | المعنى |
|--------|--------|
| `Create` / `Read` / `Update` / `Delete` | العمليات الأساسية |
| `Approve` / `Post` / `Void` / `Cancel` | اعتماد/ترحيل/إلغاء المستندات |
| `Export` / `Print` | تصدير وطباعة |
| `Operate` | تشغيل (مثل `Pos.Operate`) |
| `Manage` | إدارة كاملة (superset لكل الأفعال على المورد) |

**الموارد (Resources) الرئيسية:** `Products`, `Categories`, `Suppliers`, `Customers`, `Warehouses`, `Inventory`, `Purchases`, `Sales`, `SalesReturns`, `PurchaseReturns`, `Pos`, `Offers`, `Reports`, `Users`, `Roles`, `Settings`, `Stores`, `Dashboard`.

أمثلة: `Sales.Create`, `Sales.Void`, `Inventory.Adjust`, `Reports.Export`, `Users.Manage`, `Settings.Update`.

---

## 3) جداول التفويض (Authorization Tables)

### 3.1 `Roles`

```sql
CREATE TABLE [dbo].[Roles]
(
    [Name]             NVARCHAR(100)  NOT NULL,
    [NormalizedName]   NVARCHAR(100)  NOT NULL,
    [Description]      NVARCHAR(300)  NULL,
    [IsSystemRole]     BIT            NOT NULL CONSTRAINT DF_Roles_IsSystem DEFAULT (0), -- لا يُحذف/يُعدَّل
    [IsDefault]        BIT            NOT NULL CONSTRAINT DF_Roles_IsDefault DEFAULT (0),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,   -- الأدوار المخصّصة مملوكة للمستأجر
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_Roles_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_Roles_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Roles_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Roles_Tenant_Name]
    ON [dbo].[Roles] ([TenantId], [NormalizedName]) WHERE [IsDeleted] = 0;
GO
```

### 3.2 `Permissions` (كتالوج مرجعي عام)

```sql
CREATE TABLE [dbo].[Permissions]
(
    [Id]           INT          IDENTITY(1,1) NOT NULL,
    [Key]          VARCHAR(80)  NOT NULL,   -- 'Sales.Create'
    [Resource]     VARCHAR(40)  NOT NULL,   -- 'Sales'
    [Action]       VARCHAR(30)  NOT NULL,   -- 'Create'
    [Module]       VARCHAR(40)  NOT NULL,   -- 'Sales' (للتجميع في الواجهة)
    [DisplayNameAr] NVARCHAR(150) NOT NULL,
    [DisplayNameEn] NVARCHAR(150) NOT NULL,
    [IsActive]     BIT          NOT NULL CONSTRAINT DF_Permissions_IsActive DEFAULT (1),
    CONSTRAINT [PK_Permissions] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UX_Permissions_Key] UNIQUE ([Key])
);
GO
```

> `Permissions` **جدول مرجعي عام (Reference Data)** بلا `TenantId` — الكتالوج نفسه ثابت عبر المنصّة (كما `Countries`). ما يختلف بين المستأجرين هو *أي* صلاحيات مُسنَدة لأي أدوار (`RolePermissions`).

### 3.3 `RolePermissions`

```sql
CREATE TABLE [dbo].[RolePermissions]
(
    [RoleId]           BIGINT       NOT NULL,
    [PermissionId]     INT          NOT NULL,
    [IsGranted]        BIT          NOT NULL CONSTRAINT DF_RolePerms_Granted DEFAULT (1), -- يدعم Deny صريح

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_RolePerms_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_RolePerms_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_RolePermissions] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_RolePerms_Role]  FOREIGN KEY ([RoleId])       REFERENCES [dbo].[Roles]([Id]),
    CONSTRAINT [FK_RolePerms_Perm]  FOREIGN KEY ([PermissionId]) REFERENCES [dbo].[Permissions]([Id]),
    CONSTRAINT [FK_RolePerms_Tenant] FOREIGN KEY ([TenantId])    REFERENCES [dbo].[Tenants]([Id])
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_RolePerms_Role_Perm]
    ON [dbo].[RolePermissions] ([RoleId], [PermissionId]) WHERE [IsDeleted] = 0;
GO
```

### 3.4 `UserRoles`

```sql
CREATE TABLE [dbo].[UserRoles]
(
    [UserId]           BIGINT       NOT NULL,
    [RoleId]           BIGINT       NOT NULL,
    [AssignedStoreId]  BIGINT       NULL,   -- الدور محصور بفرع معيّن (NULL = كل الفروع)

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT       IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT       NOT NULL,
    [StoreId]          BIGINT       NULL,
    [CreatedDate]      DATETIME2(3) NOT NULL CONSTRAINT DF_UserRoles_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT       NULL,
    [ModifiedDate]     DATETIME2(3) NULL,
    [ModifiedBy]       BIGINT       NULL,
    [DeletedDate]      DATETIME2(3) NULL,
    [DeletedBy]        BIGINT       NULL,
    [IsDeleted]        BIT          NOT NULL CONSTRAINT DF_UserRoles_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION   NOT NULL,

    CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_UserRoles_User]   FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT [FK_UserRoles_Role]   FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles]([Id]),
    CONSTRAINT [FK_UserRoles_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_UserRoles_User_Role_Store]
    ON [dbo].[UserRoles] ([UserId], [RoleId], [AssignedStoreId]) WHERE [IsDeleted] = 0;
GO
```

---

## 4) الأدوار المُدمجة وصلاحياتها (Built-in Roles)

| الدور | النطاق | الوصف |
|-------|--------|-------|
| **Super Admin** | Platform | فوق المستأجرين — إدارة المنصّة، الفوترة، إنشاء المستأجرين. لا يرى بيانات أعمال المستأجرين افتراضياً |
| **Company Owner** | Tenant | كل شيء داخل مستأجره: الفروع، المستخدمون، الأدوار، الإعدادات، كل الوحدات |
| **Branch Manager** | Store | إدارة فرع واحد: مبيعات، مخزون، تقارير الفرع، مستخدمو الفرع. لا يُدير الأدوار العامة |
| **Cashier** | Store | تشغيل POS، إنشاء فواتير بيع، مرتجعات محدودة، لا يرى التكاليف |
| **Warehouse Employee** | Store | استلام بضاعة، تسوية/تحويل مخزون، جرد. لا مبيعات |
| **Sales Employee** | Store | إنشاء فواتير بيع وعروض أسعار، إدارة العملاء. لا مشتريات |
| **Purchase Employee** | Store | فواتير شراء، أوامر شراء، إدارة الموردين. لا مبيعات |
| **Accountant** | Tenant | تقارير مالية، مدفوعات، ترحيل، تصدير. قراءة واسعة، تعديل محاسبي |
| **Read Only** | Tenant/Store | قراءة فقط لكل ما يُسمح له، بلا أي تعديل — للمراجعة والتدقيق |

> `Super Admin` و`Company Owner` أدوار نظام (`IsSystemRole = 1`) لا تُحذف. الباقي **قوالب** يمكن للمستأجر تعديل صلاحياتها أو استنساخها.

---

## 5) مصفوفة الصلاحيات (Permission Matrix)

الرموز: **F** = Full (CRUD+)، **R** = Read، **C** = Create/Update محدود، **–** = لا صلاحية.

| Module ↓ / Role → | Super&nbsp;Admin | Company&nbsp;Owner | Branch&nbsp;Mgr | Cashier | Warehouse | Sales | Purchase | Accountant | Read&nbsp;Only |
|-------------------|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Products**      | F¹ | F  | F  | R  | R  | R  | R  | R  | R |
| **Categories**    | F¹ | F  | C  | R  | R  | R  | R  | R  | R |
| **Suppliers**     | –  | F  | R  | –  | R  | –  | F  | R  | R |
| **Customers**     | –  | F  | F  | C  | –  | F  | –  | R  | R |
| **Warehouses**    | –  | F  | C  | –  | C  | –  | –  | R  | R |
| **Inventory**     | –  | F  | F  | R  | F  | R  | R  | R  | R |
| **Purchases**     | –  | F  | R  | –  | C² | –  | F  | R  | R |
| **Sales**         | –  | F  | F  | C³ | –  | F  | –  | R  | R |
| **SalesReturns**  | –  | F  | F  | C⁴ | –  | C  | –  | R  | R |
| **PurchaseReturns**| – | F  | R  | –  | C  | –  | F  | R  | R |
| **Pos**           | –  | F  | F  | F⁵ | –  | R  | –  | –  | – |
| **Offers**        | –  | F  | C  | R  | –  | R  | –  | R  | R |
| **Reports**       | R  | F  | R⁶ | R⁷ | R⁷ | R⁷ | R⁷ | F  | R |
| **Users**         | F¹ | F  | C⁸ | –  | –  | –  | –  | –  | – |
| **Roles**         | F¹ | F  | –  | –  | –  | –  | –  | –  | – |
| **Settings**      | F¹ | F  | R  | –  | –  | –  | –  | R  | – |
| **Stores**        | F¹ | F  | R  | –  | –  | –  | –  | R  | R |
| **Dashboard**     | F¹ | F  | R  | R  | R  | R  | R  | R  | R |

**حواشٍ:** ¹ على مستوى المنصّة أو المستأجر · ² استلام أوامر الشراء فقط · ³ لا يرى التكلفة/هامش الربح · ⁴ ضمن حدّ مبلغ يحدّده المدير · ⁵ يشمل `Pos.Operate` + فتح/إغلاق الشفت · ⁶ تقارير فرعه فقط · ⁷ تقارير الوحدة الخاصّة به فقط · ⁸ مستخدمو فرعه فقط، دون منح أدوار أعلى منه.

---

## 6) تحميل الصلاحيات في JWT

عند نجاح تسجيل الدخول، نحسب **الاتحاد الفعّال (Effective Permissions)** للمستخدم ونضعه في claim `perm`:

```sql
-- الصلاحيات الفعّالة = كل صلاحيات أدوار المستخدم، مع احترام Deny الصريح
SELECT DISTINCT p.[Key]
FROM   UserRoles ur
JOIN   RolePermissions rp ON rp.RoleId = ur.RoleId AND rp.IsDeleted = 0
JOIN   Permissions p       ON p.Id = rp.PermissionId AND p.IsActive = 1
WHERE  ur.UserId = @UserId
  AND  ur.TenantId = @TenantId
  AND  ur.IsDeleted = 0
  AND  rp.IsGranted = 1
  AND  NOT EXISTS (   -- استبعاد ما مُنِع صراحةً في أي دور (Deny يغلب Grant)
        SELECT 1 FROM RolePermissions d
        JOIN UserRoles ur2 ON ur2.RoleId = d.RoleId AND ur2.UserId = ur.UserId
        WHERE d.PermissionId = rp.PermissionId AND d.IsGranted = 0 AND d.IsDeleted = 0);
```

الناتج يُسطَّح في الـ JWT: `"perm": ["Sales.Create","Sales.Read","Pos.Operate", ...]`. إن كبر الحجم (Company Owner بكل الصلاحيات) نضع بدلاً منه `"perm_ref"` (نسخة مخزّنة في Redis) — انظر [05-Authentication.md](05-Authentication.md) §3.

---

## 7) سياسات التفويض في .NET (Authorization Policies)

نسجّل سياسة واحدة لكل صلاحية ديناميكياً، بدلاً من تعريف كل واحدة يدوياً:

```csharp
// Policy Provider ديناميكي: أي policy باسم "Perm:Sales.Create" تُبنى تلقائياً
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    public Task<AuthorizationPolicy?> GetPolicyAsync(string name)
    {
        if (name.StartsWith("Perm:"))
        {
            var perm = name["Perm:".Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(perm))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return _fallback.GetPolicyAsync(name);
    }
}

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx, PermissionRequirement req)
    {
        // يقرأ claims "perm" الموضوعة في JWT — لا استعلام DB
        if (ctx.User.HasClaim("perm", req.Permission))
            ctx.Succeed(req);
        return Task.CompletedTask;
    }
}
```

## 8) الـ `[HasPermission]` Attribute المخصّص

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission) => Policy = $"Perm:{permission}";
}
```

الاستخدام على الـ Controllers/Endpoints:

```csharp
[ApiController, Route("api/sales")]
public class SalesController : ControllerBase
{
    [HttpPost]
    [HasPermission("Sales.Create")]          // ⇐ يفحص الصلاحية لا الدور
    public Task<IActionResult> Create(...) { ... }

    [HttpPost("{id}/void")]
    [HasPermission("Sales.Void")]
    public Task<IActionResult> Void(long id) { ... }

    [HttpGet("report")]
    [HasPermission("Reports.Export")]
    public Task<IActionResult> Export(...) { ... }
}
```

> **دفاع في العمق:** إلى جانب فحص الـ attribute (Coarse-grained)، نفحص أيضاً على مستوى البيانات (Fine-grained): `TenantId` عبر Global Query Filter، و`StoreId` مقابل `UserRoles.AssignedStoreId` — فلا يستطيع كاشير فرع أن يفتح فاتورة فرع آخر حتى لو امتلك `Sales.Read`.

---

## 9) Claims المستخدمة في التفويض

| Claim | المصدر | الاستخدام |
|-------|--------|-----------|
| `tenant_id` | `Users.TenantId` | العزل — يُطابَق ضدّ كل استعلام |
| `store_id` | الفرع النشط | حصر العمليات بالفرع |
| `user_id` | `Users.Id` | التدقيق و`CreatedBy` |
| `roles` | `UserRoles` | عرض/تصنيف فقط — **لا** يُفحَص في الكود |
| `perm` | `RolePermissions` (مُسطّح) | ★ الفحص الفعلي للتفويض |
| `security_stamp` | `Users.SecurityStamp` | إبطال فوري عند تغيّر الصلاحيات |

**إبطال فوري:** أي تعديل على صلاحيات دور أو أدوار مستخدم ⇒ تدوير `SecurityStamp` للمتأثّرين ⇒ رفض الـ JWT الحالية عند أوّل طلب حسّاس ⇒ إجبار على `refresh` يُصدِر claims محدّثة.

---

## 10) الصلاحيات القابلة للإعداد لكل مستأجر (Per-Tenant Configurable)

- الأدوار المُدمجة تُنشأ للمستأجر عبر **Seeding** عند تهيئته (Templates تُنسخ إلى `Roles` + `RolePermissions` بـ `TenantId` الخاص به).
- `Company Owner` يستطيع من شاشة *إدارة الأدوار*:
  - **تعديل** صلاحيات أي دور غير نظامي (منح/منع `RolePermissions.IsGranted`).
  - **إنشاء أدوار مخصّصة** (مثل «مشرف الورديّة المسائية») باختيار صلاحيات من الكتالوج.
  - **حصر دور بفرع** عبر `UserRoles.AssignedStoreId`.
- لا يمكن لأي مستأجر رؤية أو تعديل أدوار مستأجر آخر (كلها مفلترة بـ `TenantId`).
- الصلاحيات الحسّاسة (`Roles.Manage`, `Settings.Update`, `Users.Manage`) لا تُمنح لأدوار التشغيل افتراضياً، وتتطلّب موافقة `Company Owner`.

```
تدفّق تعديل صلاحية:
Owner يُبدّل Sales.Void لدور Cashier ──▶ UPDATE RolePermissions.IsGranted
      ──▶ tenant SecurityStamp bump للمتأثّرين ──▶ JWT القديمة تُرفَض
      ──▶ عند refresh تُحمَّل الصلاحيات الجديدة في claim perm
```

---

_يلتزم هذا الملف بمعايير [04-Database-Design.md](04-Database-Design.md). المصادقة وإصدار الرموز في [05-Authentication.md](05-Authentication.md)._
