# Smart ERP POS — SaaS Platform Documentation

> نظام **ERP + POS** سحابي متعدّد المستأجرين (Multi-Tenant SaaS)، مبنيّ على **.NET 9** و **SQL Server**، مصمَّم ليُباع تجارياً لآلاف الشركات والمتاجر من مصدر كود واحد ونشر واحد.

---

## 🎯 الرؤية (Vision)

بناء منصّة تجارية تنافس **Microsoft Dynamics 365 Business Central**, **SAP Business One**, **Odoo**, و **Zoho Inventory** — بحيث:

- **مصدر كود واحد** (Single Codebase) يخدم آلاف العملاء.
- **نشر واحد** (Single Deployment) — لا تعديل على الكود عند إضافة عميل جديد.
- **عزل تام للبيانات** بين المستأجرين (Tenant Isolation).
- **قابلية تخصيص كاملة** (شعار، ألوان، عملة، ضريبة، لغة...) دون إعادة نشر.

هذا **ليس** مشروعاً تعليمياً ولا تطبيق CRUD — بل نظام **production-grade** جاهز للبيع.

---

## 🏢 القطاعات المستهدفة (Target Business Types)

| القطاع | Business Type |
|--------|---------------|
| السوبرماركت | Supermarket |
| البقالات | Grocery |
| محلات الألبان | Dairy Shop |
| المطاعم | Restaurant |
| الصيدليات | Pharmacy |
| محلات الملابس | Clothing |
| الإلكترونيات | Electronics |
| قطع غيار السيارات | Auto Parts |
| الأدوات المنزلية | Hardware |
| مستحضرات التجميل | Cosmetics |

**كل شيء قابل للإعداد (Configurable)** — لا يُعدَّل الكود المصدري عند استقبال عميل جديد.

---

## 🧱 المكدّس التقني (Technology Stack)

| الطبقة | التقنية |
|--------|---------|
| Runtime | **.NET 9** |
| API | **ASP.NET Core Web API** |
| UI (Admin/Back-office) | **ASP.NET Core MVC** + Bootstrap 5 + jQuery + AJAX + DataTables + Chart.js |
| Real-time | **SignalR** |
| ORM | **Entity Framework Core 9** |
| Database | **SQL Server 2022** |
| Auth | **ASP.NET Identity** + **JWT** |
| Validation | **FluentValidation** |
| Mapping | **AutoMapper** |
| CQRS/Mediator | **MediatR** |
| Logging | **Serilog** |
| Cache | **Redis** (لاحقاً) |
| Background Jobs | **Hangfire** |

**المبادئ:** SOLID · Clean Architecture · Repository + Unit of Work · DDD concepts · CQRS (عند الحاجة).

---

## 📁 خريطة التوثيق (Documentation Map)

### الملفات الجذرية (Root Docs)

| # | الملف | المحتوى |
|---|-------|---------|
| — | [README.md](README.md) | هذا الملف — نقطة الدخول |
| 01 | [01-Project-Overview.md](01-Project-Overview.md) | نظرة عامة، الأهداف، النطاق |
| 02 | [02-System-Architecture.md](02-System-Architecture.md) | Clean Architecture، الطبقات، التدفّق |
| 03 | [03-Database-Strategy.md](03-Database-Strategy.md) | استراتيجية التخزين، لماذا SQL Server، Multi-Tenant DB |
| 04 | [04-Database-Design.md](04-Database-Design.md) | التصميم العام، المعايير الموحّدة، الأعمدة المشتركة |
| 05 | [05-Authentication.md](05-Authentication.md) | تسجيل الدخول، JWT، Refresh Token، OTP |
| 06 | [06-Roles-And-Permissions.md](06-Roles-And-Permissions.md) | الأدوار، الصلاحيات، Claims |
| 07 | [07-Store-Management.md](07-Store-Management.md) | إدارة المتاجر والفروع |
| 08 | [08-Products.md](08-Products.md) | المنتجات (باركود، وحدات، متغيّرات...) |
| 09 | [09-Categories.md](09-Categories.md) | التصنيفات الشجرية |
| 10 | [10-Suppliers.md](10-Suppliers.md) | الموردون |
| 11 | [11-Customers.md](11-Customers.md) | العملاء، حدود الائتمان، نقاط الولاء |
| 12 | [12-Warehouses.md](12-Warehouses.md) | المستودعات، التحويل، التسوية |
| 13 | [13-Inventory.md](13-Inventory.md) | المخزون وحركاته |
| 14 | [14-Purchase-Invoices.md](14-Purchase-Invoices.md) | فواتير الشراء والاستلام |
| 15 | [15-Sales-Invoices.md](15-Sales-Invoices.md) | فواتير البيع |
| 16 | [16-Sales-Returns.md](16-Sales-Returns.md) | مرتجعات البيع |
| 17 | [17-Purchase-Returns.md](17-Purchase-Returns.md) | مرتجعات الشراء |
| 18 | [18-POS.md](18-POS.md) | نقطة البيع (الكاشير، الشفت، End Of Day) |
| 19 | [19-Offers-And-Promotions.md](19-Offers-And-Promotions.md) | العروض والترويج |
| 20 | [20-Reports.md](20-Reports.md) | التقارير |
| 21 | [21-Dashboard.md](21-Dashboard.md) | لوحة التحكم |
| 22 | [22-Notifications.md](22-Notifications.md) | الإشعارات |
| 23 | [23-Settings.md](23-Settings.md) | الإعدادات |
| 24 | [24-MultiTenant.md](24-MultiTenant.md) | تعدّد المستأجرين والتخصيص |
| 25 | [25-API-Design.md](25-API-Design.md) | تصميم الـ API |
| 26 | [26-Deployment.md](26-Deployment.md) | النشر |
| 27 | [27-Security.md](27-Security.md) | الأمان (OWASP) |
| 28 | [28-Backup-And-Restore.md](28-Backup-And-Restore.md) | النسخ الاحتياطي والاستعادة |
| 29 | [29-Performance.md](29-Performance.md) | الأداء |
| 30 | [30-Future-Roadmap.md](30-Future-Roadmap.md) | خارطة الطريق المستقبلية |
| 31 | [31-Subscription-Billing.md](31-Subscription-Billing.md) | نظام الاشتراكات والفوترة (طبقة المنصّة) |

### المجلدات الفرعية

- **[Database/](Database/)** — ERD، الجداول، العلاقات، الفهارس، الإجراءات المخزّنة.
- **[API/](API/)** — توثيق الـ endpoints لكل وحدة.
- **[UI/](UI/)** — توثيق الشاشات.
- **[Images/](Images/)** — الصور والمخططات.

### مجلد البرومبت (AI-Prompt)

قواعد توليد الكود بالذكاء الاصطناعي — انظر [../AI-Prompt/MASTER_PROMPT.md](../AI-Prompt/MASTER_PROMPT.md).

---

## 📐 المعايير الموحّدة (Global Conventions)

> هذه المعايير **مُلزِمة** لكل ملف في التوثيق ولكل جدول في قاعدة البيانات. مفصّلة في [04-Database-Design.md](04-Database-Design.md).

- **كل جدول أعمال** يحتوي حتماً: `Id`, `TenantId`, `StoreId`, `CreatedDate`, `CreatedBy`, `ModifiedDate`, `ModifiedBy`, `DeletedDate`, `DeletedBy`, `IsDeleted`, `ConcurrencyStamp`.
- **Soft Delete** إجباري (لا حذف فعلي للبيانات المحاسبية).
- **كل استعلام يُفلتر تلقائياً** بـ `TenantId` عبر **EF Core Global Query Filter**.
- **المفاتيح**: `Id` من نوع `BIGINT IDENTITY` أو `UNIQUEIDENTIFIER` حسب الجدول (مبرّر في كل ملف).
- **الأموال**: `DECIMAL(18,4)` — لا `FLOAT` أبداً للمبالغ.
- **التواريخ**: `DATETIME2` بتوقيت UTC.

---

## 🗂️ بنية كل ملف شاشة (Page Doc Template)

كل ملف شاشة (مثل Products, POS) يتبع البنية التالية:

1. الهدف من الصفحة (Purpose)
2. صلاحيات الدخول (Access Permissions)
3. تصميم الصفحة (Page Layout)
4. جميع الأزرار (Buttons)
5. جميع الحقول (Fields)
6. التحقق (Validation)
7. قواعد العمل (Business Rules)
8. جداول قاعدة البيانات (Database Tables)
9. الـ API
10. مخطط التدفّق (Flow Chart)
11. ماذا يحدث عند: الحذف / التعديل / تغيير السعر / انتهاء العرض
12. سجل التدقيق (Audit Log)
13. الأخطاء المحتملة (Possible Errors)
14. الأداء (Performance)
15. الأمان (Security)

---

## 🚦 حالة التوثيق (Status)

| القسم | الحالة |
|-------|--------|
| البنية الأساسية | ✅ مكتملة |
| ملفات الشاشات (01→31) | ✅ مكتملة (31 ملف) |
| Database/ | ✅ مكتملة (5 ملفات) |
| API/ | ✅ مكتملة (4 ملفات) |
| UI/ | ✅ مكتملة (5 ملفات) |
| AI-Prompt/ | ✅ مكتملة (10 ملفات) |

**الإجمالي:** 55 ملف توثيق · ~15,500 سطر.

---

_وثيقة حيّة — تُحدَّث مع تطوّر التصميم._
