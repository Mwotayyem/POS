using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Purchasing;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.Suppliers.Commands.CreateSupplier;

/// <summary>Creates a supplier; name must be unique within the tenant. TenantId is auto-stamped.</summary>
public sealed class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;

    public CreateSupplierCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        string name = request.Name.Trim();
        if (await _db.Suppliers.AnyAsync(s => s.Name == name, cancellationToken))
        {
            return Result.Failure<long>(Error.Conflict("اسم المورّد مستخدم بالفعل."));
        }

        var supplier = new Supplier
        {
            Name = name,
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            Balance = 0m,
            IsActive = true,
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(supplier.Id);
    }
}
