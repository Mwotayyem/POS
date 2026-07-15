using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Roles.Commands.DeleteRole;

/// <summary>
/// Soft-deletes a role after guarding: system roles are protected, and a role still assigned to any
/// user is refused (remove the assignments first). The role's permission grants are removed so no
/// stale grants remain. Soft delete is applied by the audit interceptor.
/// </summary>
public sealed class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public DeleteRoleCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        AppRole? role = await _db.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(Error.NotFound("الدور غير موجود."));
        }

        if (role.IsSystemRole)
        {
            return Result.Failure(Error.Conflict("لا يمكن حذف دور نظامي."));
        }

        bool inUse = await _db.UserRoles.AnyAsync(ur => ur.RoleId == role.Id, cancellationToken);
        if (inUse)
        {
            return Result.Failure(Error.Conflict(
                "لا يمكن حذف الدور لأنه مُسنَد إلى مستخدمين. أزل الإسناد أولاً."));
        }

        // Remove the role's permission grants, then soft-delete the role itself.
        List<RolePermission> grants = await _db.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync(cancellationToken);
        _db.RolePermissions.RemoveRange(grants);

        _db.Roles.Remove(role); // Interceptor converts this to a soft delete.

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
