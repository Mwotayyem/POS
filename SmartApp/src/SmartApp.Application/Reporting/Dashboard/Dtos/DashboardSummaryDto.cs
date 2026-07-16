namespace SmartApp.Application.Reporting.Dashboard.Dtos;

/// <summary>
/// A snapshot of the tenant's business activity over a date range: sales, purchases, gross profit,
/// receivable/payable totals, document counts, and the number of products below their reorder level.
/// All figures are tenant-scoped. Cancelled documents are excluded.
/// </summary>
public sealed record DashboardSummaryDto(
    DateTime FromUtc,
    DateTime ToUtc,
    decimal SalesTotal,
    decimal PurchasesTotal,
    decimal GrossProfit,
    decimal OutstandingReceivables,
    decimal OutstandingPayables,
    int SalesInvoiceCount,
    int PurchaseInvoiceCount,
    int LowStockProductCount);
