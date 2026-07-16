using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Purchasing.Suppliers.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.Suppliers.Queries.GetSupplierById;

/// <summary>Loads one tenant supplier by id; NOT_FOUND if it isn't the caller's.</summary>
public sealed class GetSupplierByIdQueryHandler : IRequestHandler<GetSupplierByIdQuery, Result<SupplierDto>>
{
    private readonly IApplicationDbContext _db;

    public GetSupplierByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<SupplierDto>> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        SupplierDto? supplier = await _db.Suppliers
            .Where(s => s.Id == request.SupplierId)
            .Select(s => new SupplierDto(s.Id, s.Name, s.Phone, s.Email, s.Address, s.Balance, s.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return supplier is null
            ? Result.Failure<SupplierDto>(Error.NotFound("المورّد غير موجود."))
            : Result.Success(supplier);
    }
}
