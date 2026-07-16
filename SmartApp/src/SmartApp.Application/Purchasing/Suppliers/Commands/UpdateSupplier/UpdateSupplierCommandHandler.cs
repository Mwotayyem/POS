using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Purchasing;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.Suppliers.Commands.UpdateSupplier;

/// <summary>Updates a supplier, enforcing name uniqueness within the tenant (excluding itself).</summary>
public sealed class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public UpdateSupplierCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        Supplier? supplier = await _db.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(Error.NotFound("المورّد غير موجود."));
        }

        string name = request.Name.Trim();
        bool nameTaken = await _db.Suppliers
            .AnyAsync(s => s.Id != supplier.Id && s.Name == name, cancellationToken);
        if (nameTaken)
        {
            return Result.Failure(Error.Conflict("اسم المورّد مستخدم بالفعل."));
        }

        supplier.Name = name;
        supplier.Phone = request.Phone?.Trim();
        supplier.Email = request.Email?.Trim();
        supplier.Address = request.Address?.Trim();
        supplier.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
