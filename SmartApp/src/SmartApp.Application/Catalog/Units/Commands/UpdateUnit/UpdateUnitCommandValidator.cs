using FluentValidation;

namespace SmartApp.Application.Catalog.Units.Commands.UpdateUnit;

public sealed class UpdateUnitCommandValidator : AbstractValidator<UpdateUnitCommand>
{
    public UpdateUnitCommandValidator()
    {
        RuleFor(x => x.UnitId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Symbol).MaximumLength(20);
        RuleFor(x => x.Precision).LessThanOrEqualTo((byte)6)
            .WithMessage("الدقة يجب أن تكون بين 0 و 6.");
    }
}
