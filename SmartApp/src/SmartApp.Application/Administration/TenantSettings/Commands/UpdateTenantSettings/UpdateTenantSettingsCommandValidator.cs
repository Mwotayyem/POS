using System.Text.Json;
using FluentValidation;

namespace SmartApp.Application.Administration.TenantSettings.Commands.UpdateTenantSettings;

public sealed class UpdateTenantSettingsCommandValidator : AbstractValidator<UpdateTenantSettingsCommand>
{
    public UpdateTenantSettingsCommandValidator()
    {
        RuleFor(x => x.Currency)
            .NotEmpty().Length(3).WithMessage("رمز العملة يجب أن يكون 3 أحرف (ISO 4217).");

        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(100);

        RuleFor(x => x.DefaultTaxRate)
            .InclusiveBetween(0m, 100m).WithMessage("نسبة الضريبة يجب أن تكون بين 0 و 100.");

        RuleFor(x => x.Locale).NotEmpty().MaximumLength(10);

        RuleFor(x => x.ThemeJson)
            .Must(BeValidJson).When(x => !string.IsNullOrWhiteSpace(x.ThemeJson))
            .WithMessage("قيمة ThemeJson ليست JSON صالحًا.");
    }

    private static bool BeValidJson(string? value)
    {
        try
        {
            using JsonDocument _ = JsonDocument.Parse(value!);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
