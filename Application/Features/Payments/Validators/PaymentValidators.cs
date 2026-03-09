using Application.Features.Payments.Commands;
using FluentValidation;

namespace Application.Features.Payments.Validators;

public class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty().WithMessage("InvoiceId is required.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero (PAY-02).");
    }
}

public class BatchRecordPaymentsCommandValidator : AbstractValidator<BatchRecordPaymentsCommand>
{
    public BatchRecordPaymentsCommandValidator()
    {
        RuleFor(x => x.Payments).NotEmpty().WithMessage("At least one payment is required.");
        RuleForEach(x => x.Payments).ChildRules(entry =>
        {
            entry.RuleFor(e => e.InvoiceId).NotEmpty().WithMessage("InvoiceId is required.");
            entry.RuleFor(e => e.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero (PAY-02).");
        });
    }
}
