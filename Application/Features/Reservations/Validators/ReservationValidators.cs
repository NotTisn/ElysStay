using Application.Features.Reservations.Commands;
using FluentValidation;

namespace Application.Features.Reservations.Validators;

public class CreateReservationCommandValidator : AbstractValidator<CreateReservationCommand>
{
    public CreateReservationCommandValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty();
        RuleFor(x => x.TenantUserId).NotEmpty();
        RuleFor(x => x.DepositAmount)
            .GreaterThan(0)
            .When(x => x.DepositAmount.HasValue);
        RuleFor(x => x.ExpiresAt)
            .Must(d => d > DateTime.UtcNow)
            .When(x => x.ExpiresAt.HasValue)
            .WithMessage("Expiry date must be in the future.");
    }
}

public class ChangeReservationStatusCommandValidator : AbstractValidator<ChangeReservationStatusCommand>
{
    public ChangeReservationStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Action)
            .NotEmpty()
            .Must(a => a.Equals("CONFIRM", StringComparison.OrdinalIgnoreCase)
                    || a.Equals("CANCEL", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Action must be CONFIRM or CANCEL.");
        RuleFor(x => x.RefundAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.RefundAmount.HasValue);
        RuleFor(x => x.RefundNote)
            .MaximumLength(500)
            .When(x => x.RefundNote is not null);
    }
}
