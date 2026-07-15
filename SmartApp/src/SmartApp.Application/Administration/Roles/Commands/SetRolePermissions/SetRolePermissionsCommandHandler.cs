using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Administration.Roles.Common;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Roles.Commands.SetRolePermissions;

/// <summary>
/// Reconciles a role's granted permissions to exactly the requested set. Validates the role belongs
/// to the caller's tenant and every code exists in the catalog, then adds/removes grants to match.
/// TenantId is set explicitly on new grants (RolePermission is not a BaseEntity).
/// </summary>
public sealed class SetRolePermissionsCommandHandler
    : IRequestHandler<SetRolePermissionsCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantProvider _tenant;

    public SetRolePermissionsCommandHandler(IApplicationDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Result> Handle(SetRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        AppRole? role = await _db.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(Error.NotFound("الدور غير موجود."));
        }

        Result<List<Permission>> resolved =
            await PermissionResolver.ResolveAsync(_db, request.Permissions, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure(resolved.Error!);
        }

        HashSet<int> desired = resolved.Value!.Select(p => p.Id).ToHashSet();

        List<RolePermission> current = await _db.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync(cancellationToken);
        HashSet<int> currentIds = current.Select(rp => rp.PermissionId).ToHashSet();

        // Remove grants not desired.
        foreach (RolePermission grant in current.Where(rp => !desired.Contains(rp.PermissionId)))
        {
            _db.RolePermissions.Remove(grant);
        }

        // Add newly desired grants.
        foreach (int permissionId in desired.Where(id => !currentIds.Contains(id)))
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId,
                TenantId = _tenant.CurrentTenantId,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
