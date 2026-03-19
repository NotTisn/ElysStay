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
            .NotEmpty().WithMessage("Mã người dùng là bắt buộc.");

        RuleFor(x => x.IdNumber)
            .Matches(@"^\d{12}$")
            .When(x => x.IdNumber is not null)
            .WithMessage("Số CCCD phải đúng 12 chữ số.");

        RuleFor(x => x.Gender)
            .MaximumLength(20).WithMessage("Giới tính không được vượt quá 20 ký tự.")
            .When(x => x.Gender is not null);

        RuleFor(x => x.PermanentAddress)
            .MaximumLength(500).WithMessage("Địa chỉ thường trú không được vượt quá 500 ký tự.")
            .When(x => x.PermanentAddress is not null);

        RuleFor(x => x.IssuedPlace)
            .MaximumLength(200).WithMessage("Nơi cấp không được vượt quá 200 ký tự.")
            .When(x => x.IssuedPlace is not null);
    }
}
