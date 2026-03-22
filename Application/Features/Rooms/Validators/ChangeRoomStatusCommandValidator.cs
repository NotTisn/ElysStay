using Application.Features.Rooms.Commands;
using FluentValidation;

namespace Application.Features.Rooms.Validators;

public class ChangeRoomStatusCommandValidator : AbstractValidator<ChangeRoomStatusCommand>
{
    public ChangeRoomStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Mã phòng là bắt buộc.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Trạng thái là bắt buộc.");
    }
}
