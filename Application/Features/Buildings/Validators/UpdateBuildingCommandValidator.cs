using Application.Features.Buildings.Commands;
using FluentValidation;

namespace Application.Features.Buildings.Validators;

public class UpdateBuildingCommandValidator : AbstractValidator<UpdateBuildingCommand>
{
    public UpdateBuildingCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Building ID is required.");

        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Building name must not exceed 200 characters.")
            .When(x => x.Name is not null);

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Address must not exceed 500 characters.")
            .When(x => x.Address is not null);

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.TotalFloors)
            .GreaterThanOrEqualTo(1).WithMessage("Total floors must be at least 1.")
            .LessThanOrEqualTo(200).WithMessage("Total floors must not exceed 200.")
            .When(x => x.TotalFloors.HasValue);

        // BD-02: invoiceDueDay range 1-28
        RuleFor(x => x.InvoiceDueDay)
            .InclusiveBetween(1, 28).WithMessage("Invoice due day must be between 1 and 28.")
            .When(x => x.InvoiceDueDay.HasValue);
    }
}
