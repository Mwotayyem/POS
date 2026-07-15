using FluentValidation;

namespace SmartApp.Application.Administration.Roles.Commands.CreateRole;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(300);
        RuleForEach(x => x.Permissions).NotEmpty().MaximumLength(100);
    }
}
