using Application.Features.Contracts.Commands;
using FluentValidation;

namespace Application.Features.Contracts.Validators;

public class AddContractTenantCommandValidator : AbstractValidator<AddContractTenantCommand>
{
    public AddContractTenantCommandValidator()
    {
        RuleFor(x => x.ContractId)
            .NotEmpty().WithMessage("ContractId is required.");

        RuleFor(x => x.TenantUserId)
            .NotEmpty().WithMessage("TenantUserId is required.");

        RuleFor(x => x.MoveInDate)
            .NotEmpty().WithMessage("MoveInDate is required.");
    }
}

public class RemoveContractTenantCommandValidator : AbstractValidator<RemoveContractTenantCommand>
{
    public RemoveContractTenantCommandValidator()
    {
        RuleFor(x => x.ContractId)
            .NotEmpty().WithMessage("ContractId is required.");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required.");
    }
}
