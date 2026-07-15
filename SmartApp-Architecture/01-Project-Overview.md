# 01 — Project Overview (نظرة عامّة على المشروع)

> يحدّد هذا الملف **ماهيّة SmartApp** ونطاقه ومبادئه الحاكمة، وما الذي **أُزيل صراحةً** من نموذج SaaS. يُقرأ قبل كل الملفات التقنية.

---

## 1) الرؤية (Vision)

`SmartApp` منصّة **إدارة أعمال (Business Management Platform)** احترافية مبنية على **ASP.NET Core**، تُصمَّم لتكون:

- **احترافية** — بمعايير هندسية على مستوى المؤسسات (Enterprise-grade).
- **قابلة للصيانة** — Clean Architecture + SOLID + حدود وحدات واضحة.
- **قابلة للبيع تجارياً** — منتج جاهز يُرخَّص لعدّة عملاء.
- **سهلة التخصيص** — كل عميل معزول ويمكن تكييف سلوكه عبر إعدادات (Data-driven).
- **قابلة للتوسّع** — تبدأ نواة عامّة، وتُضاف قدرات POS المتقدّمة لاحقاً **دون إعادة تصميم**.

---

## 2) ما هو SmartApp — وما ليس هو

### هو:

- تطبيق **Multi-Tenant** يخدم عدّة شركات/محلّات مستقلّة على نشر واحد.
- كل عميل (`Tenant`) بياناته **معزولة تماماً** عن الآخرين.
- مالك النظام **يتحكّم يدوياً** بتفعيل/تعطيل كل عميل.

### ليس هو (صراحةً — قرار حاكم):

| ❌ ليس | التوضيح |
|--------|----------|
| **ليس SaaS باشتراكات** | لا دورة اشتراك (Trial/Active/Grace/Renew) |
| **ليس نظام فوترة منصّة** | لا فواتير على العملاء مقابل الاستخدام |
| **ليس بوّابة دفع** | لا مدفوعات إلكترونية ولا تكامل بنكي |
| **ليس نظام خطط (Plans)** | لا Feature-gating قائم على خطّة مدفوعة |
| **ليس ERP بفوترة داخلية للمنصّة** | إدارة العملاء يدوية بالكامل |

> **بديل SaaS المعتمد:** تحكّم يدوي بسيط — حقل `Status` على `Tenant` بقيم `Active` / `Suspended` / `Disabled`، يغيّره مالك النظام. لا جداول اشتراك/فوترة/دفع في قاعدة البيانات على الإطلاق.

---

## 3) النطاق الوظيفي (Functional Scope)

### 3.1 الوحدات الأساسية (Initial Core Modules)

تُبنى الآن — تشكّل **نواة SmartApp**:

| الوحدة | المسؤولية |
|--------|-----------|
| **Tenancy Management** | إدارة المستأجرين (العملاء) وحالتهم اليدوية (`Active`/`Suspended`/`Disabled`) |
| **Identity & Access Control** | المصادقة (Login/Refresh/Logout) وإدارة كلمات المرور |
| **Users, Roles, Permissions** | إدارة المستخدمين والأدوار والصلاحيات (RBAC) |
| **Customer Management** | إدارة عملاء المستأجر (الزبائن) وأرصدتهم |
| **Supplier Management** | إدارة الموردين وأرصدتهم |
| **Product Catalog** | المنتجات، التصنيفات، الوحدات، الباركود |
| **Inventory Core** | الأرصدة (`Stock`) وسجل الحركات (`StockMovements` — append-only) بتكلفة WAC |
| **Sales Management** | فواتير البيع وبنودها ومرتجعاتها (بسعر أصلي مجمّد) |
| **Purchase Management** | فواتير الشراء وبنودها ومرتجعاتها وأثرها على التكلفة |
| **Reporting** | تقارير البيع/الشراء/المخزون/العملاء/الموردين |
| **Audit Logging** | سجل تدقيق (`AuditLogs` — append-only): من غيّر ماذا ومتى |
| **System Settings** | إعدادات المستأجر (اسم، عملة، ضريبة، منطقة زمنية، ثيم) |

### 3.2 الوحدات المستقبلية (Future Optional Extensions)

تُضاف لاحقاً كـ **Extensions** دون المساس بالنواة (انظر [04-Domain-Boundaries.md](04-Domain-Boundaries.md)):

`POS` · `Cashier Shifts` · `Barcode POS Terminal` · `Promotions & Offers` · `Loyalty Program` · `Advanced Multi-store Operations`.

> **مبدأ حاكم:** POS **امتداد لا أساس**. النواة العامّة لا تعتمد على POS، والعكس صحيح: POS يبني فوق النواة.

---

## 4) المتطلّبات غير الوظيفية (Non-Functional Requirements)

| المتطلّب | المعيار |
|----------|---------|
| **الأمان (Security)** | العزل حدّ أمني صارم · OWASP Top 10 · JWT + Refresh rotation · RBAC |
| **سلامة البيانات (Data Integrity)** | Transactions ذرّية · Soft Delete · Append-only للسجلّات المالية |
| **الأداء (Performance)** | فهرسة العزل · Async I/O · تجنّب N+1 · RCSI |
| **قابلية التدقيق (Auditability)** | `AuditLogs` لكل تغيير على بيانات حسّاسة |
| **قابلية التوسّع (Scalability)** | تصميم يسمح بقاعدة/شارد منفصل لعميل كبير مستقبلاً |
| **قابلية الصيانة (Maintainability)** | Clean Architecture · حدود وحدات · اتساق الأنماط |
| **قابلية التوسعة (Extensibility)** | POS وبقية الامتدادات دون إعادة تصميم النواة |

**أولوية حلّ التعارضات:** `Security > Data Integrity > Correctness > Maintainability > Performance > Convenience`.

---

## 5) القطاعات المستهدفة (Target Segments)

محلّات وشركات صغيرة ومتوسّطة تحتاج إدارة **كتالوج + مخزون + مبيعات + مشتريات + تقارير** بعزل تامّ لكل عميل، بنموذج **ترخيص يدوي** بدل الاشتراك الشهري. القطاعات التي تحتاج POS كامل تُخدَم لاحقاً عبر الامتداد.

---

## 6) مصفوفة القدرات (Capability Matrix)

| القدرة | النواة (الآن) | امتداد (لاحقاً) |
|--------|:-------------:|:---------------:|
| Multi-Tenant Isolation | ✅ | — |
| Manual Tenant Activation | ✅ | — |
| Identity + JWT + RBAC | ✅ | — |
| Product Catalog | ✅ | — |
| Inventory (Stock + WAC + Movements) | ✅ | — |
| Sales + Sales Returns | ✅ | — |
| Purchases + Purchase Returns | ✅ | — |
| Reporting | ✅ | — |
| Audit Logging | ✅ | — |
| System Settings | ✅ | — |
| POS Terminal & Shifts | — | 🔜 |
| Promotions / Offers / Loyalty | — | 🔜 |
| Multi-store (`StoreId`) | — | 🔜 |
| Row-Level Security (RLS) | تصميم جاهز | 🔜 (تقوية) |

---

## 7) مبادئ التصميم الحاكمة (Governing Principles)

1. **Tenant Isolation First** — العزل قرار معماري أساسي لا ميزة لاحقة.
2. **Manual Control, No Billing** — التحكّم يدوي؛ لا أثر لأي منطق دفع في الكود أو الـ schema.
3. **Core is Generic** — النواة لا تعرف شيئاً عن POS.
4. **Append-Only Truth** — السجلّات المالية/المخزنية لا تُعدَّل، بل تُصحَّح بحركة معاكسة.
5. **Server-Trusted Context** — `TenantId` لا يأتي من العميل أبداً.
6. **Documentation Before Code** — لا كود قبل اعتماد هذا التوثيق.

---

_يُكمّله [02-Solution-Architecture.md](02-Solution-Architecture.md) و[04-Domain-Boundaries.md](04-Domain-Boundaries.md)._
