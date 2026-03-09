using FluentValidation;

namespace Application.Features.Contracts.Validators;

public class CreateContractCommandValidator : AbstractValidator<Commands.CreateContractCommand>
{
    public CreateContractCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty().WithMessage("RoomId is required.");

        RuleFor(x => x.TenantUserId)
            .NotEmpty().WithMessage("TenantUserId is required.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("StartDate is required.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("EndDate is required.")
            .GreaterThan(x => x.StartDate).WithMessage("EndDate must be after StartDate (VAL-05).");

        RuleFor(x => x.MoveInDate)
            .NotEmpty().WithMessage("MoveInDate is required.");

        RuleFor(x => x.MonthlyRent)
            .GreaterThan(0).WithMessage("MonthlyRent must be greater than zero.");

        RuleFor(x => x.DepositAmount)
            .GreaterThanOrEqualTo(0).WithMessage("DepositAmount cannot be negative.");
    }
}

public class UpdateContractCommandValidator : AbstractValidator<Commands.UpdateContractCommand>
{
    public UpdateContractCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Contract ID is required.");

        RuleFor(x => x.MonthlyRent)
            .GreaterThan(0).When(x => x.MonthlyRent.HasValue)
            .WithMessage("MonthlyRent must be greater than zero.");

        // EndDate > StartDate is validated in the handler against the persisted StartDate
    }
}

public class TerminateContractCommandValidator : AbstractValidator<Commands.TerminateContractCommand>
{
    public TerminateContractCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Contract ID is required.");

        RuleFor(x => x.TerminationDate)
            .NotEmpty().WithMessage("TerminationDate is required.");

        RuleFor(x => x.Deductions)
            .GreaterThanOrEqualTo(0).WithMessage("Deductions cannot be negative.");
    }
}

public class RenewContractCommandValidator : AbstractValidator<Commands.RenewContractCommand>
{
    public RenewContractCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Contract ID is required.");

        RuleFor(x => x.NewEndDate)
            .NotEmpty().WithMessage("NewEndDate is required.");

        RuleFor(x => x.NewMonthlyRent)
            .GreaterThan(0).When(x => x.NewMonthlyRent.HasValue)
            .WithMessage("NewMonthlyRent must be greater than zero.");
    }
}
