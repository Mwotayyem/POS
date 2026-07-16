using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Sales;
using SmartApp.Domain.Sales.Enums;
using SmartApp.Domain.Sequences;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.Payments.Commands.RecordPayment;

/// <summary>
/// Records a payment atomically: validates the customer (and optional invoice belongs to that
/// customer), reduces the customer's balance by the amount, bumps the invoice paid amount when
/// linked, and reserves a payment number.
/// </summary>
public sealed class RecordPaymentCommandHandler : IRequestHandler<RecordPaymentCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDocumentNumberService _numbers;
    private readonly IDateTimeProvider _clock;

    public RecordPaymentCommandHandler(
        IApplicationDbContext db, IDocumentNumberService numbers, IDateTimeProvider clock)
    {
        _db = db;
        _numbers = numbers;
        _clock = clock;
    }

    public async Task<Result<long>> Handle(RecordPaymentCommand request, CancellationToken cancellationToken)
    {
        Customer? customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure<long>(Error.Validation(
                "العميل غير موجود.", [new FieldError("customerId", "معرّف عميل غير صالح.")]));
        }

        if (!Enum.IsDefined(typeof(PaymentMethod), request.Method))
        {
            return Result.Failure<long>(Error.Validation(
                "طريقة دفع غير معروفة.", [new FieldError("method", "طريقة دفع غير صالحة.")]));
        }

        SalesInvoice? invoice = null;
        if (request.SalesInvoiceId is long invoiceId)
        {
            invoice = await _db.SalesInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
            if (invoice is null || invoice.CustomerId != customer.Id)
            {
                return Result.Failure<long>(Error.Validation(
                    "الفاتورة غير مرتبطة بهذا العميل.",
                    [new FieldError("salesInvoiceId", "فاتورة غير صالحة لهذا العميل.")]));
            }
        }

        string number = await _numbers.NextAsync(DocumentType.Payment, cancellationToken);

        var payment = new Payment
        {
            PaymentNumber = number,
            CustomerId = customer.Id,
            SalesInvoiceId = request.SalesInvoiceId,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate ?? _clock.UtcNow,
            Method = (PaymentMethod)request.Method,
            Reference = request.Reference?.Trim(),
            Notes = request.Notes?.Trim(),
        };
        _db.Payments.Add(payment);

        customer.Balance -= request.Amount;
        if (invoice is not null)
        {
            invoice.PaidAmount += request.Amount;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(payment.Id);
    }
}
