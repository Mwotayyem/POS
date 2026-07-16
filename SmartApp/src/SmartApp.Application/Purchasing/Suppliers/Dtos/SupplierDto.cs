namespace SmartApp.Application.Purchasing.Suppliers.Dtos;

/// <summary>A supplier as returned by the API. Balance positive = the tenant owes the supplier.</summary>
public sealed record SupplierDto(
    long Id,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    decimal Balance,
    bool IsActive);
