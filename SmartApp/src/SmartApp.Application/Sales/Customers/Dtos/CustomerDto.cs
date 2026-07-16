namespace SmartApp.Application.Sales.Customers.Dtos;

/// <summary>A customer as returned by the API. Balance positive = the customer owes the tenant.</summary>
public sealed record CustomerDto(
    long Id,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    decimal CreditLimit,
    decimal Balance,
    bool IsActive);
