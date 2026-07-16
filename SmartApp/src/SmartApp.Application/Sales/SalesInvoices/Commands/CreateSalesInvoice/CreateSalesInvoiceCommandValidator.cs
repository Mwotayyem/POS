using FluentValidation;

namespace SmartApp.Application.Sales.SalesInvoices.Commands.CreateSalesInvoice;

public sealed class CreateSalesInvoiceCommandValidator : AbstractValidator<CreateSalesInvoiceCommand>
{
    public CreateSalesInvoiceCommandValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0).When(x => x.CustomerId.HasValue);
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.PaidAmount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Items).NotEmpty().WithMessage("يجب أن تحتوي الفاتورة على بند واحد على الأقل.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0m);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0m);
            item.RuleFor(i => i.DiscountAmount).GreaterThanOrEqualTo(0m);
            item.RuleFor(i => i.TaxRate).InclusiveBetween(0m, 100m);
        });
    }
}
