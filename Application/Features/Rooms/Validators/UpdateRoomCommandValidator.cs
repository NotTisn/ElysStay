using Application.Features.Rooms.Commands;
using FluentValidation;

namespace Application.Features.Rooms.Validators;

public class UpdateRoomCommandValidator : AbstractValidator<UpdateRoomCommand>
{
    public UpdateRoomCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Room ID is required.");

        RuleFor(x => x.RoomNumber)
            .MaximumLength(20).WithMessage("Room number must not exceed 20 characters.")
            .When(x => x.RoomNumber is not null);

        RuleFor(x => x.Floor)
            .GreaterThanOrEqualTo(1).WithMessage("Floor must be at least 1.")
            .When(x => x.Floor.HasValue);

        RuleFor(x => x.Area)
            .GreaterThan(0).WithMessage("Area must be greater than 0.")
            .LessThanOrEqualTo(1000).WithMessage("Area must not exceed 1000 m².")
            .When(x => x.Area.HasValue);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be non-negative.")
            .When(x => x.Price.HasValue);

        RuleFor(x => x.MaxOccupants)
            .GreaterThanOrEqualTo(1).WithMessage("Max occupants must be at least 1.")
            .LessThanOrEqualTo(20).WithMessage("Max occupants must not exceed 20.")
            .When(x => x.MaxOccupants.HasValue);

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.")
            .When(x => x.Description is not null);
    }
}
