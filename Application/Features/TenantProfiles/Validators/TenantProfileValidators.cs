using Application.Features.TenantProfiles.Commands;
using FluentValidation;

namespace Application.Features.TenantProfiles.Validators;

/// <summary>
/// VAL-01: IdNumber must be exactly 12 digits.
/// </summary>
public class UpdateTenantProfileCommandValidator : AbstractValidator<UpdateTenantProfileCommand>
{
    public UpdateTenantProfileCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.IdNumber)
            .Matches(@"^\d{12}$")
            .When(x => x.IdNumber is not null)
            .WithMessage("IdNumber must be exactly 12 digits.");

        RuleFor(x => x.Gender)
            .MaximumLength(20)
            .When(x => x.Gender is not null);

        RuleFor(x => x.PermanentAddress)
            .MaximumLength(500)
            .When(x => x.PermanentAddress is not null);

        RuleFor(x => x.IssuedPlace)
            .MaximumLength(200)
            .When(x => x.IssuedPlace is not null);
    }
}
