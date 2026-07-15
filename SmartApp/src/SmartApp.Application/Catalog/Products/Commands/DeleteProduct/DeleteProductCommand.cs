using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Products.Commands.DeleteProduct;

/// <summary>
/// Soft-deletes a product and its child rows (units, barcodes, prices). Per
/// SmartApp-Architecture/07-ERD-Relationships.md §4, a product with stock movements must be
/// deactivated rather than deleted; that guard is added when the Inventory module exists (Phase 8).
/// </summary>
public sealed record DeleteProductCommand(long ProductId) : IRequest<Result>;
