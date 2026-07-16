using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Sales;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.Customers.Commands.DeleteCustomer;

/// <summary>Soft-deletes a customer after guarding against referencing sales invoices/payments.</summary>
public sealed class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public DeleteCustomerCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        Customer? customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure(Error.NotFound("العميل غير موجود."));
        }

        bool inInvoices = await _db.SalesInvoices.AnyAsync(i => i.CustomerId == customer.Id, cancellationToken);
        bool inPayments = await _db.Payments.AnyAsync(p => p.CustomerId == customer.Id, cancellationToken);
        if (inInvoices || inPayments)
        {
            return Result.Failure(Error.Conflict("لا يمكن حذف عميل مرتبط بفواتير أو دفعات."));
        }

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
