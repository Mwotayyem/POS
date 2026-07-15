using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Catalog;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Products.Commands.DeleteProduct;

/// <summary>
/// Soft-deletes a product together with its child rows. The child rows are removed (soft-deleted via
/// the interceptor) so barcodes/SKU free up for reuse. When Inventory exists, a product with stock
/// movements will be blocked here and deactivated instead.
/// </summary>
public sealed class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public DeleteProductCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        Product? product = await _db.Products
            .Include(p => p.ProductUnits)
            .Include(p => p.Barcodes)
            .Include(p => p.Prices)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure(Error.NotFound("المنتج غير موجود."));
        }

        _db.ProductUnits.RemoveRange(product.ProductUnits);
        _db.ProductBarcodes.RemoveRange(product.Barcodes);
        _db.ProductPrices.RemoveRange(product.Prices);
        _db.Products.Remove(product); // Interceptor converts to soft delete.

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
