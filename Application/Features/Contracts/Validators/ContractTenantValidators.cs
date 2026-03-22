using Application.Features.Contracts.Commands;
using FluentValidation;

namespace Application.Features.Contracts.Validators;

public class AddContractTenantCommandValidator : AbstractValidator<AddContractTenantCommand>
{
    public AddContractTenantCommandValidator()
    {
        RuleFor(x => x.ContractId)
            .NotEmpty().WithMessage("Mã hợp đồng là bắt buộc.");

        RuleFor(x => x.TenantUserId)
            .NotEmpty().WithMessage("Mã khách thuê là bắt buộc.");

        RuleFor(x => x.MoveInDate)
            .NotEmpty().WithMessage("Ngày dọn vào là bắt buộc.");
    }
}

public class RemoveContractTenantCommandValidator : AbstractValidator<RemoveContractTenantCommand>
{
    public RemoveContractTenantCommandValidator()
    {
        RuleFor(x => x.ContractId)
            .NotEmpty().WithMessage("Mã hợp đồng là bắt buộc.");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Mã khách thuê là bắt buộc.");
    }
}
