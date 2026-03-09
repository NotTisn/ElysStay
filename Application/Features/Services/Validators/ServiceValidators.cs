using Application.Features.Services.Commands;
using FluentValidation;

namespace Application.Features.Services.Validators;

public class CreateServiceCommandValidator : AbstractValidator<CreateServiceCommand>
{
    public CreateServiceCommandValidator()
    {
        RuleFor(x => x.BuildingId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(50);
        RuleFor(x => x.UnitPrice).GreaterThan(0).WithMessage("UnitPrice must be greater than 0.");
    }
}

public class UpdateServiceCommandValidator : AbstractValidator<UpdateServiceCommand>
{
    public UpdateServiceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.Unit).MaximumLength(50).When(x => x.Unit is not null);
        RuleFor(x => x.UnitPrice).GreaterThan(0).When(x => x.UnitPrice.HasValue).WithMessage("UnitPrice must be greater than 0.");
    }
}
