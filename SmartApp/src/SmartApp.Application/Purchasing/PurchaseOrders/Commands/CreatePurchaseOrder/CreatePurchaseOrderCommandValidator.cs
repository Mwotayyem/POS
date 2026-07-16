using FluentValidation;

namespace SmartApp.Application.Purchasing.PurchaseOrders.Commands.CreatePurchaseOrder;

public sealed class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.SupplierId).GreaterThan(0);
        RuleFor(x => x.Items).NotEmpty().WithMessage("يجب أن يحتوي الأمر على بند واحد على الأقل.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0m);
            item.RuleFor(i => i.UnitCost).GreaterThanOrEqualTo(0m);
        });
    }
}
