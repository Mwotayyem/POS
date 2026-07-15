using FluentValidation;

namespace SmartApp.Application.Administration.Roles.Commands.SetRolePermissions;

public sealed class SetRolePermissionsCommandValidator : AbstractValidator<SetRolePermissionsCommand>
{
    public SetRolePermissionsCommandValidator()
    {
        RuleFor(x => x.RoleId).GreaterThan(0);
        RuleForEach(x => x.Permissions).NotEmpty().MaximumLength(100);
    }
}
