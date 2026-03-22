using Application.Features.Buildings.Commands;
using FluentValidation;

namespace Application.Features.Buildings.Validators;

public class UpdateBuildingCommandValidator : AbstractValidator<UpdateBuildingCommand>
{
    public UpdateBuildingCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Mã tòa nhà là bắt buộc.");

        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Tên tòa nhà không được vượt quá 200 ký tự.")
            .When(x => x.Name is not null);

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Địa chỉ không được vượt quá 500 ký tự.")
            .When(x => x.Address is not null);

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Mô tả không được vượt quá 2000 ký tự.")
            .When(x => x.Description is not null);

        RuleFor(x => x.TotalFloors)
            .GreaterThanOrEqualTo(1).WithMessage("Số tầng phải ít nhất là 1.")
            .LessThanOrEqualTo(200).WithMessage("Số tầng không được vượt quá 200.")
            .When(x => x.TotalFloors.HasValue);

        // BD-02: invoiceDueDay range 1-28
        RuleFor(x => x.InvoiceDueDay)
            .InclusiveBetween(1, 28).WithMessage("Ngày đến hạn hóa đơn phải từ 1 đến 28.")
            .When(x => x.InvoiceDueDay.HasValue);
    }
}
