using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Application.Sales.Customers.Dtos;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.Customers.Queries.GetCustomerById;

/// <summary>Loads one tenant customer by id; NOT_FOUND if it isn't the caller's.</summary>
public sealed class GetCustomerByIdQueryHandler : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    private readonly IApplicationDbContext _db;

    public GetCustomerByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<CustomerDto>> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        CustomerDto? customer = await _db.Customers
            .Where(c => c.Id == request.CustomerId)
            .Select(c => new CustomerDto(
                c.Id, c.Name, c.Phone, c.Email, c.Address, c.CreditLimit, c.Balance, c.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return customer is null
            ? Result.Failure<CustomerDto>(Error.NotFound("العميل غير موجود."))
            : Result.Success(customer);
    }
}
