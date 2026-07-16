namespace SmartApp.Application.Inventory.Warehouses.Dtos;

/// <summary>A warehouse as returned by the API.</summary>
public sealed record WarehouseDto(
    long Id,
    string Name,
    string? Code,
    string? Address,
    bool IsDefault,
    bool IsActive);
