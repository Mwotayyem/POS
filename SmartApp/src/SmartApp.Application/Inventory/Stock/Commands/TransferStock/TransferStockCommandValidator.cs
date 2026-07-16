using FluentValidation;

namespace SmartApp.Application.Inventory.Stock.Commands.TransferStock;

public sealed class TransferStockCommandValidator : AbstractValidator<TransferStockCommand>
{
    public TransferStockCommandValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.FromWarehouseId).GreaterThan(0);
        RuleFor(x => x.ToWarehouseId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0m)
            .WithMessage("كمية التحويل يجب أن تكون أكبر من صفر.");
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}
