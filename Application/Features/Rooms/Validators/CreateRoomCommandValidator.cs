using Application.Features.Rooms.Commands;
using FluentValidation;

namespace Application.Features.Rooms.Validators;

public class CreateRoomCommandValidator : AbstractValidator<CreateRoomCommand>
{
    public CreateRoomCommandValidator()
    {
        RuleFor(x => x.BuildingId)
            .NotEmpty().WithMessage("Mã tòa nhà là bắt buộc.");

        RuleFor(x => x.RoomNumber)
            .NotEmpty().WithMessage("Số phòng là bắt buộc.")
            .MaximumLength(50).WithMessage("Số phòng không được vượt quá 50 ký tự.");

        RuleFor(x => x.Floor)
            .GreaterThanOrEqualTo(1).WithMessage("Tầng phải ít nhất là 1.");

        RuleFor(x => x.Area)
            .GreaterThan(0).WithMessage("Diện tích phải lớn hơn 0.")
            .LessThanOrEqualTo(1000).WithMessage("Diện tích không được vượt quá 1000 m².");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Giá phòng không được âm.");

        RuleFor(x => x.MaxOccupants)
            .GreaterThanOrEqualTo(1).WithMessage("Số người tối đa phải ít nhất là 1.")
            .LessThanOrEqualTo(20).WithMessage("Số người tối đa không được vượt quá 20.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Mô tả không được vượt quá 2000 ký tự.")
            .When(x => x.Description is not null);
    }
}
