using FluentValidation;

namespace SmartApp.Application.Sales.Payments.Commands.RecordPayment;

public sealed class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.SalesInvoiceId).GreaterThan(0).When(x => x.SalesInvoiceId.HasValue);
        RuleFor(x => x.Amount).GreaterThan(0m).WithMessage("مبلغ الدفعة يجب أن يكون أكبر من صفر.");
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}
