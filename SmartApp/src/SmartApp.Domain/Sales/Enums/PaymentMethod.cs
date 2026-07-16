namespace SmartApp.Domain.Sales.Enums;

/// <summary>
/// How a customer payment was made. This is a commercial debt-settlement descriptor, NOT a payment
/// gateway (06-Tables-Definitions.md §5.3). Stored as TINYINT.
/// </summary>
public enum PaymentMethod : byte
{
    Cash = 1,
    Transfer = 2,
    Card = 3,
}
