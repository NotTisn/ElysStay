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
    }
}
