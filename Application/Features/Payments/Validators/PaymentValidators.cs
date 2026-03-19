using Application.Features.Payments.Commands;
using Application.Features.Payments.Queries;
using Domain.Enums;
using FluentValidation;

namespace Application.Features.Payments.Validators;

public class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty().WithMessage("InvoiceId is required.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero (PAY-02).");
        RuleFor(x => x.PaymentMethod)
            .MaximumLength(50).When(x => x.PaymentMethod is not null)
            .WithMessage("PaymentMethod cannot exceed 50 characters.");
        RuleFor(x => x.Note)
            .MaximumLength(500).When(x => x.Note is not null)
            .WithMessage("Note cannot exceed 500 characters.");
    }
}

public class BatchRecordPaymentsCommandValidator : AbstractValidator<BatchRecordPaymentsCommand>
{
    public BatchRecordPaymentsCommandValidator()
    {
        RuleFor(x => x.Payments).NotEmpty().WithMessage("At least one payment is required.");
        RuleFor(x => x.Payments.Count).LessThanOrEqualTo(50)
            .WithMessage("Maximum 50 payments per batch request.");
        RuleForEach(x => x.Payments).ChildRules(entry =>
        {
            entry.RuleFor(e => e.InvoiceId).NotEmpty().WithMessage("InvoiceId is required.");
            entry.RuleFor(e => e.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero (PAY-02).");
            entry.RuleFor(e => e.PaymentMethod)
                .MaximumLength(50).When(e => e.PaymentMethod is not null)
                .WithMessage("PaymentMethod cannot exceed 50 characters.");
            entry.RuleFor(e => e.Note)
                .MaximumLength(500).When(e => e.Note is not null)
                .WithMessage("Note cannot exceed 500 characters.");
        });
    }
}

public class GetPaymentsQueryValidator : AbstractValidator<GetPaymentsQuery>
{
    public GetPaymentsQueryValidator()
    {
        RuleFor(x => x.Type)
            .Must(type => string.IsNullOrWhiteSpace(type) || Enum.TryParse<PaymentType>(type, true, out _))
            .WithMessage("Type must be one of: RentPayment, DepositIn, DepositRefund.");

        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate!.Value)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("FromDate must be on or before ToDate.");
    }
}

public class GetPaymentSummaryQueryValidator : AbstractValidator<GetPaymentSummaryQuery>
{
    public GetPaymentSummaryQueryValidator()
    {
        RuleFor(x => x.Type)
            .Must(type => string.IsNullOrWhiteSpace(type) || Enum.TryParse<PaymentType>(type, true, out _))
            .WithMessage("Type must be one of: RentPayment, DepositIn, DepositRefund.");

        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate!.Value)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("FromDate must be on or before ToDate.");
    }
}
