using FluentValidation;

namespace SmartApp.Application.Inventory.Stock.Commands.AdjustStock;

public sealed class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.QuantityChange).NotEqual(0m)
            .WithMessage("كمية التسوية يجب أن تكون غير صفرية.");
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}
