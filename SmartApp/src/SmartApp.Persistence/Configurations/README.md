# Entity Configurations

هنا توضَع `IEntityTypeConfiguration<T>` لكل كيان (Fluent API) — **واحدة لكل كيان**، مجمّعة حسب الوحدة (Tenancy / Identity / Catalog / ...).

- تُطبَّق تلقائياً عبر `modelBuilder.ApplyConfigurationsFromAssembly(...)` في [`AppDbContext`](../Context/AppDbContext.cs).
- كل كيان يرث `BaseEntity` يحصل تلقائياً على **Global Query Filter** (عزل المستأجر + Soft Delete).
- القواعد الحاكمة: [05-Database-Design.md](../../../../SmartApp-Architecture/05-Database-Design.md).

> **Phase 2:** بنية فقط — لا configurations بعد (تُضاف مع كل كيان في المراحل التالية، بدءاً من `Tenant` في Phase التالية).

مثال البنية المستقبلية:
```
Configurations/
├── Tenancy/    TenantConfiguration.cs · TenantSettingConfiguration.cs
├── Identity/   UserConfiguration.cs · RoleConfiguration.cs · ...
├── Catalog/    ProductConfiguration.cs · CategoryConfiguration.cs · ...
└── ...
```
