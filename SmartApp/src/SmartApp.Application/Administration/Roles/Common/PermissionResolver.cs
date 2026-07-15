using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Identity;
using SmartApp.Shared.Constants;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Administration.Roles.Common;

/// <summary>
/// Resolves a set of permission codes to their <see cref="Permission"/> reference rows, validating
/// each code against the fixed <see cref="PermissionCatalog"/> first. Shared by role create/update
/// and permission-assignment handlers. Unknown codes produce a VALIDATION_ERROR.
/// </summary>
internal static class PermissionResolver
{
    private static readonly HashSet<string> KnownCodes =
        PermissionCatalog.AllCodes.ToHashSet(StringComparer.Ordinal);

    public static async Task<Result<List<Permission>>> ResolveAsync(
        IApplicationDbContext db, IReadOnlyList<string> codes, CancellationToken cancellationToken)
    {
        var requested = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (requested.Count == 0)
        {
            return Result.Success(new List<Permission>());
        }

        List<string> unknown = requested.Where(c => !KnownCodes.Contains(c)).ToList();
        if (unknown.Count > 0)
        {
            return Result.Failure<List<Permission>>(Error.Validation(
                "صلاحيات غير معروفة.",
                unknown.Select(c => new FieldError("permissions", $"صلاحية غير صالحة: {c}")).ToList()));
        }

        // Permission is a global reference table (no tenant filter), but IgnoreQueryFilters keeps
        // this correct regardless of ambient context.
        List<Permission> resolved = await db.Permissions
            .IgnoreQueryFilters()
            .Where(p => requested.Contains(p.Code))
            .ToListAsync(cancellationToken);

        // A catalog code with no seeded row means the catalog was not seeded — treat as internal.
        if (resolved.Count != requested.Count)
        {
            return Result.Failure<List<Permission>>(Error.Internal(
                "لم تُزرع بعض الصلاحيات في قاعدة البيانات. شغّل بذر الصلاحيات."));
        }

        return Result.Success(resolved);
    }
}
