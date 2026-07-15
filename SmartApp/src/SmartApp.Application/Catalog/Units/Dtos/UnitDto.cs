namespace SmartApp.Application.Catalog.Units.Dtos;

/// <summary>A unit of measure as returned by the API. See 06-Tables-Definitions.md §3.2.</summary>
public sealed record UnitDto(
    long Id,
    string Name,
    string? Symbol,
    byte Precision,
    bool IsActive);
