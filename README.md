# SmartApp — منصّة إدارة أعمال متعدّدة المستأجرين (ASP.NET Core)

> منصّة تجارية لإدارة الأعمال مبنية على **ASP.NET Core / .NET 9** و **Clean Architecture**، متعدّدة المستأجرين (Multi-Tenant) بعزل تامّ للبيانات، وإدارة تفعيل **يدوية** للعملاء — **بلا اشتراكات ولا فوترة ولا مدفوعات**.

---

## 📌 نظرة عامّة

`SmartApp` نظام يخدم **عدّة عملاء مستقلّين** (شركات/محلّات) على نشر واحد، مع عزل تامّ لبيانات كل عميل عبر `TenantId`. **مالك النظام يتحكّم يدوياً** بتفعيل/تعطيل كل عميل (`Active` / `Suspended` / `Disabled`) — لا نظام دفع أو اشتراك تلقائي.

النظام يبدأ **نواة عامّة (Generic Core)** لإدارة الأعمال، ومصمَّم ليقبل قدرات **POS المتقدّمة لاحقاً كامتداد (Extension)** دون إعادة تصميم.

---

## 🗂️ بنية المستودع (Repository Structure)

| المجلد | المحتوى |
|--------|---------|
| **[`SmartApp-Architecture/`](SmartApp-Architecture/)** | 📐 **التوثيق المعماري المعتمد** — 15 ملفاً (المرجع الحاكم لكل الكود) |
| **[`SmartApp/`](SmartApp/)** | 💻 **الكود الفعلي** — Solution مبنية على .NET 9 (Phase 1 مكتملة) |
| `Smart-POS-Documentation/` | 📚 توثيق قديم (نموذج SaaS) — **مرجع تاريخي فقط** (استُبدِل) |
| `AI-Prompt/` | 🤖 قواعد توليد الكود القديمة — مرجع |
| `PROJECT_SUMMARY.md` | ملخّص التوثيق القديم |

> **ملاحظة:** التوثيق الحاكم الآن هو `SmartApp-Architecture/`؛ التوثيق القديم `Smart-POS-Documentation/` بقي كمرجع فقط بعد قرار الاستبدال (إزالة كل ما يخصّ SaaS/الاشتراكات/الفوترة).

---

## 📐 التوثيق المعماري (Architecture)

ابدأ من [SmartApp-Architecture/README.md](SmartApp-Architecture/README.md). أبرز الملفات:

- [02 — Solution Architecture](SmartApp-Architecture/02-Solution-Architecture.md) — Clean Architecture و 6 مشاريع.
- [05 — Database Design](SmartApp-Architecture/05-Database-Design.md) — المرجع الحاكم لقاعدة البيانات.
- [06 — Tables & Definitions](SmartApp-Architecture/06-Tables-Definitions.md) — 30 جدول نواة بـ SQL كامل.
- [09 — Multi-Tenant](SmartApp-Architecture/09-Multi-Tenant.md) — العزل والتفعيل اليدوي.
- [14 — Implementation Roadmap](SmartApp-Architecture/14-Implementation-Roadmap.md) — مراحل التنفيذ.

---

## 💻 الكود (Phase 1 — Foundation ✅)

المرحلة الأولى مكتملة: **هيكل نظيف يعمل Build ويُقلِع، بلا منطق أعمال بعد**.

```
SmartApp/
├── SmartApp.sln
├── src/
│   ├── SmartApp.Domain          ← قلب النظام
│   ├── SmartApp.Application      ← CQRS / MediatR + الواجهات
│   ├── SmartApp.Infrastructure   ← الخدمات التقنية (لاحقاً)
│   ├── SmartApp.Persistence      ← EF Core + SQL Server (لاحقاً)
│   ├── SmartApp.Shared           ← عناصر محايدة مشتركة
│   └── SmartApp.API              ← نقطة الدخول + Swagger
└── tests/
    ├── SmartApp.Domain.Tests
    ├── SmartApp.Application.Tests
    └── SmartApp.IntegrationTests
```

**تفاصيل الكود والتشغيل:** [SmartApp/README.md](SmartApp/README.md).

### تشغيل سريع

```bash
cd SmartApp
dotnet build SmartApp.sln
dotnet run --project src/SmartApp.API
# ثم افتح: http://localhost:<port>/swagger
```

**حالة البناء:** ✅ 0 تحذير · 0 خطأ · الخادم يُقلِع و Swagger يعمل.

---

## 🚦 حالة المشروع (Status)

| المرحلة | الوصف | الحالة |
|:-------:|-------|:------:|
| — | التوثيق المعماري | ✅ معتمد |
| **1** | Foundation (الهيكل والبنية التحتية) | ✅ مكتمل |
| 2 | Tenancy + العزل (Multi-Tenant Core) | ⏳ التالي |
| 3 | Identity & RBAC | 🔜 |
| 4 | Catalog + Inventory | 🔜 |
| 5 | Sales + Purchases | 🔜 |
| 6 | Reporting + Audit + Settings | 🔜 |
| 7 | Hardening & Launch | 🔜 |
| 8+ | امتدادات (POS · Offers · Loyalty · Multi-store) | 🔜 |

---

## 🧱 القرارات المعمارية المعتمدة

- **Clean Architecture** بـ 6 مشاريع (الاعتماد يتّجه نحو Domain).
- **Multi-Tenant** — `TenantId` حدّ العزل · EF Core Global Query Filter · لا `StoreId` مبدئياً.
- **بلا SaaS** — لا Subscriptions/Billing/Payments/Plans · تحكّم يدوي عبر `Tenant.Status`.
- **CQRS** (MediatR) · **Repository + Unit of Work** · **FluentValidation**.
- **Identity + JWT + Refresh rotation** · **RBAC** بصلاحيات `resource.action`.
- **قاعدة البيانات:** SQL Server · Soft Delete · UTC · `DECIMAL(18,4)` للأموال · Append-only للسجلّات المالية.

---

## 🛠️ التقنيات

.NET 9 · ASP.NET Core 9 Web API · EF Core 9 · SQL Server · MediatR · FluentValidation · Asp.Versioning · Swagger.

---

_يلتزم كل الكود بالتوثيق المعتمد في [SmartApp-Architecture/](SmartApp-Architecture/). لا يُكتب كود يخالف القرارات المعتمدة._
