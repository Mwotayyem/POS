using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Stock.Commands.TransferStock;

/// <summary>
/// Transfers a quantity of a product from one warehouse to another. Generates two linked movements
/// (OUT of the source, IN to the destination) at the source's current average cost, so the transfer
/// is cost-preserving. Both legs commit in a single transaction.
/// </summary>
public sealed record TransferStockCommand(
    long ProductId,
    long FromWarehouseId,
    long ToWarehouseId,
    decimal Quantity,
    string? Reason) : IRequest<Result>;
