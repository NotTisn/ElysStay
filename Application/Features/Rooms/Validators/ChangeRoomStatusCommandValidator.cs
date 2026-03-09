using Application.Features.Rooms.Commands;
using FluentValidation;

namespace Application.Features.Rooms.Validators;

public class ChangeRoomStatusCommandValidator : AbstractValidator<ChangeRoomStatusCommand>
{
    public ChangeRoomStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Room ID is required.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.");
    }
}
