# 26 — Deployment (النشر والتشغيل)

> هذا الملف يحكم كيفية بناء ونشر وتشغيل **Smart ERP POS** عبر البيئات. الهدف: نشر **آلي، متكرّر، قابل للتراجع (Rollback)، وبلا توقّف (Zero-Downtime)** يخدم آلاف المستأجرين من نشر واحد.

---

## 1) البيئات (Environments)

| البيئة | الغرض | قاعدة البيانات | الأسرار | من ينشر |
|--------|-------|----------------|---------|---------|
| **Development** | تطوير محلّي | SQL Server محلّي / Docker | `dotnet user-secrets` | كل مطوّر |
| **Staging** | مطابقة الإنتاج للاختبار النهائي (UAT) | نسخة معزولة تشبه الإنتاج | Key Vault (فرع Staging) | CI/CD تلقائي عند دمج `develop` |
| **Production** | التشغيل الفعلي للعملاء | SQL Server 2022 مُدار + نسخ احتياطي | Key Vault (فرع Prod) | CI/CD بموافقة يدوية عند وسم `release/*` |

**قواعد:**

- **Config as Code**: كل الفروق بين البيئات في `appsettings.{Environment}.json` + متغيّرات بيئة، لا في الكود.
- **Staging = Prod**: نفس إصدار الـ runtime، نفس بنية الـ DB، نفس الإعدادات (عدا الأسرار والمقاييس) — لضمان أن ما نجح في Staging ينجح في Prod.
- المتغيّر `ASPNETCORE_ENVIRONMENT` هو الحاكم الوحيد لاختيار الإعدادات.

---

## 2) خطّ التكامل والنشر المستمرّ (CI/CD Pipeline)

التدفّق: **Build → Test → Publish → Migrate → Deploy → Verify**. المنصّة GitLab CI (النظام مستضاف على GitLab) أو GitHub Actions بنفس المنطق.

```yaml
# .gitlab-ci.yml — مبسّط
stages: [build, test, publish, migrate, deploy, verify]
variables:
  DOTNET_VERSION: "9.0"

build:
  stage: build
  script:
    - dotnet restore
    - dotnet build -c Release --no-restore

test:
  stage: test
  script:
    - dotnet test -c Release --no-build --collect:"XPlat Code Coverage"
  coverage: '/Total\s*\|\s*(\d+(?:\.\d+)?)%/'
  # الفشل هنا يوقف الخطّ — لا نشر بلا اختبارات خضراء

publish:
  stage: publish
  script:
    - dotnet publish src/SmartPos.Api -c Release -o ./publish
    - docker build -t $REGISTRY/smartpos-api:$CI_COMMIT_SHORT_SHA .
    - docker push $REGISTRY/smartpos-api:$CI_COMMIT_SHORT_SHA
  only: [develop, /^release\/.*/]

migrate:
  stage: migrate
  script:
    # توليد سكربت SQL مُراجَع (idempotent) بدل تطبيق مباشر من التطبيق
    - dotnet ef migrations script --idempotent -o migrate.sql
    - sqlcmd -S $DB_HOST -d $DB_NAME -i migrate.sql -b
  environment: { name: staging }

deploy:
  stage: deploy
  script:
    - ./deploy.sh $CI_COMMIT_SHORT_SHA   # نشر أزرق/أخضر — انظر §8
  environment: { name: production }
  when: manual   # موافقة يدوية للإنتاج
  only: [/^release\/.*/]

verify:
  stage: verify
  script:
    - curl -fsS https://api.smartpos.com/health/ready || exit 1
```

**مبادئ الخطّ:**

- **بوّابة الجودة**: الاختبارات + التحليل الساكن (SAST) + فحص الحزم (dependency scan) يجب أن تنجح قبل النشر.
- **صورة واحدة لكل البيئات**: نبني صورة Docker مرّة واحدة (`$CI_COMMIT_SHORT_SHA`) ونرقّيها عبر البيئات — لا إعادة بناء لكل بيئة.
- الموافقة اليدوية (`when: manual`) إجبارية قبل الإنتاج.

---

## 3) استراتيجية هجرات EF Core (Migrations Strategy)

| القاعدة | القرار | السبب |
|---------|--------|-------|
| التطبيق | **سكربت SQL مُراجَع** (`--idempotent`) في مرحلة `migrate` | مراجعة بشرية + تحكّم DBA، لا مفاجآت وقت التشغيل |
| منع `Database.Migrate()` وقت الإقلاع | ممنوع في الإنتاج | لتفادي تعارض عند تعدّد النسخ (Instances) |
| التوافق للأمام | كل هجرة **متوافقة مع الإصدار السابق** (Expand/Contract) | لتمكين Zero-Downtime |
| البيانات الحسّاسة | الهجرات لا تحذف عموداً مباشرة | نمرّ بمرحلتين (انظر أدناه) |
| Multi-Tenant | الهجرات على المخطّط المشترك تنطبق على كل المستأجرين دفعة واحدة | نموذج Shared Schema (انظر [24-MultiTenant.md](24-MultiTenant.md)) |

**نمط Expand/Contract لتغيير مؤلم (مثل إعادة تسمية عمود) بلا توقّف:**

1. **Expand**: أضف العمود الجديد (nullable)، وانشر كوداً يكتب في العمودين ويقرأ من القديم.
2. **Backfill**: مهمّة خلفية (Hangfire) تملأ العمود الجديد من القديم.
3. **Switch**: انشر كوداً يقرأ من الجديد.
4. **Contract**: هجرة لاحقة تحذف العمود القديم — بعد التأكّد.

> يضمن هذا أن أيّ نسختين من التطبيق (القديمة والجديدة) تعملان على **نفس المخطّط** أثناء النشر المتدرّج.

---

## 4) الاستضافة: Kestrel + Reverse Proxy

المعمارية القياسية: **Kestrel** (خادم التطبيق) خلف **Reverse Proxy** (Nginx أو IIS) يتولّى TLS والتوجيه وموازنة الحمل.

```
[ العميل ] ──HTTPS──> [ Nginx / IIS (TLS termination) ] ──HTTP──> [ Kestrel × N instances ]
```

**Nginx (Linux/Container) — نموذج:**

```nginx
upstream smartpos {
    server app1:8080;
    server app2:8080;   # موازنة حمل بين نسختين
    keepalive 64;
}
server {
    listen 443 ssl http2;
    server_name api.smartpos.com;
    ssl_certificate     /etc/ssl/smartpos.crt;
    ssl_certificate_key /etc/ssl/smartpos.key;

    location / {
        proxy_pass http://smartpos;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
    location /health { proxy_pass http://smartpos; access_log off; }
}
```

**IIS (Windows) — نموذج:** موديول `ASP.NET Core Module V2` بنمط `OutOfProcess`/`InProcess`، مع `web.config` يوجّه إلى Kestrel. يُفعّل `ForwardedHeaders` في التطبيق ليقرأ IP و Scheme الحقيقيين خلف الـ proxy:

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

---

## 5) الحاويات (Docker)

**Dockerfile — متعدّد المراحل (Multi-stage) لصورة صغيرة آمنة:**

```dockerfile
# ===== مرحلة البناء =====
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY *.sln .
COPY src/SmartPos.Api/*.csproj src/SmartPos.Api/
RUN dotnet restore
COPY . .
RUN dotnet publish src/SmartPos.Api -c Release -o /app /p:UseAppHost=false

# ===== مرحلة التشغيل =====
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
# مستخدم غير جذري (Least Privilege)
RUN adduser --disabled-password --gecos "" appuser
USER appuser
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD curl -fsS http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "SmartPos.Api.dll"]
```

**docker-compose.yml — بيئة كاملة (API + SQL Server + Redis + Seq):**

```yaml
services:
  api:
    image: ${REGISTRY}/smartpos-api:${TAG:-latest}
    depends_on: { db: { condition: service_healthy } }
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Server=db;Database=SmartPos;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True"
      ConnectionStrings__Redis: "redis:6379"
      Serilog__WriteTo__1__Args__serverUrl: "http://seq:5341"
    ports: ["8080:8080"]
    deploy: { replicas: 2 }   # نسختان لموازنة الحمل و Zero-Downtime

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      SA_PASSWORD: ${SA_PASSWORD}
      MSSQL_PID: Standard
    volumes: [ "mssql-data:/var/opt/mssql" ]
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$$SA_PASSWORD" -Q "SELECT 1"
      interval: 10s
      retries: 10

  redis:
    image: redis:7-alpine
    volumes: [ "redis-data:/data" ]

  seq:
    image: datalust/seq:latest
    environment: { ACCEPT_EULA: "Y" }
    ports: ["5341:80"]
    volumes: [ "seq-data:/data" ]

volumes: { mssql-data: {}, redis-data: {}, seq-data: {} }
```

---

## 6) متغيّرات البيئة والأسرار (Configuration & Secrets)

| النوع | Development | Staging/Production |
|-------|-------------|--------------------|
| الإعدادات غير الحسّاسة | `appsettings.{Env}.json` | نفسها + Environment Variables |
| الأسرار (كلمات مرور DB، مفاتيح JWT، مفاتيح API) | `dotnet user-secrets` | **Azure Key Vault** / **Docker Secrets** / **HashiCorp Vault** |

**قواعد صارمة:**

- **ممنوع منعاً باتاً** وجود سرّ في مصدر الكود أو في `appsettings.json` المُودَع في Git.
- الأسرار تُحقَن كمتغيّرات بيئة أو تُقرأ من Key Vault عند الإقلاع.
- سرّ توقيع الـ JWT ومفتاح تشفير البيانات الحسّاسة يُدوَّران دورياً (Key Rotation) — انظر [27-Security.md](27-Security.md).
- ربط الإعدادات بأنماط قوية (Strongly-typed Options) مع `ValidateOnStart` لرفض الإقلاع عند نقص إعداد حرج.

```csharp
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .Validate(o => o.Secret.Length >= 32, "JWT secret too short")
    .ValidateOnStart();   // يفشل الإقلاع فوراً إذا نقص سرّ حرج
```

سلسلة الاتّصال بالتنسيق البيئي: `ConnectionStrings__Default` (الشرطتان `__` تُترجَمان إلى تسلسل هرمي في .NET).

---

## 7) فحوصات الصحّة (Health Checks)

ثلاث نقاط قياسية عبر `Microsoft.Extensions.Diagnostics.HealthChecks`:

| المسار | النوع | الغرض |
|--------|------|-------|
| `/health/live` | Liveness | هل العملية حيّة؟ (لإعادة تشغيل الحاوية) |
| `/health/ready` | Readiness | هل جاهزة لاستقبال حركة؟ (DB + Redis متاحان) |
| `/health/startup` | Startup | اكتمال الإقلاع (تحميل الإعدادات، الهجرات مطبَّقة) |

```csharp
builder.Services.AddHealthChecks()
    .AddSqlServer(cs, name: "sql", tags: ["ready"])
    .AddRedis(redisCs, name: "redis", tags: ["ready"]);

app.MapHealthChecks("/health/live",  new() { Predicate = _ => false });          // حيّ فقط
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });
```

- موازِن الحمل (Nginx/IIS/K8s) يعتمد `/health/ready` لسحب النسخة من الدوران قبل تحديثها.
- `/health/*` مُعفاة من Rate Limiting والمصادقة، لكن غير مكشوفة علناً بتفاصيل حسّاسة.

---

## 8) نشر بلا توقّف (Zero-Downtime Deployment)

**النمط المعتمد: Blue-Green (أو Rolling عند Kubernetes).**

```
1) الإصدار الحالي (Blue) يخدم الحركة.
2) نُطلق النسخة الجديدة (Green) بجانبه، مع الهجرات المتوافقة للأمام مطبَّقة مسبقاً.
3) Green يجتاز /health/ready ⇒ نحوّل الحركة تدريجياً إليه عبر الـ proxy.
4) نراقب المقاييس والأخطاء (نافذة تحقّق).
5) نجاح ⇒ نُطفئ Blue. فشل ⇒ نُعيد الحركة إلى Blue فوراً (Rollback لحظي).
```

**شروط تحقّقه:**

- الهجرات **Expand/Contract** (§3) بحيث يعمل الإصداران على نفس المخطّط.
- التطبيق **Stateless** (لا جلسات محلّية) — الحالة في DB/Redis (انظر [25-API-Design.md](25-API-Design.md)).
- إيقاف رشيق (Graceful Shutdown): `IHostApplicationLifetime` ينهي الطلبات الجارية قبل إطفاء النسخة، مع `ShutdownTimeout` مناسب.

```csharp
builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false);
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));
```

---

## 9) التراجع (Rollback)

| الطبقة | استراتيجية التراجع |
|--------|--------------------|
| التطبيق | إعادة توجيه الـ proxy إلى النسخة السابقة (Blue) أو نشر الوسم/الصورة السابقة (`:previous-sha`) |
| قاعدة البيانات | **لا نتراجع بحذف عمود** — بفضل Expand/Contract يبقى المخطّط متوافقاً؛ عند الضرورة القصوى نستعيد Point-in-Time (انظر [28-Backup-And-Restore.md](28-Backup-And-Restore.md)) |
| الإعدادات | إصدارات الإعدادات مُدارة (Git/Key Vault versions) وقابلة للاسترجاع |

**قاعدة:** كل نشر إنتاجي يجب أن يكون **قابلاً للتراجع خلال دقائق** دون فقد بيانات. الهجرات غير المتوافقة للخلف ممنوعة إلا عبر نافذة صيانة معلنة.

---

## 10) بعد النشر (Post-Deployment)

- **Smoke Tests**: مرحلة `verify` تضرب `/health/ready` ومسارات حرجة (تسجيل دخول، إنشاء فاتورة تجريبية على مستأجر اختبار).
- **المراقبة**: APM (Application Insights/OpenTelemetry) + Serilog → Seq/Elasticsearch؛ تنبيهات على معدّل الأخطاء `5xx` وزمن الاستجابة (انظر [29-Performance.md](29-Performance.md)).
- **إشعار**: نجاح/فشل النشر يُرسَل إلى قناة الفريق (Slack/Email).

---

## 11) قائمة تدقيق النشر (Deployment Checklist)

- [ ] الاختبارات + SAST + فحص الحزم خضراء في CI.
- [ ] صورة Docker واحدة موسومة بـ SHA ومُرقّاة عبر البيئات.
- [ ] هجرات EF متوافقة للأمام (Expand/Contract) ومُطبَّقة كسكربت مُراجَع.
- [ ] الأسرار من Key Vault/Secrets لا من الكود.
- [ ] `/health/ready` يعتمد عليه الـ proxy قبل تحويل الحركة.
- [ ] نشر Blue-Green مع Graceful Shutdown.
- [ ] خطّة Rollback جاهزة وقابلة للتنفيذ خلال دقائق.
- [ ] Smoke tests + مراقبة + إشعار الفريق بعد النشر.

---

_يلتزم كل إجراء نشر بهذا المرجع. أي انحراف يُوثَّق في سجلّ التغييرات._
