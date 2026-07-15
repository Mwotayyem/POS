using SmartApp.Domain.Common;

namespace SmartApp.Domain.Tenancy;

/// <summary>
/// Per-tenant configuration (currency, tax, timezone, locale, theme). This IS tenant-owned, so it
/// inherits <see cref="BaseEntity"/> and participates in the global tenant query filter — one row
/// per tenant. Part of the Tenant Core. See SmartApp-Architecture/06-Tables-Definitions.md §1.2.
/// </summary>
public sealed class TenantSetting : BaseEntity
{
    public string Currency { get; set; } = "SAR";
    public string TimeZone { get; set; } = "UTC";
    public decimal DefaultTaxRate { get; set; }
    public string Locale { get; set; } = "ar";

    /// <summary>Optional theme JSON (validated via ISJSON at the DB level).</summary>
    public string? ThemeJson { get; set; }
}
