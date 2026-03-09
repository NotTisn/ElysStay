using Application.Features.Invoices.Commands;
using FluentValidation;

namespace Application.Features.Invoices.Validators;

public class GenerateInvoicesCommandValidator : AbstractValidator<GenerateInvoicesCommand>
{
    public GenerateInvoicesCommandValidator()
    {
        RuleFor(x => x.BuildingId).NotEmpty().WithMessage("BuildingId is required.");
        RuleFor(x => x.BillingYear).InclusiveBetween(2020, 2100).WithMessage("BillingYear must be between 2020 and 2100 (VAL-06).");
        RuleFor(x => x.BillingMonth).InclusiveBetween(1, 12).WithMessage("BillingMonth must be between 1 and 12 (VAL-06).");
    }
}

public class UpdateInvoiceCommandValidator : AbstractValidator<UpdateInvoiceCommand>
{
    public UpdateInvoiceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Invoice ID is required.");
        RuleFor(x => x.PenaltyAmount).GreaterThanOrEqualTo(0).When(x => x.PenaltyAmount.HasValue).WithMessage("PenaltyAmount cannot be negative.");
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0).When(x => x.DiscountAmount.HasValue).WithMessage("DiscountAmount cannot be negative.");
    }
}

public class BatchSendInvoicesCommandValidator : AbstractValidator<BatchSendInvoicesCommand>
{
    public BatchSendInvoicesCommandValidator()
    {
        RuleFor(x => x.InvoiceIds).NotEmpty().WithMessage("At least one invoice ID is required.");
    }
}
