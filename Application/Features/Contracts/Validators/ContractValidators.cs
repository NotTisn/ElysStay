using FluentValidation;

namespace Application.Features.Contracts.Validators;

public class CreateContractCommandValidator : AbstractValidator<Commands.CreateContractCommand>
{
    public CreateContractCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty().WithMessage("Mã phòng là bắt buộc.");

        RuleFor(x => x.TenantUserId)
            .NotEmpty().WithMessage("Mã khách thuê là bắt buộc.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Ngày bắt đầu là bắt buộc.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("Ngày kết thúc là bắt buộc.")
            .GreaterThan(x => x.StartDate).WithMessage("Ngày kết thúc phải sau ngày bắt đầu.");

        RuleFor(x => x.MoveInDate)
            .NotEmpty().WithMessage("Ngày dọn vào là bắt buộc.")
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Ngày dọn vào không được trước ngày bắt đầu.")
            .LessThanOrEqualTo(x => x.EndDate)
            .WithMessage("Ngày dọn vào không được sau ngày kết thúc.");

        RuleFor(x => x.MonthlyRent)
            .GreaterThan(0).WithMessage("Tiền thuê hàng tháng phải lớn hơn 0.");

        RuleFor(x => x.DepositAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Tiền đặt cọc không được âm.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).When(x => x.Note is not null)
            .WithMessage("Ghi chú không được vượt quá 1000 ký tự.");
    }
}

public class UpdateContractCommandValidator : AbstractValidator<Commands.UpdateContractCommand>
{
    public UpdateContractCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Mã hợp đồng là bắt buộc.");

        RuleFor(x => x.MonthlyRent)
            .GreaterThan(0).When(x => x.MonthlyRent.HasValue)
            .WithMessage("Tiền thuê hàng tháng phải lớn hơn 0.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).When(x => x.Note is not null)
            .WithMessage("Ghi chú không được vượt quá 1000 ký tự.");

        // EndDate > StartDate is validated in the handler against the persisted StartDate
    }
}

public class TerminateContractCommandValidator : AbstractValidator<Commands.TerminateContractCommand>
{
    public TerminateContractCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Mã hợp đồng là bắt buộc.");

        RuleFor(x => x.TerminationDate)
            .NotEmpty().WithMessage("Ngày chấm dứt là bắt buộc.");

        RuleFor(x => x.Deductions)
            .GreaterThanOrEqualTo(0).WithMessage("Khoản khấu trừ không được âm.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).When(x => x.Note is not null)
            .WithMessage("Ghi chú chấm dứt không được vượt quá 1000 ký tự.");
    }
}

public class RenewContractCommandValidator : AbstractValidator<Commands.RenewContractCommand>
{
    public RenewContractCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Mã hợp đồng là bắt buộc.");

        RuleFor(x => x.NewEndDate)
            .NotEmpty().WithMessage("Ngày kết thúc mới là bắt buộc.");

        RuleFor(x => x.NewMonthlyRent)
            .GreaterThan(0).When(x => x.NewMonthlyRent.HasValue)
            .WithMessage("Tiền thuê hàng tháng mới phải lớn hơn 0.");
    }
}
