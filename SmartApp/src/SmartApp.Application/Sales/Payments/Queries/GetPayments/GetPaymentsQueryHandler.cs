using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Sales.Payments.Dtos;
using SmartApp.Domain.Sales;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.Payments.Queries.GetPayments;

/// <summary>Loads the current tenant's payments (tenant-filtered), newest first.</summary>
public sealed class GetPaymentsQueryHandler
    : IRequestHandler<GetPaymentsQuery, Result<IReadOnlyList<PaymentDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetPaymentsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<PaymentDto>>> Handle(
        GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Payment> query = _db.Payments;

        if (request.CustomerId is long customerId)
        {
            query = query.Where(p => p.CustomerId == customerId);
        }

        List<PaymentDto> payments = await query
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id)
            .Select(p => new PaymentDto(
                p.Id, p.PaymentNumber, p.CustomerId, p.SalesInvoiceId, p.Amount,
                p.PaymentDate, (byte)p.Method, p.Reference, p.Notes))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PaymentDto>>(payments);
    }
}
