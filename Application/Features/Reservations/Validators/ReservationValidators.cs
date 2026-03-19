using Application.Features.Reservations.Commands;
using FluentValidation;

namespace Application.Features.Reservations.Validators;

public class CreateReservationCommandValidator : AbstractValidator<CreateReservationCommand>
{
    public CreateReservationCommandValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty().WithMessage("Mã phòng là bắt buộc.");
        RuleFor(x => x.TenantUserId).NotEmpty().WithMessage("Mã khách thuê là bắt buộc.");
        RuleFor(x => x.DepositAmount)
            .GreaterThan(0).WithMessage("Tiền đặt cọc phải lớn hơn 0.")
            .When(x => x.DepositAmount.HasValue);
        RuleFor(x => x.ExpiresAt)
            .Must(d => d > DateTime.UtcNow)
            .When(x => x.ExpiresAt.HasValue)
            .WithMessage("Ngày hết hạn phải trong tương lai.");
        RuleFor(x => x.Note)
            .MaximumLength(500).When(x => x.Note is not null)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}

public class ChangeReservationStatusCommandValidator : AbstractValidator<ChangeReservationStatusCommand>
{
    public ChangeReservationStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Mã đặt phòng là bắt buộc.");
        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Hành động là bắt buộc.")
            .Must(a => a.Equals("CONFIRM", StringComparison.OrdinalIgnoreCase)
                    || a.Equals("CANCEL", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Hành động phải là CONFIRM hoặc CANCEL.");
        RuleFor(x => x.RefundAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền hoàn không được âm.")
            .When(x => x.RefundAmount.HasValue);
        RuleFor(x => x.RefundNote)
            .MaximumLength(500).WithMessage("Ghi chú hoàn tiền không được vượt quá 500 ký tự.")
            .When(x => x.RefundNote is not null);
    }
}
