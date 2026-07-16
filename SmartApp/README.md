# SmartApp — منصّة إدارة أعمال (ASP.NET Core)

> **Solution فعلية** مبنية على **Clean Architecture** و **.NET 9**، متعدّدة المستأجرين (Multi-Tenant) بعزل تامّ للبيانات وإدارة تفعيل يدوية للعملاء — **بلا اشتراكات ولا فوترة ولا مدفوعات**.

**الحالة:** ✅ **المشروع مكتمل ومُتحقَّق منه (Phase 1–13).** Backend كامل (12 مرحلة، **117/117 اختبار**) + **Frontend حقيقي** (React + Vite + TypeScript). حلّ متكامل يفتح في Visual Studio وجاهز للتشغيل.

> **التحقّق النهائي (2026-07-16):** 9 مشاريع · Build **0/0** · **117/117 اختبار** · 6 migrations · **20 controller / 81 endpoint** · **32 جدولاً** · Kestrel يقلع و Swagger يخدم (`/swagger/v1/swagger.json` → 200) · Frontend build (112 module، 0 ثغرة) + dev (200 على `:5173`) · سلسلة الأعمال الكاملة (Login → Product → Customer → Purchase → Sales → Dashboard) مُتحقَّقة عبر HTTP. التفاصيل في [`PROJECT_SUMMARY.md`](SmartApp/PROJECT_SUMMARY.md#final-verification-2026-07-16). *(SQL Server LocalDB لم يُقلع في هذه البيئة؛ التدفّقات المعتمدة على قاعدة البيانات تحقّقت على نفس حزمة التطبيق عبر مضيف SQLite في الاختبارات.)*

---

## ما أُنجز في Phase 13 (Frontend Application)

واجهة أمامية احترافية (**ليست Demo**) في [`frontend/`](frontend/README.md)، تتّصل بالـ API الموجود.

- ✅ **التقنية المختارة: React + Vite + TypeScript (SPA).** لماذا؟ أفضل ملاءمة لـ REST API مع JWT (فصل معماري نظيف)، أكبر نظام بيئي وصيانة طويلة الأمد، وأمان أنواع يطابق صرامة الـ backend (الـ envelope والـ DTOs كأنواع).
- ✅ **المصادقة:** صفحة تسجيل دخول · JWT (access + refresh) · **refresh تلقائي عند 401** (طلب واحد مشترك ثم إعادة المحاولة) · تسجيل خروج · استعادة الجلسة من `/profile` عند إعادة التحميل.
- ✅ **التخويل:** القائمة الجانبية وإجراءات الصفحات **مُفلترة حسب صلاحيات المستخدم** (من `/profile`)، مطابقةً لسياسات `[HasPermission]` في الـ backend.
- ✅ **التخطيط:** Sidebar + Header + User menu · قائمة ديناميكية · تصميم متجاوب · RTL عربي · ثيم فاتح/داكن.
- ✅ **الصفحات (18):** لوحة معلومات · تقارير · ملف شخصي · الإدارة (المستخدمون/الأدوار مع إسناد الصلاحيات/الصلاحيات/إعدادات المستأجر) · الكتالوج (المنتجات/التصنيفات/الوحدات/العلامات) · المخزون (المستودعات/الأرصدة) · المشتريات (المورّدون/الفواتير) · المبيعات (العملاء/الفواتير).
- ✅ **الجودة:** بنية نظيفة (components/services/typed API clients/hooks) · معالجة أخطاء موحّدة (من `error.code`) · حالات تحميل · جدول بيانات ونافذة منبثقة قابلان لإعادة الاستخدام · **بناء ناجح** (`tsc` صارم + `vite`) بلا أخطاء (112 module).

> **المشروع مكتمل:** Backend (Phases 1–12) + Frontend (Phase 13).

---

## ما أُنجز في Phase 12 (Production Readiness)

تحصين وتجهيز للإنتاج، دون تغيير المعمارية:

- ✅ **Health Check** — `GET /health` (مجهول) يتحقّق من اتصال قاعدة البيانات (`DatabaseHealthCheck`)، لفحوص liveness/readiness.
- ✅ **CORS whitelist** — سياسة قابلة للضبط من `Cors:AllowedOrigins` (المنشأ نفسه فقط افتراضياً).
- ✅ **Rate Limiting** — نافذة ثابتة عامّة، **opt-in** عبر `RateLimiting:Enabled` (مطفأة في الاختبارات/التطوير، مفعّلة في الإنتاج).
- ✅ **`appsettings.Production.json`** — قيم إنتاجية آمنة (الأسرار من متغيّرات البيئة، Swagger مطفأ في الإنتاج، مستويات تسجيل أعلى).
- ✅ **[SECURITY.md](SECURITY.md)** — مراجعة OWASP Top-10 كاملة + مراجعة Authorization + مراجعة عزل المستأجر (كل نقطة موثّقة بما هو منفّذ فعلاً).
- ✅ **[DEPLOYMENT.md](DEPLOYMENT.md)** — الإعدادات والأسرار · تطبيق الـ migrations · التشغيل/النشر · الصحّة والتسجيل والمراقبة · **استراتيجية النسخ الاحتياطي والاستعادة** · مراجعة الأداء والفهارس · قائمة تحقّق ما قبل الإطلاق.
- ✅ **اختبار (1 جديد، 116/116 إجمالاً):** `/health` مجهول ويعيد Healthy. Build 0/0 · بلا model drift.

> **Backend مكتمل.** المتبقّي: Phase 13 — Frontend.

---

## ما أُنجز في Phase 11 (Dashboard & Reports API)

طبقة تقارير **للقراءة فقط** (بلا جداول/migration جديدة) فوق البيانات الموجودة، كلها معزولة بالمستأجر ومحميّة بـ `[HasPermission("reports.view")]`.

- ✅ **Dashboard** — `GET /api/v1/dashboard/summary?from&to`: إجمالي المبيعات + إجمالي المشتريات + **إجمالي الربح** (Σ (UnitPrice−UnitCost)×(Quantity−ReturnedQty)) + الذمم المدينة/الدائنة القائمة + عدد الفواتير + عدد المنتجات تحت حدّ إعادة الطلب. (نطاق افتراضي: آخر 30 يوماً.)
- ✅ **Reports:**
  - `GET /reports/sales?from&to` — المبيعات مجمّعة حسب اليوم.
  - `GET /reports/low-stock` — المنتجات عند/تحت `ReorderLevel`.
  - `GET /reports/products?from&to` — أداء المنتجات (كمية/إيراد/تكلفة/ربح) مرتّبة بالربح.
- ✅ **CQRS خالص** — كل تقرير Query + Handler يجمّع عبر EF (المستأجر مُفلتَر تلقائياً)، الفواتير الملغاة مستثناة، والكميات المرتجعة مطروحة.
- ✅ **اختبارات (7 جديدة، 115/115 إجمالاً):** الملخّص يعكس المبيعات/المشتريات/الربح · كشف المخزون المنخفض (عدّاد + قائمة) · تقرير المنتجات يجمّع الإيراد/التكلفة/الربح · تقرير المبيعات اليومي · عزل المستأجر (مستأجر بلا نشاط = أصفار) · authorization.

> **لم يُنشأ بعد:** Production hardening (Phase 12) · Frontend (Phase 13).

---

## ما أُنجز في Phase 10 (Sales Module)

وحدة المبيعات — الوجه المقابل للمشتريات. مبنية مطابقةً لـ [06-Tables-Definitions.md §5–§6](../SmartApp-Architecture/06-Tables-Definitions.md).

- ✅ **6 كيانات:** `Customer` (رصيد A/R + CreditLimit)، `SalesInvoice`+Items (**UnitPrice سعر البيع + UnitCost لقطة WAC وقت البيع** للربحية، CustomerId اختياري لبيع نقدي، ReturnedQty بقيد CHECK)، `Payment` (دفعة عميل: Cash/Transfer/Card)، `SalesReturn`+Items.
- ✅ **فاتورة البيع (create = معاملة واحدة):** ترقيم + الفاتورة + البنود + **لقطة تكلفة WAC لكل بند** + حركة مخزون OUT عبر `IStockLedger` (يرفض البيع إن لم يكفِ المخزون) + **رفع رصيد العميل** (بالمبلغ غير المدفوع). حساب المجاميع.
- ✅ **الدفعات:** تسجيل دفعة عميل → تخفيض رصيده، وإن رُبطت بفاتورة تُحدّث `PaidAmount`.
- ✅ **مرتجع البيع:** يتحقّق من المتبقّي القابل للإرجاع (منع الزائد)، حركة مخزون IN (إعادة للمخزون بالتكلفة الأصلية)، **تخفيض رصيد العميل**، تحديث ReturnedQty وحالة الفاتورة.
- ✅ **API:** `/api/v1/customers` · `/sales-invoices` (+`/{id}/returns`) · `/payments`، محميّة بـ `[HasPermission("sales.*")]`.
- ✅ **Migration `AddSales`** — 6 جداول بكل الفهارس والقيود. **بلا model drift**.
- ✅ **اختبارات (13 جديدة، 108/108 إجمالاً):** Customer CRUD · البيع يخفّض المخزون + يرفع A/R + يلتقط التكلفة · رفض البيع فوق المتاح · دفعة جزئية تضيف غير المدفوع · دفعة تخفّض الرصيد وتحدّث PaidAmount · مرتجع يعيد للمخزون ويخفّض الرصيد ويحدّث الحالة · منع الإرجاع الزائد · بيع نقدي بلا عميل · عزل المستأجر · authorization.

> **لم يُنشأ بعد:** Reports/Dashboard · Frontend.

---

## ما أُنجز في Phase 9 (Purchasing Module)

وحدة المشتريات فوق الكتالوج والمخزون. مبنية مطابقةً لـ [06-Tables-Definitions.md §5–§7](../SmartApp-Architecture/06-Tables-Definitions.md) حيث تُعرَّف، مع توسعات موثّقة (Purchase Orders غير موجودة في الوثائق).

- ✅ **7 كيانات:** `Supplier` (رصيد A/P)، `PurchaseOrder`+Items (workflow: Draft→Confirmed→Received→Cancelled — greenfield)، `PurchaseInvoice`+Items (SupplierId، UnitPrice=التكلفة، ReturnedQty بقيد CHECK)، `PurchaseReturn`+Items. + كيان `DocumentSequence` لترقيم المستندات (per-tenant).
- ✅ **`IStockLedger` هو المدخل الوحيد للمخزون** — فاتورة الشراء تستدعيه لكل بند (وارد → WAC) بدل لمس جداول المخزون مباشرةً، مطابقةً لقاعدة حدود الوحدات في [04-Domain-Boundaries.md §2.4](../SmartApp-Architecture/04-Domain-Boundaries.md).
- ✅ **`IDocumentNumberService`** — ترقيم تسلسلي ذرّي داخل معاملة المستند (PO-/PINV-/PRET-...).
- ✅ **فاتورة الشراء (create = معاملة واحدة):** ترقيم + الفاتورة + البنود + حركة مخزون IN لكل بند + تحديث WAC + `Product.CostPrice` + **رصيد المورّد** (يزيد بالمبلغ غير المدفوع). حساب المجاميع (subtotal/discount/tax/grand).
- ✅ **مرتجع الشراء:** يتحقّق أن الكمية ≤ المتبقّي (منع الإرجاع الزائد)، حركة مخزون OUT، **يخفّض رصيد المورّد**، يزيد ReturnedQty، ويحدّث حالة الفاتورة (Partially/FullyReturned).
- ✅ **أمر الشراء:** CRUD + confirm/cancel، وربطه بالفاتورة عند الاستلام (Received). لا يمسّ المخزون.
- ✅ **API:** `/api/v1/suppliers` · `/purchase-orders` (+confirm/cancel) · `/purchase-invoices` (+`/{id}/returns`)، محميّة بـ `[HasPermission("purchasing.*")]`.
- ✅ **Migration `AddPurchasing`** — 8 جداول (7 مشتريات + DocumentSequences) بكل الفهارس والقيود. **بلا model drift**.
- ✅ **اختبارات (15 جديدة، 95/95 إجمالاً):** Supplier CRUD · فاتورة تُدخِل المخزون + WAC + رصيد المورّد + Product.CostPrice · WAC عند شراء ثانٍ · دفعة جزئية تضيف غير المدفوع فقط · مرتجع يعكس المخزون والرصيد والحالة · منع الإرجاع الزائد · workflow أمر الشراء (confirm/cancel/منع تأكيد الملغى) · فاتورة من أمر تجعله Received · عزل المستأجر · authorization.

> **لم يُنشأ بعد:** Sales · Reports · Frontend.

---

## ما أُنجز في Phase 8 (Inventory Module)

وحدة المخزون فوق الكتالوج، بنفس القوالب. **ملاحظة تصميم:** الطلب أضاف Warehouses + Stock لكل مستودع + Transfers، وهي **توسعة مقصودة وموثّقة** تتجاوز نموذج الوثائق (التي تحصر Stock بصف واحد لكل منتج بلا مستودعات) — مع الالتزام الحرفي بقواعد WAC و append-only.

- ✅ **3 كيانات:** `Warehouse` (مستودع، افتراضي واحد)، `Stock` (رصيد لكل product+warehouse: QtyOnHand + AvgCost)، `StockMovement` (**append-only** — IN/OUT/ADJUST/TRANSFER).
- ✅ **قاعدة Append-Only مفروضة على مستوى الـ Persistence** — `AuditableEntityInterceptor` يرمي استثناءً عند أي محاولة UPDATE/DELETE لأي كيان `IAppendOnly` (StockMovement). التصحيح = حركة معاكسة جديدة.
- ✅ **`IStockLedger`** — نقطة الدخول الوحيدة لتغيير المخزون: يحسب **WAC** بدقّة حسب [13-Development-Rules.md §6.2](../SmartApp-Architecture/13-Development-Rules.md) (وارد: `(qty*avg + inQty*inCost)/(qty+inQty)`؛ صادر: يُخصَم بالمتوسّط الحالي والمتوسّط لا يتغيّر)، يكتب الحركة، ويزامن `Product.CostPrice`. تستخدمه المبيعات/المشتريات لاحقاً عبر الواجهة لا مباشرةً.
- ✅ **العمليات:** Warehouses CRUD · Stock balances/movements queries · **Adjust** (كمية موقّعة → حركة) · **Transfer** (حركتان مرتبطتان OUT+IN بنفس المرجع، بتكلفة المصدر، في معاملة واحدة). المخزون السالب مرفوض.
- ✅ **API Controllers** — `/api/v1/warehouses` + `/api/v1/stock/{balances,movements,adjust,transfer}`، محميّة بـ `[HasPermission("inventory.*")]`.
- ✅ **Migration `AddInventory`** — 3 جداول + فهارس (رصيد فريد لكل product+warehouse، فهرس حركات by-product-date). **بلا model drift**.
- ✅ **اختبارات (19 جديدة، 80/80 إجمالاً):** Warehouse CRUD + قاعدة الافتراضي الواحد · WAC عند وارد ثانٍ · الصادر لا يغيّر المتوسّط · رفض ما دون الصفر · التحويل ينقل الكمية ويحفظ التكلفة · التحويل بمخزون غير كافٍ لا يترك حالة جزئية · Product.CostPrice يتتبّع WAC · append-only (insert ينجح، update/delete يرمي) · عزل المستأجر · authorization.

> **لم يُنشأ بعد:** Purchasing · Sales · Reports · Frontend.

---

## ما أُنجز في Phase 7 (Catalog Module)

أول Business Module — كتالوج المنتجات، بنفس القوالب (CQRS · Clean Architecture · Tenant Isolation · Response Envelope · Validation · Authorization). مبني مطابقةً لـ [06-Tables-Definitions.md §3](../SmartApp-Architecture/06-Tables-Definitions.md).

- ✅ **7 كيانات:** `Category` (شجرة عبر ParentId + SortOrder)، `Unit` (Name/Symbol/Precision)، `Brand` (greenfield)، `Product` (SKU/Category/Brand/BaseUnit/Cost/Sale/Tax/TrackStock/ReorderLevel/CustomFieldsJson)، `ProductUnit` (معامل تحويل DECIMAL(18,6))، `ProductBarcode` (متعدّد + Primary)، `ProductPrice` (أنواع أسعار: Retail/Wholesale/Distributor/Online).
- ✅ **EF Configurations** — فهارس فريدة مُفلترة per-tenant (`UX_Products_Tenant_Sku` · `UX_ProductBarcodes_Tenant_Barcode` · `UX_Units_Tenant_Name` · `UX_Brands_Tenant_Name` · `UX_ProductPrices_Tenant_Product_Type`)، قيود `CHECK` (أسعار ≥ 0، معامل تحويل > 0)، FKs بـ `NO ACTION`، ROWVERSION، `ISJSON` مُقيَّد بـ SQL Server.
- ✅ **CQRS كامل** — CRUD لكل من Categories/Units/Brands/Products + validators. المنتج يُنشأ/يُحدَّث مع مجموعاته الفرعية (units/barcodes/prices) في معاملة واحدة.
- ✅ **قواعد أعمال** — منع دورة التصنيفات (لا يكون أباً لنفسه أو لأحد فروعه)، منع حذف تصنيف له فروع/منتجات، منع حذف وحدة/علامة مستخدمة، تفرّد SKU والباركود على مستوى المستأجر، باركود رئيسي واحد كحدّ أقصى.
- ✅ **API Controllers** — `/api/v1/categories` · `/units` · `/brands` · `/products` (بحث + فلترة category/brand)، كلها محميّة بـ `[HasPermission("catalog.*")]`.
- ✅ **Migration `AddCatalog`** — 7 جداول بكل الفهارس والقيود. **بلا model drift**.
- ✅ **Swagger** — كل الـ endpoints موثّقة تلقائياً.
- ✅ **اختبارات (25 جديدة، 61/61 إجمالاً):** CRUD لكل كيان · عزل المستأجرين (كيان مستأجر آخر → 404) · authorization (403 بلا صلاحية) · validation (اسم فارغ/precision>6/SKU مكرّر/باركود مكرّر/باركودان رئيسيان/وحدة أساس غير موجودة) · دورة التصنيفات · حراسة الحذف.

> **لم يُنشأ بعد:** Inventory (Warehouses/Stock/Movements) · Purchasing · Sales · Frontend.

---

## ما أُنجز في Phase 6 (Administration Foundation)

طبقة إدارة كاملة عبر REST، بنفس القوالب (CQRS · Clean Architecture · Tenant Isolation · Response Envelope · Validation · Authorization Policies):

- ✅ **Users Management** — `GET /api/v1/users` (بحث) · `GET /users/{id}` · `POST /users` (إنشاء + إسناد أدوار) · `PUT /users/{id}` (تعديل + مطابقة الأدوار) · `POST /users/{id}/activate` · `POST /users/{id}/deactivate` (يُبطِل refresh tokens النشطة).
- ✅ **Roles Management (RBAC)** — CRUD كامل (`GET`/`POST`/`PUT`/`DELETE /api/v1/roles`) + `PUT /roles/{id}/permissions` (إسناد الصلاحيات) · حماية الأدوار النظامية (Owner) من التعديل/الحذف · منع حذف دور مُسنَد لمستخدمين.
- ✅ **Permissions Management** — `GET /api/v1/permissions` (قراءة فقط — الكتالوج ثابت مُعرَّف بالنظام؛ الإسناد يتمّ عبر الأدوار).
- ✅ **Tenant Settings** — `GET /api/v1/tenant/settings` (يُرجع الافتراضيات إن لم تُضبَط) · `PUT` (upsert: عملة/منطقة زمنية/ضريبة/locale/theme JSON مُتحقَّق منه).
- ✅ **Current User Profile** — `GET /api/v1/profile` (الهوية + الأدوار + الصلاحيات الفعلية) · `PUT /profile` (تعديل ذاتي) · `POST /profile/change-password` (تحقّق كلمة المرور الحالية + إبطال الجلسات). **مصادقة فقط، بلا صلاحية محدّدة.**
- ✅ **Seeding** — `PermissionSeeder` (يزرع الكتالوج عند الإقلاع، idempotent) + `TenantRoleSeeder` (دور Owner بكل الصلاحيات لكل مستأجر). أُضيفت صلاحيات جديدة: `roles.create/update/delete/permissions.manage` · `settings.view/manage`.
- ✅ **Authorization** — كل endpoint محميّ بـ `[HasPermission("resource.action")]`؛ عزل المستأجر يضمن أن كيانات مستأجر آخر غير مرئية (NOT_FOUND).
- ✅ **اختبارات (23 جديدة، 36/36 إجمالاً):** happy-path لكل مجموعة · عزل المستأجرين (users من مستأجر آخر → 404) · authorization (بلا صلاحية → 403 · بلا مصادقة → 401) · validation (بريد/كلمة مرور/عملة/صلاحية غير صالحة → 400) · تدوير كلمة المرور تعمل بالجديدة · حماية الدور النظامي.

> **لم يُنشأ بعد:** Products/Sales/Inventory · Business modules · Frontend.

---

## ما أُنجز في Phase 5 (API Foundation + Authentication)

- ✅ **Response Envelope موحّد** — `ApiResponse<T>` (success/data/error/meta) + `Result<T>` + `Error` + `ErrorStatusMapper`.
- ✅ **Authentication endpoints:**
  - `POST /api/v1/auth/login` — email+password · تحقّق المستأجر (Active) · تحقّق المستخدم · JWT بصلاحيات + refresh token (hash مخزَّن).
  - `POST /api/v1/auth/refresh` — تدوير التوكن (rotation) + إبطال القديم + **reuse detection**.
  - `POST /api/v1/auth/logout` — إبطال refresh token.
- ✅ **CQRS handlers** (MediatR) + FluentValidation عبر `ValidationBehavior`.
- ✅ **Permission-based Authorization** — `[HasPermission("resource.action")]` + `PermissionPolicyProvider` (ديناميكي) + `PermissionAuthorizationHandler`.
- ✅ **JWT Authentication** — Bearer + التحقّق (issuer/audience/lifetime/key) · binding كسول من `JwtSettings`.
- ✅ **Global Exception Handling** — يحوّل الأخطاء إلى الـ envelope الموحّد (ProblemDetails-style).
- ✅ **Swagger** — زرّ Authorize (JWT Bearer) + الـ endpoints ظاهرة.
- ✅ **اختبارات (6 جديدة، 13/13 إجمالاً):** login ناجح · كلمة مرور خاطئة (401) · مستأجر معطّل (403 TENANT_INACTIVE) · تدوير refresh + إبطال القديم · logout يُبطِل · validation بالـ envelope.

> **لم يُنشأ بعد:** Products/Sales/Inventory · Business modules · Register endpoint.

---

## ما أُنجز في Phase 4 (Identity & RBAC Foundation)

- ✅ **Identity Entities** — `AppUser` (TenantId nullable للـ system owner) · `AppRole` · `Permission` (مرجعي عالمي، int key) · `UserRole` · `RolePermission` (junction) · `RefreshToken` (hash + rotation + reuse detection).
- ✅ **EF Configurations** — علاقات · فهارس · قيود فريدة tenant-scoped (`UX_Users_Tenant_Email` · `UX_Roles_Tenant_Name` · `UX_Permissions_Code`) · فلاتر عزل صريحة للكيانات nullable-tenant.
- ✅ **Shared Constants** — `Permissions` (resource.action) + `RoleNames`.
- ✅ **Authentication Foundation** — `IPasswordHasher` + `PasswordHasher` (PBKDF2) · `IJwtService` + `JwtService` (JWT بصلاحيات + refresh token hashing) · `JwtSettings` binding.
- ✅ **Migration `AddIdentity`** — 6 جداول Identity بكل الفهارس والقيود. **بلا model drift**.
- ✅ **اختبارات العزل (3 جديدة، 7/7 إجمالاً)**: عزل المستخدمين · عزل الأدوار والصلاحيات (مع تأكيد أن `Permissions` مرجعي مشترك) · ملكية RefreshToken لمستأجره فقط.

> **لم يُنشأ بعد:** Login/Register endpoints · Controllers · Authorization policies كاملة.

---

## ما أُنجز في Phase 3 (Tenant Core)

- ✅ **Tenant Entity** — `Tenant` (يرث `AuditableEntity` + `ISoftDeletable`، **بلا `TenantId`** لأنه تعريف المستأجر نفسه) + `TenantSetting` (tenant-owned، يرث `BaseEntity`).
- ✅ **EF Configurations** — `TenantConfiguration` + `TenantSettingConfiguration` (Fluent · Indexes · Unique constraints · ROWVERSION · Check constraints).
- ✅ **أول Migration حقيقية** — `InitialCreate` (جدولا `Tenants` + `TenantSettings` مع كل الفهارس والقيود). **بلا model drift**.
- ✅ **TenantResolutionMiddleware** — يقرأ المستأجر من سياق الطلب + بوّابة التفعيل اليدوي (Active/Suspended/Disabled → 403).
- ✅ **اختبارات العزل (4/4 تمرّ)** على SQLite حقيقي:
  - `TenantA_Cannot_Read_TenantB_Data` — مستأجر لا يرى بيانات آخر.
  - `Global_Query_Filter_Scopes_Reads_To_Current_Tenant` — الفلتر العالمي يعمل.
  - `TenantId_Is_Stamped_Server_Side_On_Insert` — الختم التلقائي.
  - `Delete_Is_Soft_And_Excluded_By_Filter` — Soft Delete يعمل.

> **لم يُنشأ بعد:** باقي Entities (Products/Sales/Inventory...) · Controllers · Business logic · Authentication.

---

## ما أُنجز في Phase 2 (Domain + Persistence Foundation)

- ✅ **Domain Core:** `Entity` · `AuditableEntity` · `BaseEntity` (Tenant + Audit + Soft Delete + ConcurrencyStamp) + الواجهات (`ITenantOwned`, `IAuditable`, `ISoftDeletable`, `IAppendOnly`).
- ✅ **Common enums:** `TenantStatus` (Active/Suspended/Disabled — بديل SaaS).
- ✅ **Domain Exceptions:** `DomainException` (base) · `BusinessRuleViolationException` · `TenantMismatchException`.
- ✅ **Application Interfaces:** `ITenantProvider` · `ICurrentUserService` · `IDateTimeProvider` · `IApplicationDbContext`.
- ✅ **Persistence:** `AppDbContext` مع **EF Core Global Query Filter** (عزل المستأجر + Soft Delete تلقائياً لكل `BaseEntity`) + **ختم `TenantId`** خادم-جانبياً + `AuditableEntityInterceptor` (audit + تحويل الحذف لـ soft delete).
- ✅ **Infrastructure:** `TenantProvider` · `CurrentUserService` · `DateTimeProvider` (يقرؤون من `HttpContext`/Claims).
- ✅ **Migration-ready:** `AppDbContextFactory` (design-time) + connection config — تمّ **التحقّق فعلياً** بتوليد migration ناجحة ثم إزالتها (حسب قيد "لا تنشئ migrations الآن").
- ✅ **Build نظيف: 0/0** · التطبيق يُقلِع و DI يحلّ كل الخدمات.

> **لم يُنشأ بعد:** Entities فعلية · DbSets · Controllers · Business logic · Authentication.

---

## ما أُنجز في Phase 1 (Foundation)

هذه المرحلة تُنشئ **الهيكل والبنية التحتية فقط**، حسب [Implementation Roadmap](../SmartApp-Architecture/14-Implementation-Roadmap.md):

- ✅ إنشاء `SmartApp.sln` والمشاريع الستّة + مشاريع الاختبار.
- ✅ إعداد Project References حسب Clean Architecture (الاعتماد يتّجه نحو الـ Domain).
- ✅ الهيكل الداخلي للمجلّدات لكل مشروع.
- ✅ ملفات إعداد موحّدة (`Directory.Build.props`, `.editorconfig`, `global.json`).
- ✅ بنية Dependency Injection لكل طبقة (`AddApplication` / `AddInfrastructure` / `AddPersistence` / `AddApiServices`).
- ✅ `Program.cs` + Middleware pipeline + تحميل الإعدادات.
- ✅ بنية معالجة الأخطاء الموحّدة (Global Exception Handling → Envelope موحّد).
- ✅ بنية API Versioning (`/api/v1/...`) + إعداد Swagger (مع JWT Bearer).
- ✅ **Build نظيف: 0 تحذير · 0 خطأ** · الخادم يُقلِع و Swagger يعمل (200).

> **لم يُنشأ بعد (مؤجَّل لمراحل لاحقة):** Entities · DbContext · Migrations · Controllers · Business Logic · Authentication implementation.

---

## بنية المشاريع (Clean Architecture — 6 مشاريع)

```
SmartApp.sln
├── src/
│   ├── SmartApp.Domain          ← قلب النظام (لا يعتمد إلا على Shared)
│   ├── SmartApp.Application      ← حالات الاستخدام (CQRS/MediatR + FluentValidation + الواجهات)
│   ├── SmartApp.Infrastructure   ← الخدمات التقنية (JWT, PasswordHasher, TenantProvider)
│   ├── SmartApp.Persistence      ← الوصول للبيانات (EF Core + SQL Server + Seeding)
│   ├── SmartApp.Shared           ← عناصر محايدة مشتركة (Result, Constants, ...)
│   └── SmartApp.API              ← نقطة الدخول (Composition Root + Middleware + Swagger)
│
└── tests/
    ├── SmartApp.Domain.Tests
    ├── SmartApp.Application.Tests
    └── SmartApp.IntegrationTests
```

### اتجاه الاعتماد (Dependency Direction)

```
API → Application → Domain → Shared
Infrastructure → Application + Domain + Shared
Persistence   → Application + Domain + Shared
API (Composition Root) → كل المشاريع
```

- **`Domain`** لا يعتمد على أي مشروع سوى `Shared` (ولا حزم خارجية).
- **`Application`** يعرف الواجهات فقط (Interfaces)، لا تنفيذ البنية التحتية.
- **`API`** وحده يربط التنفيذ بالواجهات عبر DI (Dependency Inversion).

---

## التقنيات (Technology Stack)

| المجال | التقنية |
|--------|---------|
| Runtime | .NET 9 (C# 13, Nullable enabled, Implicit usings) |
| API | ASP.NET Core 9 Web API |
| CQRS | MediatR 12.4.1 |
| Validation | FluentValidation 11.11 |
| ORM | EF Core 9 + SQL Server (SQLite in-memory للاختبارات) |
| Auth | JWT Bearer + Permission-based Authorization (`resource.action`) |
| Passwords | PBKDF2 (ASP.NET Core `PasswordHasher`) |
| Versioning | Asp.Versioning (URL segment: `/api/v1/`) |
| Docs | Swagger / Swashbuckle 7.2 (JWT Bearer) |

> الجودة: `TreatWarningsAsErrors = true` + `AnalysisLevel = latest-recommended` — لا تحذيرات مُقدَّمة، ولا حزم بها ثغرات معروفة.

---

## التشغيل (Getting Started)

**المتطلّبات:** .NET 9 SDK · SQL Server (يُستخدم لاحقاً في Phase 2) · Visual Studio 2022 أو VS Code.

```bash
# بناء الحلّ كاملاً
dotnet build SmartApp.sln

# تشغيل الـ API
dotnet run --project src/SmartApp.API

# ثم افتح Swagger:
#   http://localhost:<port>/swagger
```

> فتح المشروع في Visual Studio: افتح `SmartApp.sln` مباشرةً.

---

## ملاحظات مهمّة (Notes)

- **إدارة العملاء يدوية بالكامل** — لا نظام اشتراك/فوترة/دفع. حالة المستأجر (`Active`/`Suspended`/`Disabled`) يتحكّم بها مالك النظام (يُنفَّذ في Phase 2).
- **العزل قرار معماري أساسي** — `TenantId` هو حدّ العزل، عبر EF Core Global Query Filter (يُنفَّذ في Phase 2). لا `StoreId` مبدئياً.
- **الأسرار لا تُرفَع** — `ConnectionStrings:SmartAppDb` و`Jwt:SigningKey` فارغة في `appsettings.json`؛ تُملأ عبر User Secrets / متغيّرات البيئة.
- **AutoMapper مؤجَّل** — بسبب توافق النسخ/الترخيص، تُحسَم مكتبة الـ mapping في Phase 3 عند الحاجة الفعلية للتحويل.

---

## تشغيل الحلّ الكامل (Backend + Frontend)

```bash
# 1) قاعدة البيانات — أنشئ الجداول (SQL Server / LocalDB)
cd SmartApp
dotnet ef database update --project src/SmartApp.Persistence --startup-project src/SmartApp.API

# 2) Backend API
dotnet run --project src/SmartApp.API      # Swagger على /swagger (تطوير)

# 3) Frontend (في نافذة أخرى)
cd SmartApp/frontend
npm install
npm run dev                                 # http://localhost:5173 (يمرّر /api للـ backend)
```

### تسجيل الدخول الافتراضي (بيئة التطوير)

عند أول إقلاع في بيئة **Development**، يُنشئ التطبيق تلقائياً — إن كانت قاعدة البيانات فارغة — **مستأجراً افتراضياً (DEMO) + دور Owner (كل الصلاحيات) + مستخدم Owner**، فتسجّل الدخول مباشرةً دون أي إدخال يدوي:

| الحقل | القيمة |
|------|-------|
| **Email** | `admin@smartapp.local` |
| **Password** | `Admin@123456` |

- يعمل من **Swagger** (زرّ Authorize بعد `POST /api/v1/auth/login`) ومن **الواجهة** على `http://localhost:5173`.
- **آمن:** يعمل فقط عندما `Seed:DevData=true` (مفعّل في `appsettings.Development.json` فقط، ومطفأ في الإنتاج)، و**idempotent** (لا يفعل شيئاً إن وُجد أي مستخدم — لا يكرّر ولا يستبدل بيانات).
- لتغيير البيانات الافتراضية: عدّل `Seed:OwnerEmail` / `Seed:OwnerPassword` / `Seed:TenantName` / `Seed:TenantCode` في `appsettings.Development.json`.

> **باختصار:** `dotnet ef database update` → `dotnet run` → افتح Swagger و`http://localhost:5173` → سجّل الدخول بالحساب الافتراضي.

الأسرار (`Jwt:SigningKey` + `ConnectionStrings:SmartAppDb`) من متغيّرات البيئة / user-secrets. تفاصيل النشر في [DEPLOYMENT.md](DEPLOYMENT.md) والأمن في [SECURITY.md](SECURITY.md). تفاصيل الواجهة في [frontend/README.md](frontend/README.md).

---

## التوثيق المعماري (Architecture Docs)

كل القرارات والتفاصيل في مجلد [SmartApp-Architecture](../SmartApp-Architecture/) — **المرجع الحاكم** لكل الكود. لا يُكتب كود يخالف التوثيق المعتمد.

---

_SmartApp · Phase 1–13 (Backend + Frontend مكتمل) · .NET 9 · Clean Architecture · React + Vite + TypeScript._
