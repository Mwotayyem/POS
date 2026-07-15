using MediatR;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Catalog.Brands.Commands.DeleteBrand;

/// <summary>Soft-deletes a brand. Refused if any product references it.</summary>
public sealed record DeleteBrandCommand(long BrandId) : IRequest<Result>;
