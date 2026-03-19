using Application.Features.RoomServices.Commands;
using FluentValidation;

namespace Application.Features.RoomServices.Validators;

public class UpdateRoomServicesCommandValidator : AbstractValidator<UpdateRoomServicesCommand>
{
    public UpdateRoomServicesCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty().WithMessage("RoomId is required.");

        RuleFor(x => x.Services)
            .NotNull().WithMessage("Services list is required.");

        RuleForEach(x => x.Services).ChildRules(entry =>
        {
            entry.RuleFor(e => e.ServiceId)
                .NotEmpty().WithMessage("ServiceId is required.");

            entry.RuleFor(e => e.OverrideUnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("OverrideUnitPrice cannot be negative.")
                .When(e => e.OverrideUnitPrice.HasValue);

            entry.RuleFor(e => e.OverrideQuantity)
                .GreaterThanOrEqualTo(0).WithMessage("OverrideQuantity cannot be negative.")
                .When(e => e.OverrideQuantity.HasValue);
        });

        RuleFor(x => x.Services)
            .Must(services => services.Select(s => s.ServiceId).Distinct().Count() == services.Count)
            .WithMessage("Duplicate ServiceId entries are not allowed.");
    }
}

public class RemoveRoomServiceOverrideCommandValidator : AbstractValidator<RemoveRoomServiceOverrideCommand>
{
    public RemoveRoomServiceOverrideCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty().WithMessage("RoomId is required.");

        RuleFor(x => x.ServiceId)
            .NotEmpty().WithMessage("ServiceId is required.");
    }
}
