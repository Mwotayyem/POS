namespace SmartApp.Domain.Catalog.Enums;

/// <summary>
/// The kind of price a <see cref="ProductPrice"/> row represents. A product may carry at most one
/// price per type. Stored as its numeric value (TINYINT). Greenfield — not in the architecture docs.
/// </summary>
public enum PriceType : byte
{
    /// <summary>Standard retail price.</summary>
    Retail = 1,

    /// <summary>Wholesale price.</summary>
    Wholesale = 2,

    /// <summary>Distributor price.</summary>
    Distributor = 3,

    /// <summary>Online / e-commerce price.</summary>
    Online = 4,
}
