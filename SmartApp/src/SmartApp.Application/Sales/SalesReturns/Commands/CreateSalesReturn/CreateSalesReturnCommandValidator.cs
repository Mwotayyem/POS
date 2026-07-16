using FluentValidation;

namespace SmartApp.Application.Sales.SalesReturns.Commands.CreateSalesReturn;

public sealed class CreateSalesReturnCommandValidator : AbstractValidator<CreateSalesReturnCommand>
{
    public CreateSalesReturnCommandValidator()
    {
        RuleFor(x => x.SalesInvoiceId).GreaterThan(0);
        RuleFor(x => x.Items).NotEmpty().WithMessage("يجب أن يحتوي الإرجاع على بند واحد على الأقل.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.SalesInvoiceItemId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0m);
        });
    }
}
