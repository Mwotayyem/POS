# 22 — Notifications (الإشعارات)

> نظام الإشعارات يُبقي أصحاب المتاجر والمديرين على اطّلاع لحظي بالأحداث المهمّة: **نقص المخزون، المنتجات المنتهية، العروض المنتهية، فشل النسخ الاحتياطي، التحديثات الجديدة، والدفعات المعلّقة** — عبر قنوات متعدّدة (داخل التطبيق via SignalR، البريد، SMS/WhatsApp).

> يلتزم بالمرجع الحاكم [04-Database-Design.md](04-Database-Design.md) (الأعمدة المشتركة، `TenantId`، Soft Delete، الفهرسة). مرتبط بـ [13-Inventory.md](13-Inventory.md), [19-Offers-And-Promotions.md](19-Offers-And-Promotions.md), [23-Settings.md](23-Settings.md), [28-Backup-And-Restore.md](28-Backup-And-Restore.md).

---

## 1) الهدف من الصفحة (Purpose)

توفير مركز إشعارات موحّد (Notification Center) داخل التطبيق + توصيل خارجي (بريد/رسائل)، بحيث:

- تُولَّد الإشعارات **تلقائياً** من أحداث النظام (Event-driven) أو من **مهام خلفية مجدولة** (Background Jobs / Hangfire).
- تُوصَّل **لحظياً** داخل التطبيق عبر **SignalR** (bell icon + toast)، ولمن يريد عبر البريد/SMS/WhatsApp حسب تفضيلاته.
- تُتتبَّع **حالة القراءة** (مقروء/غير مقروء) لكل مستخدم، وتُصنَّف حسب النوع والخطورة.

---

## 2) صلاحيات الدخول (Access Permissions)

| الصلاحية | الوصف | يملكها |
|----------|-------|--------|
| `Notifications.View` | عرض إشعاراتي | كل مستخدم مصادَق |
| `Notifications.ManageRules` | ضبط قواعد التوليد والحدود (مثل عتبة نقص المخزون) | Tenant Owner, Store Manager |
| `Notifications.MarkRead` | تعليم كمقروء | صاحب الإشعار |
| `Notifications.Broadcast` | إرسال إشعار يدوي للفريق | Tenant Owner |

**استهداف الإشعار (Targeting):** كل إشعار موجَّه إمّا لمستخدم محدّد (`UserId`)، أو لدور (`TargetRole`)، أو لكل مستخدمي فرع (`StoreId`)، أو لكل الشركة (`TenantId` فقط). التصفية تحترم العزل: مستخدم لا يرى إلا إشعارات مستأجره وضمن نطاق فرعه/دوره.

---

## 3) تصميم الصفحة (Page Layout)

```
┌────────────────────────────────────────────────────────────────────┐
│  🔔 (3)   ← جرس في الترويسة، عدّاد غير المقروء لحظي (SignalR)        │
├────────────────────────────────────────────────────────────────────┤
│  مركز الإشعارات        [ الكل | غير مقروء ]   [ ✔ تعليم الكل مقروء ] │
├──────┬──────────────────────────────────────────┬─────────┬─────────┤
│ الأثر│ العنوان                                    │ النوع    │ الوقت    │
├──────┼──────────────────────────────────────────┼─────────┼─────────┤
│ 🔴   │ نقص مخزون: حليب المراعي (متبقّي 4)          │ Low Stock│ قبل 5 د │
│ 🟠   │ انتهت صلاحية: دواء Panadol (Batch#221)     │ Expired  │ قبل 1 س │
│ 🟡   │ عرض "خصم الجمعة" ينتهي غداً                 │ Offer End│ قبل 3 س │
│ 🔴   │ فشل النسخ الاحتياطي الليلي                  │ Backup   │ قبل 8 س │
│ 🔵   │ تحديث جديد v2.4 متاح                        │ Update   │ أمس     │
│ 🟠   │ 5 دفعات معلّقة تتجاوز الاستحقاق             │ Payment  │ أمس     │
└──────┴──────────────────────────────────────────┴─────────┴─────────┘
```

النقر على إشعار يفتح **رابطاً عميقاً (Deep Link)** إلى الكيان المصدر (المنتج/الفاتورة/العرض).

---

## 4) جميع الأزرار (Buttons)

| الزر | الإجراء | الصلاحية |
|------|---------|----------|
| **جرس الإشعارات** | يفتح القائمة المنسدلة (آخر 10 + عدّاد) | `Notifications.View` |
| **تعليم كمقروء** | يضبط `IsRead=1` لإشعار | `Notifications.MarkRead` |
| **تعليم الكل مقروء** | يضبط الكل مقروء دفعة واحدة | `Notifications.MarkRead` |
| **الانتقال للمصدر** | Deep link للكيان (Product/Invoice/Offer) | حسب صلاحية الكيان |
| **إعدادات الإشعارات** | ضبط القنوات والعتبات | `Notifications.ManageRules` |
| **إرسال إشعار للفريق** | Broadcast يدوي | `Notifications.Broadcast` |

---

## 5) جميع الحقول (Fields)

| الحقل | النوع | ملاحظة |
|-------|------|--------|
| `Type` | قائمة | LOW_STOCK / EXPIRED / OFFER_ENDING / BACKUP_FAILURE / NEW_UPDATE / PENDING_PAYMENT |
| `Severity` | قائمة | INFO / WARNING / CRITICAL (يحدّد اللون والقناة) |
| `Title` / `Body` | نص | نصّ الإشعار (قابل للترجمة) |
| `LinkUrl` | نص | رابط عميق للكيان المصدر |
| `TargetUserId` / `TargetRole` / `StoreId` | مرجع | الاستهداف |
| `Channels` | JSON | القنوات المطلوبة `["InApp","Email","WhatsApp"]` |
| `IsRead` / `ReadAtUtc` | bit / datetime | حالة القراءة |

---

## 6) التحقق (Validation)

| القاعدة | الرسالة |
|---------|---------|
| `Type` ضمن القائمة المعرّفة (CHECK constraint) | "نوع إشعار غير معروف" |
| `Severity` ضمن {INFO, WARNING, CRITICAL} | "درجة خطورة غير صالحة" |
| استهداف واحد على الأقل (User/Role/Store/Tenant) | "يجب تحديد جمهور الإشعار" |
| عتبة نقص المخزون (`ReorderLevel`) ≥ 0 | "العتبة يجب أن تكون رقماً موجباً" |
| `Channels` يمرّ فحص `ISJSON` | "بنية القنوات غير صالحة" |

---

## 7) قواعد العمل (Business Rules)

### 7.1 أنواع الإشعارات وآلية التوليد

| النوع | المحفّز (Trigger) | الآلية |
|-------|-------------------|--------|
| **Low Stock** | رصيد منتج ≤ `ReorderLevel` | Event عند كل حركة مخزون تُخفّض الرصيد (بيع/تحويل) + Job دوري احتياطي |
| **Expired Products** | تاريخ صلاحية Batch ≤ اليوم (أو ضمن نافذة تحذير) | **Background Job يومي** يفحص `Batches`/`Stock` |
| **Offers Ending** | عرض ينتهي خلال 24–48 ساعة | Background Job يومي يفحص `Offers.EndsUtc` |
| **Backup Failure** | فشل وظيفة النسخ الاحتياطي | Event من معالِج فشل Job النسخ (انظر [28-Backup-And-Restore.md](28-Backup-And-Restore.md)) |
| **New Updates** | إصدار جديد للمنصّة | Broadcast من Platform Admin لكل المستأجرين |
| **Pending Payments** | دفعة عميل/مورد تجاوزت الاستحقاق | Background Job يومي يفحص `CustomerPayments`/`SupplierPayments` |

### 7.2 قواعد حاكمة

1. **منع التكرار (Deduplication):** لا يُولَّد نفس الإشعار لنفس الكيان مرّتين ضمن نافذة زمنية (مثلاً Low Stock لنفس المنتج مرّة كل 24 ساعة) — يُحرَس بـ `DedupKey` فريد.
2. **الخطورة تحدّد القناة:** `CRITICAL` (فشل نسخ احتياطي) يُرسَل In-App + Email + WhatsApp؛ `INFO` (تحديث) In-App فقط. القنوات الفعلية تُقرأ من تفضيلات المستأجر ([23-Settings.md](23-Settings.md)).
3. **الاستهداف الذكي:** Low Stock لفرع يذهب لمدير ذلك الفرع؛ Backup Failure للـ Owner؛ Pending Payments للمحاسب.
4. **التوصيل الخارجي عبر طابور:** الإرسال (بريد/رسائل) يُدفَع إلى Background Job (Hangfire) مع إعادة محاولة (Retry) وسجل نتيجة — لا يُعطّل مسار الطلب.
5. **الحذف ناعم + انتهاء صلاحية:** الإشعارات القديمة تُؤرشف/تُنظّف بـ Job دوري (Retention) مع الحفاظ على Soft Delete.

---

## 8) جداول قاعدة البيانات (Database Tables)

### 8.1 جدول `Notifications`

```sql
CREATE TABLE [dbo].[Notifications]
(
    [Type]             VARCHAR(30)   NOT NULL,   -- LOW_STOCK / EXPIRED / OFFER_ENDING / BACKUP_FAILURE / NEW_UPDATE / PENDING_PAYMENT
    [Severity]         VARCHAR(10)   NOT NULL CONSTRAINT DF_Notifications_Sev DEFAULT ('INFO'),
    [Title]            NVARCHAR(200) NOT NULL,
    [Body]             NVARCHAR(1000) NULL,
    [LinkUrl]          NVARCHAR(400) NULL,        -- deep link للكيان المصدر
    [EntityType]       VARCHAR(40)   NULL,        -- Product / Offer / Invoice ...
    [EntityId]         BIGINT        NULL,        -- معرّف الكيان المصدر
    [DedupKey]         VARCHAR(120)  NULL,        -- لمنع التكرار ضمن نافذة زمنية
    [TargetUserId]     BIGINT        NULL,        -- مستخدم محدّد (NULL = دور/فرع/شركة)
    [TargetRole]       VARCHAR(40)   NULL,        -- StoreManager / Accountant / Owner
    [Channels]         NVARCHAR(200) NULL,        -- JSON: ["InApp","Email","WhatsApp"]
    [IsRead]           BIT           NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT (0),
    [ReadAtUtc]        DATETIME2(3)  NULL,
    [IsSentExternally] BIT           NOT NULL CONSTRAINT DF_Notifications_Sent DEFAULT (0),

    -- ===== الأعمدة المشتركة =====
    [Id]               BIGINT        IDENTITY(1,1) NOT NULL,
    [TenantId]         BIGINT        NOT NULL,
    [StoreId]          BIGINT        NULL,         -- NULL = على مستوى الشركة
    [CreatedDate]      DATETIME2(3)  NOT NULL CONSTRAINT DF_Notifications_CreatedDate DEFAULT (SYSUTCDATETIME()),
    [CreatedBy]        BIGINT        NULL,
    [ModifiedDate]     DATETIME2(3)  NULL,
    [ModifiedBy]       BIGINT        NULL,
    [DeletedDate]      DATETIME2(3)  NULL,
    [DeletedBy]        BIGINT        NULL,
    [IsDeleted]        BIT           NOT NULL CONSTRAINT DF_Notifications_IsDeleted DEFAULT (0),
    [ConcurrencyStamp] ROWVERSION    NOT NULL,

    CONSTRAINT [PK_Notifications] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Notifications_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_Notifications_Store]  FOREIGN KEY ([StoreId])  REFERENCES [dbo].[Stores]([Id]),
    CONSTRAINT [CK_Notifications_Type]
        CHECK ([Type] IN ('LOW_STOCK','EXPIRED','OFFER_ENDING','BACKUP_FAILURE','NEW_UPDATE','PENDING_PAYMENT')),
    CONSTRAINT [CK_Notifications_Sev]
        CHECK ([Severity] IN ('INFO','WARNING','CRITICAL')),
    CONSTRAINT [CK_Notifications_Channels]
        CHECK ([Channels] IS NULL OR ISJSON([Channels]) = 1)
);
GO

-- فهرس العزل + صندوق وارد المستخدم (غير المقروء أولاً)
CREATE NONCLUSTERED INDEX [IX_Notifications_Tenant_User_Unread]
    ON [dbo].[Notifications] ([TenantId], [TargetUserId], [IsRead])
    INCLUDE ([Type], [Severity], [Title], [CreatedDate])
    WHERE [IsDeleted] = 0;
GO

-- فهرس العزل القياسي (TenantId, StoreId)
CREATE NONCLUSTERED INDEX [IX_Notifications_Tenant_Store]
    ON [dbo].[Notifications] ([TenantId], [StoreId]) WHERE [IsDeleted] = 0;
GO

-- منع التكرار ضمن النافذة الزمنية (DedupKey فريد داخل المستأجر عند وجوده)
CREATE UNIQUE NONCLUSTERED INDEX [UX_Notifications_Dedup]
    ON [dbo].[Notifications] ([TenantId], [DedupKey])
    WHERE [DedupKey] IS NOT NULL AND [IsDeleted] = 0;
GO
```

> **ملاحظة:** حالة القراءة مخزَّنة هنا مباشرة بافتراض أن الإشعار موجَّه لمستخدم/دور محدّد. للبثّ الجماعي (Broadcast لكل مستخدمي الشركة) مع تتبّع قراءة مستقلّ لكل مستخدم، يُضاف جدول ربط `NotificationReads (NotificationId, UserId, ReadAtUtc)` — يُوثَّق عند تفعيل البثّ واسع النطاق.

---

## 9) الـ API

| Method | Endpoint | الوصف | الصلاحية |
|--------|----------|-------|----------|
| `GET`  | `/api/notifications?unread=true&page=1` | صندوق الإشعارات (Paged) | `Notifications.View` |
| `GET`  | `/api/notifications/unread-count` | عدّاد غير المقروء (للجرس) | `Notifications.View` |
| `PATCH`| `/api/notifications/{id}/read` | تعليم مقروء | `Notifications.MarkRead` |
| `PATCH`| `/api/notifications/read-all` | تعليم الكل مقروء | `Notifications.MarkRead` |
| `PUT`  | `/api/notifications/rules` | ضبط القواعد والعتبات والقنوات | `Notifications.ManageRules` |
| `POST` | `/api/notifications/broadcast` | إشعار يدوي للفريق | `Notifications.Broadcast` |

**قناة SignalR (Real-time):**

```
Hub: /hubs/notifications
Group: "tenant:{TenantId}:user:{UserId}"   ← كل مستخدم في مجموعته المعزولة
Event → client:  ReceiveNotification({ id, type, severity, title, linkUrl, createdAt })
Event → client:  UpdateUnreadCount(count)
```

> **العزل في SignalR:** أسماء المجموعات تبدأ إجبارياً بـ `tenant:{TenantId}:` — لا يمكن لعميل الاشتراك في مجموعة مستأجر آخر (يُتحقَّق من `TenantId` عند `OnConnected` من الـ JWT، لا من العميل).

---

## 10) مخطط التدفّق (Flow Chart) — من الحدث إلى الجرس

```
[حدث أو Background Job]  (مثال: بيع يخفّض رصيد حليب إلى 4 ≤ عتبة 5)
        │
        ▼
[NotificationService.Raise(LOW_STOCK, product, store)]
        │
        ▼
[Dedup?] هل يوجد إشعار مماثل ضمن 24 ساعة؟ ── نعم ──► تجاهل
        │ لا
        ▼
[INSERT Notifications]  (TenantId, StoreId, TargetRole=StoreManager, Severity=WARNING)
        │
        ├──────────────► [SignalR] Push للمجموعة tenant:{T}:user:{managers}
        │                         → 🔔 عدّاد+toast لحظي
        │
        └──────────────► [Enqueue Job] توصيل خارجي حسب Channels
                                │
                                ├─► Email (SMTP من TenantSettings)
                                └─► WhatsApp/SMS (Provider من TenantSettings)
                                        │
                                        ▼
                                [Retry عند الفشل + سجل نتيجة]
```

---

## 11) ماذا يحدث عند: القراءة / الحذف / تغيّر الحدث

| الحدث | السلوك |
|-------|--------|
| **قراءة إشعار** | `IsRead=1`, `ReadAtUtc=now` + بثّ `UpdateUnreadCount` عبر SignalR لكل أجهزة المستخدم. |
| **زوال سبب الإشعار** | مثال: إعادة تعبئة المخزون فوق العتبة → الإشعار السابق يبقى (سجل تاريخي) ولا يُولَّد جديد؛ يمكن وضع علامة "تمّت المعالجة". |
| **حذف إشعار** | Soft delete — يختفي من الصندوق ويبقى للتدقيق. |
| **تكرار الحدث** | يُمنع بـ `DedupKey` ضمن النافذة الزمنية. |
| **فشل التوصيل الخارجي** | إعادة محاولة (Hangfire Retry)؛ بعد استنفادها يُسجَّل الفشل ويبقى الإشعار In-App. |
| **انتهاء الاحتفاظ (Retention)** | Job دوري يؤرشف/ينظّف الإشعارات الأقدم من مدّة محدّدة. |

---

## 12) سجل التدقيق (Audit Log)

- **الإشعارات نفسها ليست حدث تدقيق** بل نتيجة أحداث مُدقَّقة أصلاً (حركة مخزون، فشل نسخ) — الحدث الأصلي مسجَّل في `AuditLogs`.
- **Broadcast اليدوي** يُسجَّل (من أرسل، لمن، المحتوى).
- **تغيير قواعد/عتبات الإشعارات** يُسجَّل بـ `OldValues`/`NewValues`.

---

## 13) الأخطاء المحتملة (Possible Errors)

| الكود | السبب | الرسالة |
|------|-------|---------|
| `422 UNKNOWN_NOTIFICATION_TYPE` | نوع خارج القائمة | "نوع إشعار غير معروف" |
| `422 NO_TARGET` | بلا جمهور محدّد | "يجب تحديد جمهور الإشعار" |
| `409 DUPLICATE_NOTIFICATION` | خرق `UX_Notifications_Dedup` | (يُبتلَع بصمت — منع تكرار مقصود) |
| `502 EXTERNAL_DELIVERY_FAILED` | فشل SMTP/WhatsApp | يُعاد محاولته؛ يُسجَّل ولا يُعطِّل النظام |
| `403 SIGNALR_TENANT_MISMATCH` | محاولة اشتراك بمجموعة مستأجر آخر | يُرفض الاتصال |

---

## 14) الأداء (Performance)

- عدّاد غير المقروء يُخدَم من فهرس `IX_Notifications_Tenant_User_Unread` (Covering + Filtered) — بلا مسح كامل.
- الدفع اللحظي عبر SignalR يستهدف مجموعة المستخدم فقط (لا بثّ واسع غير ضروري).
- التوصيل الخارجي **غير متزامن** (Hangfire) — لا يبطئ مسار الطلب الأصلي.
- التنظيف الدوري (Retention) يحافظ على حجم الجدول صغيراً وسريع الاستعلام.
- التوليد Event-driven أساساً؛ Jobs الدورية تعمل خارج ساعات الذروة لأنواع الفحص الثقيل (Expired/Payments).

---

## 15) الأمان (Security)

1. **عزل صارم:** كل استعلام إشعارات مفلتر بـ `TenantId` (Global Query Filter + RLS)؛ مجموعات SignalR مُسبَّقة بـ `tenant:{TenantId}:` ويُتحقَّق منها من الـ JWT عند الاتصال.
2. **الاستهداف يحترم الصلاحية:** لا يظهر إشعار يشير لكيان لا يملك المستخدم صلاحية رؤيته (Deep link محمي).
3. **تقنيع البيانات الحسّاسة:** نصّ الإشعار الخارجي (بريد/WhatsApp) لا يتضمّن أسراراً أو أرقاماً كاملة حسّاسة.
4. **حماية قنوات التوصيل:** بيانات SMTP/WhatsApp مشفّرة في الإعدادات ([23-Settings.md](23-Settings.md))، والإرسال مُقيَّد بمعدّل (Rate-limited) لمنع إساءة الاستخدام.
5. **منع التكرار = حماية من الإزعاج/الإغراق:** `DedupKey` يمنع فيضان الإشعارات (Notification Flooding).

---

_يلتزم بالمرجع الحاكم [04-Database-Design.md](04-Database-Design.md): `Channels` كـ JSON مسموح (إعداد مرن مع `ISJSON`)، وحالة القراءة/الاستهداف علائقية._
