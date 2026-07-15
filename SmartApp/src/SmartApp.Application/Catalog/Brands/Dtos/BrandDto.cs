namespace SmartApp.Application.Catalog.Brands.Dtos;

/// <summary>A brand as returned by the API (greenfield entity).</summary>
public sealed record BrandDto(
    long Id,
    string Name,
    string? Code,
    string? Description,
    bool IsActive);
