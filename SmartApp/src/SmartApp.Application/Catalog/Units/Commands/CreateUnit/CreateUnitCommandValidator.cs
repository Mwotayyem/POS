using FluentValidation;

namespace SmartApp.Application.Catalog.Units.Commands.CreateUnit;

public sealed class CreateUnitCommandValidator : AbstractValidator<CreateUnitCommand>
{
    public CreateUnitCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Symbol).MaximumLength(20);
        RuleFor(x => x.Precision).LessThanOrEqualTo((byte)6)
            .WithMessage("الدقة يجب أن تكون بين 0 و 6.");
    }
}
