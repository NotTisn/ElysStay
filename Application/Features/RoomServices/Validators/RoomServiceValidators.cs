using Application.Features.RoomServices.Commands;
using FluentValidation;

namespace Application.Features.RoomServices.Validators;

public class UpdateRoomServicesCommandValidator : AbstractValidator<UpdateRoomServicesCommand>
{
    public UpdateRoomServicesCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty().WithMessage("Mã phòng là bắt buộc.");

        RuleFor(x => x.Services)
            .NotNull().WithMessage("Danh sách dịch vụ là bắt buộc.");

        RuleForEach(x => x.Services).ChildRules(entry =>
        {
            entry.RuleFor(e => e.ServiceId)
                .NotEmpty().WithMessage("Mã dịch vụ là bắt buộc.");

            entry.RuleFor(e => e.OverrideUnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Đơn giá tùy chỉnh không được âm.")
                .When(e => e.OverrideUnitPrice.HasValue);

            entry.RuleFor(e => e.OverrideQuantity)
                .GreaterThanOrEqualTo(0).WithMessage("Số lượng tùy chỉnh không được âm.")
                .When(e => e.OverrideQuantity.HasValue);
        });

        RuleFor(x => x.Services)
            .Must(services => services.Select(s => s.ServiceId).Distinct().Count() == services.Count)
            .WithMessage("Không được có mã dịch vụ trùng lặp.");
    }
}

public class RemoveRoomServiceOverrideCommandValidator : AbstractValidator<RemoveRoomServiceOverrideCommand>
{
    public RemoveRoomServiceOverrideCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty().WithMessage("Mã phòng là bắt buộc.");

        RuleFor(x => x.ServiceId)
            .NotEmpty().WithMessage("Mã dịch vụ là bắt buộc.");
    }
}
