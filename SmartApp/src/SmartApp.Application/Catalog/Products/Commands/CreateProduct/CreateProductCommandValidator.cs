using FluentValidation;

namespace SmartApp.Application.Catalog.Products.Commands.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Sku).MaximumLength(60);
        RuleFor(x => x.BaseUnitId).GreaterThan(0);
        RuleFor(x => x.CategoryId).GreaterThan(0).When(x => x.CategoryId.HasValue);
        RuleFor(x => x.BrandId).GreaterThan(0).When(x => x.BrandId.HasValue);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SalePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxRate).InclusiveBetween(0m, 100m);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
    }
}
