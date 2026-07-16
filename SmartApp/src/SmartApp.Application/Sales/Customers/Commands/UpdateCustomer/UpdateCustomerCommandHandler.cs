using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Sales;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Sales.Customers.Commands.UpdateCustomer;

/// <summary>Updates a customer, enforcing name uniqueness within the tenant (excluding itself).</summary>
public sealed class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public UpdateCustomerCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        Customer? customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure(Error.NotFound("العميل غير موجود."));
        }

        string name = request.Name.Trim();
        bool nameTaken = await _db.Customers
            .AnyAsync(c => c.Id != customer.Id && c.Name == name, cancellationToken);
        if (nameTaken)
        {
            return Result.Failure(Error.Conflict("اسم العميل مستخدم بالفعل."));
        }

        customer.Name = name;
        customer.Phone = request.Phone?.Trim();
        customer.Email = request.Email?.Trim();
        customer.Address = request.Address?.Trim();
        customer.CreditLimit = request.CreditLimit;
        customer.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
