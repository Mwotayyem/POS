using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Administration.Roles.Common;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Roles.Commands.CreateRole;

/// <summary>
/// Creates a tenant role and grants the requested permissions. Role name must be unique within the
/// tenant; permission codes must exist in the catalog. TenantId is auto-stamped on the role
/// (BaseEntity) and set explicitly on each grant (RolePermission is not a BaseEntity).
/// </summary>
public sealed class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, Result<long>>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantProvider _tenant;

    public CreateRoleCommandHandler(IApplicationDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Result<long>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        string normalizedName = request.Name.Trim().ToUpperInvariant();

        bool nameTaken = await _db.Roles
            .AnyAsync(r => r.NormalizedName == normalizedName, cancellationToken);
        if (nameTaken)
        {
            return Result.Failure<long>(Error.Conflict("اسم الدور مستخدم بالفعل."));
        }

        Result<List<Permission>> permissions =
            await PermissionResolver.ResolveAsync(_db, request.Permissions, cancellationToken);
        if (permissions.IsFailure)
        {
            return Result.Failure<long>(permissions.Error!);
        }

        var role = new AppRole
        {
            TenantId = _tenant.CurrentTenantId,
            Name = request.Name.Trim(),
            NormalizedName = normalizedName,
            Description = request.Description?.Trim(),
            IsSystemRole = false,
        };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (Permission permission in permissions.Value!)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permission.Id,
                TenantId = _tenant.CurrentTenantId,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(role.Id);
    }
}
