using Application.Features.StaffAssignments.Commands;
using FluentValidation;

namespace Application.Features.StaffAssignments.Validators;

public class AssignStaffCommandValidator : AbstractValidator<AssignStaffCommand>
{
    public AssignStaffCommandValidator()
    {
        RuleFor(x => x.BuildingId)
            .NotEmpty().WithMessage("BuildingId is required.");

        RuleFor(x => x.StaffId)
            .NotEmpty().WithMessage("StaffId is required.");
    }
}

public class UnassignStaffCommandValidator : AbstractValidator<UnassignStaffCommand>
{
    public UnassignStaffCommandValidator()
    {
        RuleFor(x => x.BuildingId)
            .NotEmpty().WithMessage("BuildingId is required.");

        RuleFor(x => x.StaffId)
            .NotEmpty().WithMessage("StaffId is required.");
    }
}
