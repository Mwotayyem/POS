using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Purchasing.Suppliers.Dtos;
using SmartApp.Domain.Purchasing;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.Suppliers.Queries.GetSuppliers;

/// <summary>Loads the current tenant's suppliers (tenant-filtered), optional name/phone search.</summary>
public sealed class GetSuppliersQueryHandler
    : IRequestHandler<GetSuppliersQuery, Result<IReadOnlyList<SupplierDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetSuppliersQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<SupplierDto>>> Handle(
        GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Supplier> query = _db.Suppliers;

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string like = $"%{request.Search.Trim()}%";
            query = query.Where(s => EF.Functions.Like(s.Name, like) ||
                                     (s.Phone != null && EF.Functions.Like(s.Phone, like)));
        }

        List<SupplierDto> suppliers = await query
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto(s.Id, s.Name, s.Phone, s.Email, s.Address, s.Balance, s.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SupplierDto>>(suppliers);
    }
}
