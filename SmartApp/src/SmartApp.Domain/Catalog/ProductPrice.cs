using SmartApp.Domain.Catalog.Enums;
using SmartApp.Domain.Common;

namespace SmartApp.Domain.Catalog;

/// <summary>
/// A typed price for a product (retail / wholesale / distributor / online). Enables multiple prices
/// per product by <see cref="PriceType"/>, in addition to the product's inline default
/// <see cref="Product.SalePrice"/>. Tenant-owned. Greenfield — not in the architecture docs; follows
/// the same conventions (DECIMAL(18,4), soft-delete, NO ACTION FKs). At most one active price per
/// (product, type), enforced by a filtered unique index.
/// </summary>
public sealed class ProductPrice : BaseEntity
{
    public long ProductId { get; set; }

    public PriceType PriceType { get; set; }

    /// <summary>The price amount. DECIMAL(18,4), &gt;= 0.</summary>
    public decimal Amount { get; set; }

    // ---- Navigations ----
    public Product? Product { get; set; }
}
