using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Brands.Commands.CreateBrand;

/// <summary>Creates a brand in the current tenant. Returns the new id.</summary>
public sealed record CreateBrandCommand(
    string Name,
    string? Code,
    string? Description) : IRequest<Result<long>>;
