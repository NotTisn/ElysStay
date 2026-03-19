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
        RuleFor(x => x.BuildingId).NotEmpty();
        RuleFor(x => x.Category)
            .NotEmpty().MaximumLength(100)
            .Must(c => AllowedCategories.Contains(c))
            .WithMessage($"Category must be one of: {string.Join(", ", AllowedCategories.Order())}.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.ExpenseDate)
            .NotEmpty()
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("ExpenseDate cannot be in the future.");
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
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .When(x => x.Amount.HasValue);
        RuleFor(x => x.Category)
            .NotEmpty()
            .MaximumLength(100)
            .Must(c => AllowedCategories.Contains(c))
            .WithMessage($"Category must be one of: {string.Join(", ", AllowedCategories.Order())}.")
            .When(x => x.Category is not null);
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(500)
            .When(x => x.Description is not null);
        RuleFor(x => x.ExpenseDate)
            .Must(d => d!.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("ExpenseDate cannot be in the future.")
            .When(x => x.ExpenseDate.HasValue);
    }
}

public class DeleteExpenseCommandValidator : AbstractValidator<DeleteExpenseCommand>
{
    public DeleteExpenseCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class GetExpensesQueryValidator : AbstractValidator<GetExpensesQuery>
{
    public GetExpensesQueryValidator()
    {
        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate!.Value)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("FromDate must be on or before ToDate.");
    }
}

public class GetExpenseSummaryQueryValidator : AbstractValidator<GetExpenseSummaryQuery>
{
    public GetExpenseSummaryQueryValidator()
    {
        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate!.Value)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("FromDate must be on or before ToDate.");
    }
}
