using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Purchasing;
using SmartApp.Domain.Purchasing.Enums;
using SmartApp.Domain.Sequences;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseOrders.Commands.CreatePurchaseOrder;

/// <summary>
/// Creates a draft purchase order after validating the supplier and products. Computes each line's
/// total and the order total, and reserves an order number. No stock effect (a PO is a plan).
/// </summary>
public sealed class CreatePurchaseOrderCommandHandler
    : IRequestHandler<CreatePurchaseOrderCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDocumentNumberService _numbers;
    private readonly IDateTimeProvider _clock;

    public CreatePurchaseOrderCommandHandler(
        IApplicationDbContext db, IDocumentNumberService numbers, IDateTimeProvider clock)
    {
        _db = db;
        _numbers = numbers;
        _clock = clock;
    }

    public async Task<Result<long>> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
        {
            return Result.Failure<long>(Error.Validation(
                "المورّد غير موجود.", [new FieldError("supplierId", "معرّف مورّد غير صالح.")]));
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        int foundProducts = await _db.Products.CountAsync(p => productIds.Contains(p.Id), cancellationToken);
        if (foundProducts != productIds.Count)
        {
            return Result.Failure<long>(Error.Validation(
                "منتج واحد أو أكثر غير موجود.", [new FieldError("items", "منتج غير صالح.")]));
        }

        string number = await _numbers.NextAsync(DocumentType.PurchaseOrder, cancellationToken);

        var order = new PurchaseOrder
        {
            OrderNumber = number,
            SupplierId = request.SupplierId,
            OrderDate = request.OrderDate ?? _clock.UtcNow,
            Status = PurchaseOrderStatus.Draft,
            Notes = request.Notes?.Trim(),
        };

        decimal total = 0m;
        foreach (CreatePurchaseOrderLine line in request.Items)
        {
            decimal lineTotal = line.Quantity * line.UnitCost;
            total += lineTotal;
            order.Items.Add(new PurchaseOrderItem
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                LineTotal = lineTotal,
            });
        }

        order.Total = total;
        _db.PurchaseOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(order.Id);
    }
}
