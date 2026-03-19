using Application.Features.Services.Commands;
using FluentValidation;

namespace Application.Features.Services.Validators;

public class CreateServiceCommandValidator : AbstractValidator<CreateServiceCommand>
{
    public CreateServiceCommandValidator()
    {
        RuleFor(x => x.BuildingId).NotEmpty().WithMessage("Mã tòa nhà là bắt buộc.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tên dịch vụ là bắt buộc.").MaximumLength(100).WithMessage("Tên dịch vụ không được vượt quá 100 ký tự.");
        RuleFor(x => x.Unit).NotEmpty().WithMessage("Đơn vị là bắt buộc.").MaximumLength(50).WithMessage("Đơn vị không được vượt quá 50 ký tự.");
        RuleFor(x => x.UnitPrice).GreaterThan(0).WithMessage("Đơn giá phải lớn hơn 0.");
    }
}

public class UpdateServiceCommandValidator : AbstractValidator<UpdateServiceCommand>
{
    public UpdateServiceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Mã dịch vụ là bắt buộc.");
        RuleFor(x => x.Name).MaximumLength(100).WithMessage("Tên dịch vụ không được vượt quá 100 ký tự.").When(x => x.Name is not null);
        RuleFor(x => x.Unit).MaximumLength(50).WithMessage("Đơn vị không được vượt quá 50 ký tự.").When(x => x.Unit is not null);
        RuleFor(x => x.UnitPrice).GreaterThan(0).When(x => x.UnitPrice.HasValue).WithMessage("Đơn giá phải lớn hơn 0.");
    }
}
