# SmartApp — Architecture Documentation

> **منصّة إدارة أعمال احترافية مبنية على ASP.NET Core** — متعدّدة المستأجرين (Multi-Tenant)، بعزل تامّ للبيانات، وإدارة تفعيل يدوية للعملاء **بلا اشتراكات ولا فوترة ولا مدفوعات**.

**نوع التوثيق:** Full Deep Design — جاهز للمراجعة والاعتماد (Approval-Ready).
**الحالة:** توثيق معماري فقط — **لا كود تنفيذي حتى الموافقة**.
**اللغة:** عربي للشرح · إنجليزي للمصطلحات التقنية والأسماء (Classes / Projects / Tables / APIs / Patterns).

---

## 1) ما هو SmartApp؟

`SmartApp` منصّة تجارية لإدارة الأعمال (Business Management Platform)، تخدم **عدّة عملاء مستقلّين** (شركات/محلّات) على نشر واحد، مع **عزل تامّ للبيانات** لكل عميل عبر `TenantId`.

**مالك النظام يتحكّم يدوياً بتفعيل/تعطيل العملاء** — لا نظام دفع تلقائي، لا اشتراكات، لا فواتير منصّة.

هذا التوثيق **يحلّ محلّ** توثيق `Smart-POS-Documentation` القديم (نموذج SaaS) — انظر [قرار الاستبدال](#5-علاقة-هذا-التوثيق-بالتوثيق-القديم).

---

## 2) القرارات المعمارية المعتمدة (Approved Decisions)

| القرار | الاختيار |
|--------|----------|
| **نموذج المنتج** | تطبيق تجاري يُباع/يُرخَّص يدوياً — **ليس SaaS** |
| **إدارة العملاء** | تفعيل/تعطيل يدوي: `Active` / `Suspended` / `Disabled` |
| **إزالة SaaS** | لا Subscriptions · لا Billing · لا Payments · لا Plans |
| **البنية** | Clean Architecture — 6 مشاريع |
| **المشاريع** | `SmartApp.Domain` · `SmartApp.Application` · `SmartApp.Infrastructure` · `SmartApp.Persistence` · `SmartApp.API` · `SmartApp.Shared` |
| **النطاق** | نواة عامّة (Generic Core) الآن · POS كـ **Extension** لاحقاً |
| **العزل** | `Tenant` هو الحدّ الأساسي · **لا `StoreId`** في الجداول مبدئياً |
| **آلية العزل** | EF Core Global Query Filter + Tenant Resolution Middleware · RLS كطبقة تقوية **اختيارية مستقبلية** |
| **قاعدة البيانات** | SQL Server 2022+ · EF Core 9 · Soft Delete · UTC · `DECIMAL(18,4)` للأموال |
| **المصادقة** | ASP.NET Core Identity + JWT + Refresh Token Rotation |
| **الصلاحيات** | RBAC قائم على Permissions (`resource.action`) |
| **الأنماط** | CQRS (MediatR) · Repository + Unit of Work · FluentValidation · AutoMapper |

---

## 3) خريطة التوثيق (Documentation Map)

اقرأ الملفات بالترتيب — كلٌّ يبني على سابقه:

| # | الملف | يحكم / يشرح |
|---|-------|-------------|
| 00 | [README.md](README.md) | نقطة الدخول · الفهرس · القرارات المعتمدة *(هذا الملف)* |
| 01 | [01-Project-Overview.md](01-Project-Overview.md) | الرؤية · النطاق · الوحدات · ما أُزيل من SaaS · مصفوفة القدرات |
| 02 | [02-Solution-Architecture.md](02-Solution-Architecture.md) | Clean Architecture · المشاريع الستّة · اتجاه الاعتماد · تدفّق الطلب · CQRS |
| 03 | [03-Project-Structure.md](03-Project-Structure.md) | بنية المجلدات المفصّلة لكل مشروع من الستّة |
| 04 | [04-Domain-Boundaries.md](04-Domain-Boundaries.md) | حدود الوحدات (Modules) · النواة مقابل POS Extension · قابلية التوسّع |
| 05 | [05-Database-Design.md](05-Database-Design.md) | **المرجع الحاكم** — `BaseEntity` · الأعمدة المشتركة · القوالب · القواعد |
| 06 | [06-Tables-Definitions.md](06-Tables-Definitions.md) | `CREATE TABLE` فعلي لكل جداول النواة + PK/FK/Constraints |
| 07 | [07-ERD-Relationships.md](07-ERD-Relationships.md) | مخطّط ERD · كل العلاقات · قواعد الحذف (No Cascade) |
| 08 | [08-Indexing-Strategy.md](08-Indexing-Strategy.md) | معايير الفهرسة · فهرس العزل · الفهارس المُرشَّحة · `CREATE INDEX` فعلي |
| 09 | [09-Multi-Tenant.md](09-Multi-Tenant.md) | **محوري** — العزل · `ITenantProvider` · Middleware · التفعيل اليدوي |
| 10 | [10-Identity-RBAC.md](10-Identity-RBAC.md) | Users/Roles/Permissions · JWT · Refresh Rotation · مصفوفة الصلاحيات |
| 11 | [11-Security-Architecture.md](11-Security-Architecture.md) | OWASP Top 10 · حماية العزل · الأسرار · RLS المستقبلي |
| 12 | [12-API-Architecture.md](12-API-Architecture.md) | REST · Response Envelope · Versioning · ProblemDetails · عقود |
| 13 | [13-Development-Rules.md](13-Development-Rules.md) | SOLID · CQRS · Repo/UoW · Naming · Async · Definition of Done |
| 14 | [14-Implementation-Roadmap.md](14-Implementation-Roadmap.md) | مراحل التطوير وترتيب التنفيذ المُوصى به بعد الاعتماد |

---

## 4) المعايير الحاكمة الموحّدة (Global Standards)

كل جدول أعمال في النظام يلتزم بالمرجع الحاكم [05-Database-Design.md](05-Database-Design.md):

- **الأعمدة المشتركة الإجبارية:** `Id (BIGINT IDENTITY)` · `TenantId` · `CreatedDate` · `CreatedBy` · `ModifiedDate` · `ModifiedBy` · `DeletedDate` · `DeletedBy` · `IsDeleted` · `ConcurrencyStamp (ROWVERSION)`.
- **الأموال:** `DECIMAL(18,4)` — لا `FLOAT` أبداً.
- **التواريخ:** `DATETIME2(3)` بتوقيت **UTC** حصراً.
- **الحذف:** Soft Delete إجباري لكل جداول الأعمال — لا حذف فعلي للبيانات المالية/المخزنية.
- **العزل:** `TenantId` في كل جدول أعمال + EF Core Global Query Filter تلقائي.
- **جداول Append-Only:** `StockMovements` و`AuditLogs` لا تُعدَّل ولا تُحذَف — إدراج فقط.
- **الحذف المتتالي:** ممنوع (`ON DELETE NO ACTION`) — لأن كل الحذف soft.

---

## 5) علاقة هذا التوثيق بالتوثيق القديم

| البُعد | `Smart-POS-Documentation` (قديم) | `SmartApp-Architecture` (هذا) |
|--------|----------------------------------|-------------------------------|
| النموذج | SaaS بفوترة واشتراكات | تطبيق تجاري بتفعيل يدوي |
| البنية | 4 مشاريع (Domain/Application/Infrastructure/Web) | 6 مشاريع (+ Persistence + Shared) |
| الواجهة | MVC + Razor (server-rendered) | Web API (الواجهة تُقرَّر لاحقاً) |
| الفروع | `StoreId` في كل جدول | لا `StoreId` — جاهز مستقبلاً |
| POS | جزء من النواة | Extension مستقبلي |

> **القرار:** هذا التوثيق **Replacement**. يُورَث منطق الأعمال العميق من القديم (WAC، المرتجعات بسعر مجمّد، `StockMovements` append-only) ويُكيَّف بعد إزالة كل SaaS. التوثيق القديم يبقى كمرجع تاريخي فقط.

---

## 6) الوحدات (Modules)

### النواة الأساسية (Initial Core — تُبنى الآن)

`Tenancy Management` · `Identity & Access Control` · `Users/Roles/Permissions` · `Customer Management` · `Supplier Management` · `Product Catalog` · `Inventory Core` · `Sales Management` · `Purchase Management` · `Reporting` · `Audit Logging` · `System Settings`.

### الوحدات المستقبلية (Optional Extensions — لاحقاً)

`POS` · `Cashier Shifts` · `Barcode POS Terminal` · `Promotions & Offers` · `Loyalty Program` · `Advanced Multi-store Operations`.

> المعمارية مصمَّمة بحيث يكون **POS امتداداً (Extension) لا أساساً (Foundation)** — تُضاف الوحدات المستقبلية دون إعادة تصميم النواة. التفصيل في [04-Domain-Boundaries.md](04-Domain-Boundaries.md).

---

## 7) الخطوات بعد الاعتماد (Post-Approval)

1. مراجعة واعتماد هذا التوثيق بالكامل.
2. تهيئة الـ Solution وإنشاء المشاريع الستّة (هيكل فارغ).
3. تنفيذ النواة (Tenancy → Identity → Catalog → Inventory → Sales/Purchases).
4. اختبارات العزل الآلية (Isolation Tests) في CI.
5. توثيق الـ Frontend (يُقرَّر لاحقاً).

---

_توثيق معماري — SmartApp. لا يُولَّد كود تنفيذي قبل اعتماد هذا التوثيق._
