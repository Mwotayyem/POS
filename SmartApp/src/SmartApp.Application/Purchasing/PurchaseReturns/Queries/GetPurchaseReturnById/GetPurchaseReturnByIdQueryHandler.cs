using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Purchasing.PurchaseReturns.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseReturns.Queries.GetPurchaseReturnById;

/// <summary>Loads one tenant purchase return with its items; NOT_FOUND if it isn't the caller's.</summary>
public sealed class GetPurchaseReturnByIdQueryHandler
    : IRequestHandler<GetPurchaseReturnByIdQuery, Result<PurchaseReturnDto>>
{
    private readonly IApplicationDbContext _db;

    public GetPurchaseReturnByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PurchaseReturnDto>> Handle(
        GetPurchaseReturnByIdQuery request, CancellationToken cancellationToken)
    {
        PurchaseReturnDto? dto = await _db.PurchaseReturns
            .Where(r => r.Id == request.ReturnId)
            .Select(r => new PurchaseReturnDto(
                r.Id,
                r.ReturnNumber,
                r.PurchaseInvoiceId,
                r.WarehouseId,
                r.ReturnDate,
                r.TotalAmount,
                r.Reason,
                r.Items
                    .OrderBy(x => x.Id)
                    .Select(x => new PurchaseReturnItemDto(
                        x.Id, x.PurchaseInvoiceItemId, x.ProductId, x.Quantity, x.UnitPrice, x.LineTotal))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<PurchaseReturnDto>(Error.NotFound("مرتجع الشراء غير موجود."))
            : Result.Success(dto);
    }
}
