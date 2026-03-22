using Application.Features.Users.Commands;
using FluentValidation;

namespace Application.Features.Users.Validators;

public class UpdateCurrentUserCommandValidator : AbstractValidator<UpdateCurrentUserCommand>
{
    public UpdateCurrentUserCommandValidator()
    {
        RuleFor(x => x.FullName)
            .MaximumLength(200).WithMessage("Họ tên không được vượt quá 200 ký tự.")
            .When(x => x.FullName is not null);

        // VAL-02: Phone must be exactly 10 digits
        RuleFor(x => x.Phone)
            .Matches(@"^\d{10}$").WithMessage("Số điện thoại phải đúng 10 chữ số.")
            .When(x => x.Phone is not null && !string.IsNullOrWhiteSpace(x.Phone));
    }
}

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Mật khẩu hiện tại là bắt buộc.");

        // AUTH-03: Minimum 8 characters
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu mới là bắt buộc.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự.");
    }
}

public class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email là bắt buộc.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.")
            .MaximumLength(254).WithMessage("Email không được vượt quá 254 ký tự.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên là bắt buộc.")
            .MaximumLength(200).WithMessage("Họ tên không được vượt quá 200 ký tự.");

        RuleFor(x => x.Phone)
            .Matches(@"^\d{10}$").WithMessage("Số điện thoại phải đúng 10 chữ số.")
            .When(x => x.Phone is not null);

        RuleFor(x => x.Password)
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự.")
            .When(x => x.Password is not null);
    }
}

public class CreateStaffCommandValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email là bắt buộc.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.")
            .MaximumLength(254).WithMessage("Email không được vượt quá 254 ký tự.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên là bắt buộc.")
            .MaximumLength(200).WithMessage("Họ tên không được vượt quá 200 ký tự.");

        RuleFor(x => x.Phone)
            .Matches(@"^\d{10}$").WithMessage("Số điện thoại phải đúng 10 chữ số.")
            .When(x => x.Phone is not null);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu là bắt buộc.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự.");
    }
}

public class ChangeUserStatusCommandValidator : AbstractValidator<ChangeUserStatusCommand>
{
    public ChangeUserStatusCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Mã người dùng là bắt buộc.");
    }
}
