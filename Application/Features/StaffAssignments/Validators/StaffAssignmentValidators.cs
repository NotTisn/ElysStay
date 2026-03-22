using Application.Features.StaffAssignments.Commands;
using FluentValidation;

namespace Application.Features.StaffAssignments.Validators;

public class AssignStaffCommandValidator : AbstractValidator<AssignStaffCommand>
{
    public AssignStaffCommandValidator()
    {
        RuleFor(x => x.BuildingId)
            .NotEmpty().WithMessage("Mã tòa nhà là bắt buộc.");

        RuleFor(x => x.StaffId)
            .NotEmpty().WithMessage("Mã nhân viên là bắt buộc.");
    }
}

public class UnassignStaffCommandValidator : AbstractValidator<UnassignStaffCommand>
{
    public UnassignStaffCommandValidator()
    {
        RuleFor(x => x.BuildingId)
            .NotEmpty().WithMessage("Mã tòa nhà là bắt buộc.");

        RuleFor(x => x.StaffId)
            .NotEmpty().WithMessage("Mã nhân viên là bắt buộc.");
    }
}
