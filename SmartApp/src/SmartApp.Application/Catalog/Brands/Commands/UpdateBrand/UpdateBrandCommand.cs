using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Brands.Commands.UpdateBrand;

/// <summary>Updates a brand. Name stays unique within the tenant.</summary>
public sealed record UpdateBrandCommand(
    long BrandId,
    string Name,
    string? Code,
    string? Description,
    bool IsActive) : IRequest<Result>;
