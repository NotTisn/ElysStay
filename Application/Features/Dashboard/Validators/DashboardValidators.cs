using Application.Features.Dashboard.Queries;
using FluentValidation;

namespace Application.Features.Dashboard.Validators;

public class GetPnlReportQueryValidator : AbstractValidator<GetPnlReportQuery>
{
    public GetPnlReportQueryValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2020, 2100)
            .WithMessage("Year must be between 2020 and 2100.");
    }
}
