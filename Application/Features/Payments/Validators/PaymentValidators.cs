using Application.Features.Payments.Commands;
using Application.Features.Payments.Queries;
using Domain.Enums;
using FluentValidation;

namespace Application.Features.Payments.Validators;

public class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty().WithMessage("Mã hóa đơn là bắt buộc.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Số tiền phải lớn hơn 0.");
        RuleFor(x => x.PaymentMethod)
            .MaximumLength(50).When(x => x.PaymentMethod is not null)
            .WithMessage("Phương thức thanh toán không được vượt quá 50 ký tự.");
        RuleFor(x => x.Note)
            .MaximumLength(500).When(x => x.Note is not null)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}

public class BatchRecordPaymentsCommandValidator : AbstractValidator<BatchRecordPaymentsCommand>
{
    public BatchRecordPaymentsCommandValidator()
    {
        RuleFor(x => x.Payments).NotEmpty().WithMessage("Cần ít nhất một khoản thanh toán.");
        RuleFor(x => x.Payments.Count).LessThanOrEqualTo(50)
            .WithMessage("Tối đa 50 khoản thanh toán mỗi lần.");
        RuleForEach(x => x.Payments).ChildRules(entry =>
        {
            entry.RuleFor(e => e.InvoiceId).NotEmpty().WithMessage("Mã hóa đơn là bắt buộc.");
            entry.RuleFor(e => e.Amount).GreaterThan(0).WithMessage("Số tiền phải lớn hơn 0.");
            entry.RuleFor(e => e.PaymentMethod)
                .MaximumLength(50).When(e => e.PaymentMethod is not null)
                .WithMessage("Phương thức thanh toán không được vượt quá 50 ký tự.");
            entry.RuleFor(e => e.Note)
                .MaximumLength(500).When(e => e.Note is not null)
                .WithMessage("Ghi chú không được vượt quá 500 ký tự.");
        });
    }
}

public class GetPaymentsQueryValidator : AbstractValidator<GetPaymentsQuery>
{
    public GetPaymentsQueryValidator()
    {
        RuleFor(x => x.Type)
            .Must(type => string.IsNullOrWhiteSpace(type) || Enum.TryParse<PaymentType>(type, true, out _))
            .WithMessage("Loại thanh toán phải là: RentPayment, DepositIn, hoặc DepositRefund.");

        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate!.Value)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
    }
}

public class GetPaymentSummaryQueryValidator : AbstractValidator<GetPaymentSummaryQuery>
{
    public GetPaymentSummaryQueryValidator()
    {
        RuleFor(x => x.Type)
            .Must(type => string.IsNullOrWhiteSpace(type) || Enum.TryParse<PaymentType>(type, true, out _))
            .WithMessage("Loại thanh toán phải là: RentPayment, DepositIn, hoặc DepositRefund.");

        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate!.Value)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
    }
}
