using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Users.Commands.UpdateUser;

/// <summary>
/// Updates a user's display fields and reconciles its role set (adds missing, removes extra).
/// The global tenant filter ensures only the caller's own users/roles are visible, so cross-tenant
/// mutation is impossible.
/// </summary>
public sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantProvider _tenant;

    public UpdateUserCommandHandler(IApplicationDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        AppUser? user = await _db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(Error.NotFound("المستخدم غير موجود."));
        }

        var requestedRoles = request.RoleIds.Distinct().ToList();
        if (requestedRoles.Count > 0)
        {
            int found = await _db.Roles.CountAsync(r => requestedRoles.Contains(r.Id), cancellationToken);
            if (found != requestedRoles.Count)
            {
                return Result.Failure(Error.Validation(
                    "بعض الأدوار غير موجودة في هذا المستأجر.",
                    [new FieldError(nameof(UpdateUserCommand.RoleIds), "دور واحد أو أكثر غير صالح.")]));
            }
        }

        user.FullName = request.FullName.Trim();
        user.Phone = request.Phone?.Trim();

        ReconcileRoles(user, requestedRoles);

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private void ReconcileRoles(AppUser user, IReadOnlyList<long> requestedRoleIds)
    {
        HashSet<long> desired = requestedRoleIds.ToHashSet();
        HashSet<long> current = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();

        // Remove assignments no longer desired.
        List<UserRole> toRemove = user.UserRoles.Where(ur => !desired.Contains(ur.RoleId)).ToList();
        foreach (UserRole ur in toRemove)
        {
            _db.UserRoles.Remove(ur);
        }

        // Add newly desired assignments.
        foreach (long roleId in desired.Where(id => !current.Contains(id)))
        {
            _db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = roleId,
                TenantId = _tenant.CurrentTenantId,
            });
        }
    }
}
