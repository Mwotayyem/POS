using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Sales;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.Customers.Commands.CreateCustomer;

/// <summary>Creates a customer; name must be unique within the tenant. TenantId is auto-stamped.</summary>
public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;

    public CreateCustomerCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        string name = request.Name.Trim();
        if (await _db.Customers.AnyAsync(c => c.Name == name, cancellationToken))
        {
            return Result.Failure<long>(Error.Conflict("اسم العميل مستخدم بالفعل."));
        }

        var customer = new Customer
        {
            Name = name,
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            CreditLimit = request.CreditLimit,
            Balance = 0m,
            IsActive = true,
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(customer.Id);
    }
}
