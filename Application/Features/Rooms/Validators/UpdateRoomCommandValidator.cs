using Application.Features.Rooms.Commands;
using FluentValidation;

namespace Application.Features.Rooms.Validators;

public class UpdateRoomCommandValidator : AbstractValidator<UpdateRoomCommand>
{
    public UpdateRoomCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Mã phòng là bắt buộc.");

        RuleFor(x => x.RoomNumber)
            .MaximumLength(50).WithMessage("Số phòng không được vượt quá 50 ký tự.")
            .When(x => x.RoomNumber is not null);

        RuleFor(x => x.Floor)
            .GreaterThanOrEqualTo(1).WithMessage("Tầng phải ít nhất là 1.")
            .When(x => x.Floor.HasValue);

        RuleFor(x => x.Area)
            .GreaterThan(0).WithMessage("Diện tích phải lớn hơn 0.")
            .LessThanOrEqualTo(1000).WithMessage("Diện tích không được vượt quá 1000 m².")
            .When(x => x.Area.HasValue);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Giá phòng không được âm.")
            .When(x => x.Price.HasValue);

        RuleFor(x => x.MaxOccupants)
            .GreaterThanOrEqualTo(1).WithMessage("Số người tối đa phải ít nhất là 1.")
            .LessThanOrEqualTo(20).WithMessage("Số người tối đa không được vượt quá 20.")
            .When(x => x.MaxOccupants.HasValue);

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Mô tả không được vượt quá 2000 ký tự.")
            .When(x => x.Description is not null);
    }
}
