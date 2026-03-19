using Application.Features.Expenses.Commands;
using Application.Features.Expenses.Queries;
using FluentValidation;

namespace Application.Features.Expenses.Validators;

public class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
{
    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Repair", "Maintenance", "Utilities", "Cleaning", "Insurance",
        "Tax", "Management", "Equipment", "Supplies", "Other"
    };

    public CreateExpenseCommandValidator()
    {
        RuleFor(x => x.BuildingId).NotEmpty().WithMessage("Mã tòa nhà là bắt buộc.");
        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Danh mục là bắt buộc.").MaximumLength(100)
            .Must(c => AllowedCategories.Contains(c))
            .WithMessage($"Danh mục phải là một trong: {string.Join(", ", AllowedCategories.Order())}.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Mô tả là bắt buộc.").MaximumLength(500).WithMessage("Mô tả không được vượt quá 500 ký tự.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Số tiền phải lớn hơn 0.");
        RuleFor(x => x.ExpenseDate)
            .NotEmpty().WithMessage("Ngày chi phí là bắt buộc.")
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Ngày chi phí không được trong tương lai.");
    }
}

public class UpdateExpenseCommandValidator : AbstractValidator<UpdateExpenseCommand>
{
    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Repair", "Maintenance", "Utilities", "Cleaning", "Insurance",
        "Tax", "Management", "Equipment", "Supplies", "Other"
    };

    public UpdateExpenseCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Mã chi phí là bắt buộc.");
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền phải lớn hơn 0.")
            .When(x => x.Amount.HasValue);
        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Danh mục là bắt buộc.")
            .MaximumLength(100)
            .Must(c => AllowedCategories.Contains(c))
            .WithMessage($"Danh mục phải là một trong: {string.Join(", ", AllowedCategories.Order())}.")
            .When(x => x.Category is not null);
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Mô tả là bắt buộc.")
            .MaximumLength(500).WithMessage("Mô tả không được vượt quá 500 ký tự.")
            .When(x => x.Description is not null);
        RuleFor(x => x.ExpenseDate)
            .Must(d => d!.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Ngày chi phí không được trong tương lai.")
            .When(x => x.ExpenseDate.HasValue);
    }
}

public class DeleteExpenseCommandValidator : AbstractValidator<DeleteExpenseCommand>
{
    public DeleteExpenseCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Mã chi phí là bắt buộc.");
    }
}

public class GetExpensesQueryValidator : AbstractValidator<GetExpensesQuery>
{
    public GetExpensesQueryValidator()
    {
        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate!.Value)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
    }
}

public class GetExpenseSummaryQueryValidator : AbstractValidator<GetExpenseSummaryQuery>
{
    public GetExpenseSummaryQueryValidator()
    {
        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate!.Value)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
    }
}
