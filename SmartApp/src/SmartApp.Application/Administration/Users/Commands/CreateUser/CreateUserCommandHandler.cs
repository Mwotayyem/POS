using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Users.Commands.CreateUser;

/// <summary>
/// Creates a tenant user with a hashed password and assigns the requested roles.
///
/// <para>TenantId is set explicitly from <see cref="ITenantProvider.CurrentTenantId"/> because
/// <see cref="AppUser"/> / <see cref="UserRole"/> are not <c>BaseEntity</c> types and are therefore
/// not auto-stamped. Role ids are validated to belong to the caller's tenant (the global filter
/// makes other tenants' roles invisible), so cross-tenant assignment is impossible.</para>
/// </summary>
public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantProvider _tenant;

    public CreateUserCommandHandler(
        IApplicationDbContext db, IPasswordHasher passwordHasher, ITenantProvider tenant)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tenant = tenant;
    }

    public async Task<Result<long>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        long tenantId = _tenant.CurrentTenantId;
        string normalizedEmail = request.Email.Trim().ToUpperInvariant();

        bool emailTaken = await _db.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        if (emailTaken)
        {
            return Result.Failure<long>(Error.Conflict("البريد الإلكتروني مستخدم بالفعل."));
        }

        Result rolesValid = await ValidateRolesAsync(request.RoleIds, cancellationToken);
        if (rolesValid.IsFailure)
        {
            return Result.Failure<long>(rolesValid.Error!);
        }

        var user = new AppUser
        {
            TenantId = tenantId,
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Phone = request.Phone?.Trim(),
            IsActive = true,
        };
        _db.Users.Add(user);

        // Persist the user first so its identity id is available for the join rows.
        await _db.SaveChangesAsync(cancellationToken);

        foreach (long roleId in request.RoleIds.Distinct())
        {
            _db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = roleId,
                TenantId = tenantId,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(user.Id);
    }

    private async Task<Result> ValidateRolesAsync(
        IReadOnlyList<long> roleIds, CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return Result.Success();
        }

        var distinct = roleIds.Distinct().ToList();
        int found = await _db.Roles.CountAsync(r => distinct.Contains(r.Id), cancellationToken);
        return found == distinct.Count
            ? Result.Success()
            : Result.Failure(Error.Validation(
                "بعض الأدوار غير موجودة في هذا المستأجر.",
                [new FieldError(nameof(CreateUserCommand.RoleIds), "دور واحد أو أكثر غير صالح.")]));
    }
}
