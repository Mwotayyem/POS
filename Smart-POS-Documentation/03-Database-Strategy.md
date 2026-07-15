# 03 — Database Strategy (استراتيجية قاعدة البيانات)

> هذه الوثيقة تشرح **لماذا** اخترنا **SQL Server** كمصدر الحقيقة (Source of Truth)، ولماذا نموذج **Shared-Schema Multi-Tenancy**، وكيف صمّمنا للانتقال المستقبلي للتوسّع (Sharding) دون إعادة كتابة. المعايير التفصيلية للجداول في [04-Database-Design.md](04-Database-Design.md).

---

## 1) لماذا SQL Server علائقي (لا JSON / NoSQL للبيانات الأساسية)

النظام **محاسبي بالدرجة الأولى**: كل فاتورة، حركة مخزون، ودفعة يجب أن تكون **صحيحة، متّسقة، وقابلة للتدقيق**. هذا يفرض قاعدة بيانات علائقية بخصائص **ACID** قوية.

| المتطلّب | لماذا يفرض العلائقية | لماذا يفشل تخزين JSON/NoSQL |
|----------|----------------------|-----------------------------|
| **سلامة المعاملات (ACID)** | فاتورة + بنود + مخزون في معاملة واحدة | أغلب مخازن الوثائق تضمن الذرّية على مستوى وثيقة واحدة فقط |
| **التكامل المرجعي (FK)** | بند فاتورة يجب أن يشير لمنتج موجود | لا FK حقيقية في JSON — تكامل هشّ |
| **التجميع والتقارير (Aggregations)** | `SUM`, `GROUP BY`, `JOIN` عبر ملايين الصفوف | استعلام JSON بطيء وغير مفهرس جيداً |
| **الدقّة المالية** | `DECIMAL(18,4)` بلا أخطاء عائمة | JSON غالباً يمثّل الأرقام كـ float |
| **العزل (Isolation Levels)** | RCSI، أقفال دقيقة | ضعيف أو غائب في مخازن الوثائق |
| **التدقيق والاستعادة** | استعلام تاريخي، Soft Delete، Temporal | صعب على بنية غير علائقية |

**القرار الحاكم (من [04-Database-Design.md](04-Database-Design.md) §8):**
> **ممنوع منعاً باتاً** تخزين الفواتير أو بنودها أو الحركات المحاسبية أو المخزون كـ JSON. البيانات المحاسبية **علائقية مطبّعة** حصراً.

**لماذا SQL Server تحديداً (لا PostgreSQL/MySQL)؟**
- تكامل ممتاز مع **.NET 9 / EF Core 9** (المزوّد الأنضج).
- ميزات مؤسسية جاهزة: **RCSI**, **Row-Level Security (RLS)**, **Temporal Tables**, **ROWVERSION** للتزامن التفاؤلي، **`OUTPUT` clause** للتسلسلات الذرّية.
- خبرة الفريق وأدوات التشغيل (SSMS, backup/restore, Always On).
- توفّر سحابي (Azure SQL) لتوسّع مُدار مستقبلاً.

---

## 2) نماذج تعدّد المستأجرين الثلاثة (Multi-Tenancy Models)

عند بناء SaaS، هناك ثلاث استراتيجيات لعزل بيانات المستأجرين:

### النموذج A — Database-per-Tenant (قاعدة لكل مستأجر)

كل مستأجر له قاعدة بيانات مستقلة تماماً.

```
Tenant 1 ──▶ DB_Tenant_1
Tenant 2 ──▶ DB_Tenant_2
Tenant N ──▶ DB_Tenant_N   (N قاعدة بيانات)
```

### النموذج B — Schema-per-Tenant (Schema لكل مستأجر)

قاعدة واحدة، لكن لكل مستأجر schema منفصل (`tenant1.Products`, `tenant2.Products`).

```
        ┌──────── One Database ────────┐
Tenant1 ──▶ [tenant1].Products, Invoices...
Tenant2 ──▶ [tenant2].Products, Invoices...
```

### النموذج C — Shared-Schema with TenantId (Schema مشترك + عمود العزل) ✅

قاعدة واحدة، جداول مشتركة، كل صفّ يحمل `TenantId`، والعزل عبر **Global Query Filter** (+ RLS اختياري).

```
        ┌──────── One Database, Shared Tables ────────┐
        │  Products(TenantId, ...)                     │
        │  SalesInvoices(TenantId, ...)                │
        │  WHERE TenantId = @current  (تلقائي)         │
```

---

## 3) جدول المقارنة والقرار (Comparison & Decision)

| المعيار | A: DB-per-Tenant | B: Schema-per-Tenant | **C: Shared-Schema + TenantId** |
|---------|------------------|----------------------|--------------------------------|
| **قوة العزل** | 🟢 الأقوى (فيزيائي) | 🟡 متوسط | 🟠 منطقي (Query Filter + RLS) |
| **التكلفة لكل مستأجر** | 🔴 عالية جداً | 🟡 متوسطة | 🟢 الأدنى (مشاركة الموارد) |
| **قابلية التوسّع (آلاف المستأجرين)** | 🔴 صعبة (آلاف القواعد) | 🔴 صعبة (حدّ schemas) | 🟢 ممتازة (صفوف فقط) |
| **صيانة/ترحيلات (Migrations)** | 🔴 تشغيلها N مرّة | 🔴 تشغيلها N مرّة | 🟢 مرّة واحدة للكل |
| **تكلفة النسخ الاحتياطي** | 🔴 N نسخة | 🟡 نسخة واحدة كبيرة | 🟢 نسخة واحدة |
| **استعادة مستأجر واحد** | 🟢 سهلة (استعادة قاعدته) | 🟡 متوسطة | 🔴 معقّدة (فلترة) |
| **"الجار الصاخب" (Noisy Neighbor)** | 🟢 معزول | 🟡 جزئي | 🔴 يحتاج ضبط موارد |
| **تخصيص schema لكل مستأجر** | 🟢 ممكن | 🟡 محدود | 🔴 موحّد للكل |
| **التعقيد التشغيلي** | 🔴 عالٍ | 🟡 متوسط | 🟢 منخفض |
| **ملاءمة SaaS ضخم منخفض التكلفة** | 🔴 ضعيفة | 🟡 متوسطة | 🟢 ممتازة |

### القرار المُتّخذ ✅

> **النموذج C — Single Database, Shared Schema, Row-Level Isolation via `TenantId`.**

### المبرّر (Justification)

1. **الهدف الأساسي** (من [01-Project-Overview.md](01-Project-Overview.md)): خدمة **آلاف المستأجرين من نشر واحد بتكلفة منخفضة**. النموذج C وحده يحقّق ذلك اقتصادياً.
2. **صيانة واحدة**: ترحيلة EF Core واحدة تُطبَّق على الجميع فوراً — لا N قاعدة.
3. **كفاءة الموارد**: مشاركة الـ connection pool، الذاكرة، والـ CPU عبر المستأجرين.
4. **العزل يُضمَن معمارياً** لا يدوياً: **EF Core Global Query Filter** يُلحق `TenantId` بكل استعلام تلقائياً، مع **SQL Server RLS** كطبقة دفاع ثانية (defense-in-depth).

**نُقرّ بالمقايضات (Trade-offs المقبولة):**
- استعادة مستأجر واحد أعقد → نعالجها بأدوات تصدير/استيراد مخصّصة و Point-in-Time على مستوى منطقي.
- الجار الصاخب → نعالجه بمراقبة الأداء وحدود موارد، ونرحّل المستأجرين الكبار لاحقاً (انظر §5).

---

## 4) كيف يُطبَّق العزل عملياً (Isolation Implementation)

### 4.1 طبقة التطبيق — EF Core Global Query Filter

```csharp
// يُطبَّق على كل كيان يرث BaseEntity
modelBuilder.Entity<TEntity>()
    .HasQueryFilter(e =>
        e.TenantId == _tenantProvider.CurrentTenantId
        && !e.IsDeleted);
```

- **مستحيل نسيان الفلترة**: كل `SELECT` يُلحق به `WHERE TenantId = @current AND IsDeleted = 0` تلقائياً.
- `TenantId` يأتي من الـ JWT عبر `ITenantProvider` — لا من إدخال المستخدم (انظر [02-System-Architecture.md](02-System-Architecture.md) §8).

### 4.2 طبقة قاعدة البيانات — Row-Level Security (اختياري، defense-in-depth)

حتى لو تسرّب استعلام خام يتجاوز EF، تمنعه RLS على مستوى المحرّك:

```sql
CREATE FUNCTION dbo.fn_TenantAccessPredicate(@TenantId BIGINT)
    RETURNS TABLE WITH SCHEMABINDING
AS
    RETURN SELECT 1 AS ok
    WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS BIGINT);
GO

CREATE SECURITY POLICY dbo.TenantIsolationPolicy
    ADD FILTER PREDICATE dbo.fn_TenantAccessPredicate([TenantId]) ON [dbo].[SalesInvoices],
    ADD BLOCK  PREDICATE dbo.fn_TenantAccessPredicate([TenantId]) ON [dbo].[SalesInvoices]
    WITH (STATE = ON);
```

تُفصّل RLS في [27-Security.md](27-Security.md).

### 4.3 فهرسة العزل (Isolation Indexing)

كل جدول أعمال يبدأ فهرسه بـ `TenantId` ليكون أول عمود بحث (من [04-Database-Design.md](04-Database-Design.md) §6):

```sql
CREATE NONCLUSTERED INDEX [IX_SalesInvoices_Tenant_Store]
    ON [dbo].[SalesInvoices] ([TenantId], [StoreId])
    WHERE [IsDeleted] = 0;
```

---

## 5) استراتيجية الترحيل المستقبلي للتوسّع (Future Sharding Strategy)

النموذج C ممتاز لآلاف المستأجرين، لكنه سيصل لحدّ عند عشرات الآلاف أو مع مستأجرين ضخمين. نصمّم من **الآن** بحيث يكون الانتقال ممكناً دون إعادة كتابة.

### مبدأ التصميم المُسبق (Design-for-Shard)

1. **`TenantId` هو مفتاح التقسيم (Shard Key)** في كل جدول أعمال أصلاً — لا حاجة لتعديل المخطّط لاحقاً.
2. **لا اعتماد على `IDENTITY` عبر المستأجرين للأرقام العامّة** — أرقام الفواتير من `Sequences` لكل مستأجر (من [04-Database-Design.md](04-Database-Design.md) §10)، فتبقى صحيحة بعد التقسيم.
3. **لا JOINs عبر مستأجرين** — كل استعلام محصور بمستأجر واحد، فالتقسيم لا يكسر شيئاً.

### مسار التوسّع المرحلي

```
المرحلة 1 (الآن):
   All Tenants ──▶ [ Single SQL Server DB ]

المرحلة 2 (نمو):
   Read Replicas للتقارير + فصل القراءة عن الكتابة (RCSI)

المرحلة 3 (توسّع أفقي):
   Tenant Router ──┬──▶ Shard A (Tenants 1..5000)
                   ├──▶ Shard B (Tenants 5001..10000)
                   └──▶ Shard C (Tenants 10001..)
   + جدول Catalog مركزي: TenantId ──▶ ShardConnectionString

المرحلة 4 (مستأجرون ضخام / VIP):
   ترقية مستأجر كبير من Shared إلى Database-per-Tenant مخصّصة
   (النموذج الهجين — Hybrid)
```

- **Tenant Catalog**: جدول توجيه مركزي (`TenantId → Shard`) يقرأه `ITenantProvider` لاختيار الاتصال.
- **الهجرة**: نقل مستأجر = تصدير صفوفه (كلّها تحمل `TenantId`) لقاعدة أخرى، ثم تحديث سطر التوجيه.

تُفصّل خطوات التوسّع زمنياً في [30-Future-Roadmap.md](30-Future-Roadmap.md) و [29-Performance.md](29-Performance.md).

---

## 6) التطبيع (Normalization)

- **الحدّ الأدنى: 3NF** لكل الجداول التشغيلية والمحاسبية (من [04-Database-Design.md](04-Database-Design.md) §1).
- **de-normalization مقصود ومبرَّر فقط** في جداول التقارير/الملخّصات (مثل جدول ملخّص مبيعات يومي مُجمَّع مسبقاً لتسريع لوحة التحكّم).
- كل قيمة تُحسب مرّة وتُشتقّ منطقياً؛ لا تكرار غير مبرَّر يهدّد التكامل.

**مثال على snapshot مبرَّر:** عند ترحيل فاتورة بيع، نخزّن `UnitPriceAtSale` و `CostAtSale` في بند الفاتورة (لا نعتمد على سعر المنتج الحالي) — لأن السعر قد يتغيّر لاحقاً، والفاتورة سجل تاريخي ثابت.

---

## 7) المعاملات والتزامن (Transactions & Concurrency)

### Transactions

- كل عملية متعدّدة الجداول تُغلَّف في **معاملة واحدة** عبر `Unit of Work` (من [04-Database-Design.md](04-Database-Design.md) §7).
- المخزون يُحدَّث ضمن **نفس معاملة** الفاتورة → لا فاتورة بلا حركة مخزون.

### Optimistic Concurrency

كل جدول أعمال يحمل `ConcurrencyStamp ROWVERSION`. EF Core يستخدمه في `WHERE` عند التحديث:

```sql
UPDATE Products SET Price = @p
WHERE Id = @id AND ConcurrencyStamp = @originalStamp;
-- إذا 0 صفوف تأثّرت → DbUpdateConcurrencyException (شخص آخر عدّل)
```

نختار **التفاؤلي** (لا Pessimistic Locking) لأن التعارض نادر في التجزئة، والأقفال المتشائمة تضرّ الأداء.

### RCSI (Read Committed Snapshot Isolation)

نفعّل RCSI على قاعدة البيانات:

```sql
ALTER DATABASE [SmartPos] SET READ_COMMITTED_SNAPSHOT ON;
```

**لماذا؟** القرّاء (التقارير، لوحة التحكّم) لا يحجبون الكتّاب (نقاط البيع) والعكس — يقرأ كل استعلام لقطة متّسقة دون أقفال مشتركة. حيوي لنظام POS كثيف الكتابة مع تقارير متزامنة.

---

## 8) متى يُسمح بـ JSON (When JSON is Allowed)

`NVARCHAR(MAX)` مع `ISJSON()` check constraint **فقط** لـ (من [04-Database-Design.md](04-Database-Design.md) §8):

| مسموح ✅ | ممنوع 🚫 |
|---------|---------|
| إعدادات مرنة (Tenant/Store settings) | الفواتير وبنودها |
| قوالب الطباعة (receipt templates) | حركات المخزون |
| الحقول المخصّصة للمنتجات (Custom Fields) | الحركات المحاسبية والدفعات |
| Snapshot تاريخي غير قابل للاستعلام العلائقي | أي بيانات تحتاج `SUM`/`JOIN`/تقارير |

```sql
[Settings] NVARCHAR(MAX) NULL
    CONSTRAINT CK_TenantSettings_Json CHECK ([Settings] IS NULL OR ISJSON([Settings]) = 1),
```

**القاعدة الذهبية:** إذا احتجت يوماً أن تعمل `SUM` أو `JOIN` أو `GROUP BY` على حقل → يجب أن يكون عموداً علائقياً، لا JSON.

---

## 9) ملخّص القرارات (Decision Summary)

| القرار | الاختيار | المرجع |
|--------|----------|--------|
| محرّك البيانات | **SQL Server 2022** | §1 |
| نموذج العزل | **Shared-Schema + TenantId** (C) | §3 |
| طبقة عزل ثانية | **RLS** (اختياري) | §4.2, [27](27-Security.md) |
| التطبيع | **3NF** حداً أدنى | §6 |
| التزامن | **Optimistic (ROWVERSION)** | §7 |
| العزل الافتراضي | **RCSI** | §7 |
| الأموال | `DECIMAL(18,4)` | [04](04-Database-Design.md) |
| الحذف | **Soft Delete** إجباري | [04](04-Database-Design.md) |
| مسار التوسّع | **Sharding by TenantId** مصمّم مسبقاً | §5 |

---

## 10) الوثائق ذات الصلة

| الوثيقة | الغرض |
|---------|-------|
| [04-Database-Design.md](04-Database-Design.md) | المعايير الحاكمة والأعمدة المشتركة والقوالب |
| [24-MultiTenant.md](24-MultiTenant.md) | تعدّد المستأجرين والتخصيص |
| [27-Security.md](27-Security.md) | RLS والأمان |
| [29-Performance.md](29-Performance.md) | الأداء والفهرسة والتوسّع |
| [30-Future-Roadmap.md](30-Future-Roadmap.md) | خارطة طريق التوسّع |

---

_وثيقة حيّة — تُحدَّث مع تطوّر استراتيجية التخزين._
