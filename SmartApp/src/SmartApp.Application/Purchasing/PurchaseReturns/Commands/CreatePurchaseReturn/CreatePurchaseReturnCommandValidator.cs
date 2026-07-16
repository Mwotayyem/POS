using FluentValidation;

namespace SmartApp.Application.Purchasing.PurchaseReturns.Commands.CreatePurchaseReturn;

public sealed class CreatePurchaseReturnCommandValidator : AbstractValidator<CreatePurchaseReturnCommand>
{
    public CreatePurchaseReturnCommandValidator()
    {
        RuleFor(x => x.PurchaseInvoiceId).GreaterThan(0);
        RuleFor(x => x.Items).NotEmpty().WithMessage("يجب أن يحتوي الإرجاع على بند واحد على الأقل.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.PurchaseInvoiceItemId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0m);
        });
    }
}
