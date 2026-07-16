using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Sales.Customers.Dtos;
using SmartApp.Domain.Sales;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.Customers.Queries.GetCustomers;

/// <summary>Loads the current tenant's customers (tenant-filtered), optional name/phone search.</summary>
public sealed class GetCustomersQueryHandler
    : IRequestHandler<GetCustomersQuery, Result<IReadOnlyList<CustomerDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetCustomersQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<CustomerDto>>> Handle(
        GetCustomersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Customer> query = _db.Customers;

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string like = $"%{request.Search.Trim()}%";
            query = query.Where(c => EF.Functions.Like(c.Name, like) ||
                                     (c.Phone != null && EF.Functions.Like(c.Phone, like)));
        }

        List<CustomerDto> customers = await query
            .OrderBy(c => c.Name)
            .Select(c => new CustomerDto(
                c.Id, c.Name, c.Phone, c.Email, c.Address, c.CreditLimit, c.Balance, c.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CustomerDto>>(customers);
    }
}
