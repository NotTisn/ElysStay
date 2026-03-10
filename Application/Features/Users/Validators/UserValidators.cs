using Application.Features.Users.Commands;
using FluentValidation;

namespace Application.Features.Users.Validators;

public class UpdateCurrentUserCommandValidator : AbstractValidator<UpdateCurrentUserCommand>
{
    public UpdateCurrentUserCommandValidator()
    {
        RuleFor(x => x.FullName)
            .MaximumLength(200).WithMessage("Full name must not exceed 200 characters.")
            .When(x => x.FullName is not null);

        // VAL-02: Phone must be exactly 10 digits
        RuleFor(x => x.Phone)
            .Matches(@"^\d{10}$").WithMessage("Phone number must be exactly 10 digits.")
            .When(x => x.Phone is not null && !string.IsNullOrWhiteSpace(x.Phone));
    }
}

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        // AUTH-03: Minimum 8 characters
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}

public class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.")
            .MaximumLength(254).WithMessage("Email must not exceed 254 characters.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(200).WithMessage("Full name must not exceed 200 characters.");

        RuleFor(x => x.Phone)
            .Matches(@"^\d{10}$").WithMessage("Phone number must be exactly 10 digits.")
            .When(x => x.Phone is not null);

        RuleFor(x => x.Password)
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .When(x => x.Password is not null);
    }
}

public class CreateStaffCommandValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.")
            .MaximumLength(254).WithMessage("Email must not exceed 254 characters.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(200).WithMessage("Full name must not exceed 200 characters.");

        RuleFor(x => x.Phone)
            .Matches(@"^\d{10}$").WithMessage("Phone number must be exactly 10 digits.")
            .When(x => x.Phone is not null);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}
