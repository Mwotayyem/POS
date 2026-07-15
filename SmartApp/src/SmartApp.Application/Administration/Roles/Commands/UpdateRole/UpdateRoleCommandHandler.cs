using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Roles.Commands.UpdateRole;

/// <summary>
/// Renames a role / updates its description. Rejects renaming a system role and enforces name
/// uniqueness within the tenant (excluding the role itself).
/// </summary>
public sealed class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public UpdateRoleCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        AppRole? role = await _db.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(Error.NotFound("الدور غير موجود."));
        }

        if (role.IsSystemRole)
        {
            return Result.Failure(Error.Conflict("لا يمكن تعديل دور نظامي."));
        }

        string normalizedName = request.Name.Trim().ToUpperInvariant();

        bool nameTaken = await _db.Roles
            .AnyAsync(r => r.Id != role.Id && r.NormalizedName == normalizedName, cancellationToken);
        if (nameTaken)
        {
            return Result.Failure(Error.Conflict("اسم الدور مستخدم بالفعل."));
        }

        role.Name = request.Name.Trim();
        role.NormalizedName = normalizedName;
        role.Description = request.Description?.Trim();

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
