using Application.Features.Invoices.Commands;
using FluentValidation;

namespace Application.Features.Invoices.Validators;

public class GenerateInvoicesCommandValidator : AbstractValidator<GenerateInvoicesCommand>
{
    public GenerateInvoicesCommandValidator()
    {
        RuleFor(x => x.BuildingId).NotEmpty().WithMessage("Mã tòa nhà là bắt buộc.");
        RuleFor(x => x.BillingYear).InclusiveBetween(2020, 2100).WithMessage("Năm thanh toán phải từ 2020 đến 2100.");
        RuleFor(x => x.BillingMonth).InclusiveBetween(1, 12).WithMessage("Tháng thanh toán phải từ 1 đến 12.");
    }
}

public class UpdateInvoiceCommandValidator : AbstractValidator<UpdateInvoiceCommand>
{
    public UpdateInvoiceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Mã hóa đơn là bắt buộc.");
        RuleFor(x => x.PenaltyAmount).GreaterThanOrEqualTo(0).When(x => x.PenaltyAmount.HasValue).WithMessage("Tiền phạt không được âm.");
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0).When(x => x.DiscountAmount.HasValue).WithMessage("Tiền giảm không được âm.");
        RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note is not null).WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}

public class BatchSendInvoicesCommandValidator : AbstractValidator<BatchSendInvoicesCommand>
{
    public BatchSendInvoicesCommandValidator()
    {
        RuleFor(x => x.InvoiceIds).NotEmpty().WithMessage("Cần ít nhất một mã hóa đơn.");
        RuleForEach(x => x.InvoiceIds)
            .NotEmpty().WithMessage("Mã hóa đơn không được để trống.");
        RuleFor(x => x.InvoiceIds.Count).LessThanOrEqualTo(100)
            .WithMessage("Tối đa 100 hóa đơn mỗi lần gửi.");
        RuleFor(x => x.InvoiceIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Không được có mã hóa đơn trùng lặp.");
    }
}
