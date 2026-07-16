using MediatR;
using SmartApp.Application.Inventory.Stock.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Inventory.Stock.Queries.GetStockBalances;

/// <summary>
/// Lists stock balances for the current tenant, optionally filtered by warehouse and/or product.
/// </summary>
public sealed record GetStockBalancesQuery(
    long? WarehouseId = null,
    long? ProductId = null) : IRequest<Result<IReadOnlyList<StockBalanceDto>>>;
