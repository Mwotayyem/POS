using SmartApp.Domain.Common;
using SmartApp.Domain.Sales.Enums;

namespace SmartApp.Domain.Sales;

/// <summary>
/// A customer payment — a commercial debt settlement that reduces the customer's balance. Optionally
/// linked to a specific invoice. Tenant-owned. Mirrors SmartApp-Architecture/06-Tables-Definitions.md
/// §5.3 (CustomerPayments). NOT a payment gateway.
/// </summary>
public sealed class Payment : BaseEntity
{
    /// <summary>Sequential document number (e.g. PAY-000001). Unique per tenant.</summary>
    public string PaymentNumber { get; set; } = string.Empty;

    public long CustomerId { get; set; }

    /// <summary>Optional invoice this payment is applied against.</summary>
    public long? SalesInvoiceId { get; set; }

    /// <summary>Amount paid. DECIMAL(18,4).</summary>
    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    public string? Reference { get; set; }
    public string? Notes { get; set; }

    // ---- Navigations ----
    public Customer? Customer { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }
}
