using FluentValidation;

namespace SmartApp.Application.Profile.Commands.ChangeMyPassword;

public sealed class ChangeMyPasswordCommandValidator : AbstractValidator<ChangeMyPasswordCommand>
{
    public ChangeMyPasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty().MinimumLength(8).MaximumLength(200)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("كلمة المرور الجديدة يجب أن تختلف عن الحالية.");
    }
}
