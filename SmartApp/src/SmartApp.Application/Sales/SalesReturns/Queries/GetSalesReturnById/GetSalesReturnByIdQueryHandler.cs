using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Sales.SalesReturns.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.SalesReturns.Queries.GetSalesReturnById;

/// <summary>Loads one tenant sales return with its items; NOT_FOUND if it isn't the caller's.</summary>
public sealed class GetSalesReturnByIdQueryHandler
    : IRequestHandler<GetSalesReturnByIdQuery, Result<SalesReturnDto>>
{
    private readonly IApplicationDbContext _db;

    public GetSalesReturnByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<SalesReturnDto>> Handle(
        GetSalesReturnByIdQuery request, CancellationToken cancellationToken)
    {
        SalesReturnDto? dto = await _db.SalesReturns
            .Where(r => r.Id == request.ReturnId)
            .Select(r => new SalesReturnDto(
                r.Id,
                r.ReturnNumber,
                r.SalesInvoiceId,
                r.WarehouseId,
                r.ReturnDate,
                r.TotalAmount,
                r.Reason,
                r.Items
                    .OrderBy(x => x.Id)
                    .Select(x => new SalesReturnItemDto(
                        x.Id, x.SalesInvoiceItemId, x.ProductId, x.Quantity, x.UnitPrice, x.LineTotal))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<SalesReturnDto>(Error.NotFound("مرتجع المبيعات غير موجود."))
            : Result.Success(dto);
    }
}
