using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Purchasing;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.Suppliers.Commands.DeleteSupplier;

/// <summary>Soft-deletes a supplier after guarding against referencing purchase orders/invoices.</summary>
public sealed class DeleteSupplierCommandHandler : IRequestHandler<DeleteSupplierCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public DeleteSupplierCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        Supplier? supplier = await _db.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(Error.NotFound("المورّد غير موجود."));
        }

        bool inOrders = await _db.PurchaseOrders.AnyAsync(o => o.SupplierId == supplier.Id, cancellationToken);
        bool inInvoices = await _db.PurchaseInvoices.AnyAsync(i => i.SupplierId == supplier.Id, cancellationToken);
        if (inOrders || inInvoices)
        {
            return Result.Failure(Error.Conflict("لا يمكن حذف مورّد مرتبط بأوامر أو فواتير شراء."));
        }

        _db.Suppliers.Remove(supplier);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
