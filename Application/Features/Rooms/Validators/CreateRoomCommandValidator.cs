using Application.Features.Rooms.Commands;
using FluentValidation;

namespace Application.Features.Rooms.Validators;

public class CreateRoomCommandValidator : AbstractValidator<CreateRoomCommand>
{
    public CreateRoomCommandValidator()
    {
        RuleFor(x => x.BuildingId)
            .NotEmpty().WithMessage("Building ID is required.");

        RuleFor(x => x.RoomNumber)
            .NotEmpty().WithMessage("Room number is required.")
            .MaximumLength(20).WithMessage("Room number must not exceed 20 characters.");

        RuleFor(x => x.Floor)
            .GreaterThanOrEqualTo(1).WithMessage("Floor must be at least 1.");

        RuleFor(x => x.Area)
            .GreaterThan(0).WithMessage("Area must be greater than 0.")
            .LessThanOrEqualTo(1000).WithMessage("Area must not exceed 1000 m².");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be non-negative.");

        RuleFor(x => x.MaxOccupants)
            .GreaterThanOrEqualTo(1).WithMessage("Max occupants must be at least 1.")
            .LessThanOrEqualTo(20).WithMessage("Max occupants must not exceed 20.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.")
            .When(x => x.Description is not null);
    }
}
