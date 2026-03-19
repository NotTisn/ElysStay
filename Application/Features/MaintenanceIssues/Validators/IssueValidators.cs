using Application.Features.MaintenanceIssues.Commands;
using FluentValidation;

namespace Application.Features.MaintenanceIssues.Validators;

public class CreateIssueCommandValidator : AbstractValidator<CreateIssueCommand>
{
    public CreateIssueCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Tiêu đề là bắt buộc.").MaximumLength(200).WithMessage("Tiêu đề không được vượt quá 200 ký tự.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Mô tả là bắt buộc.").MaximumLength(2000).WithMessage("Mô tả không được vượt quá 2000 ký tự.");
    }
}

public class UpdateIssueCommandValidator : AbstractValidator<UpdateIssueCommand>
{
    public UpdateIssueCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Mã sự cố là bắt buộc.");
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề là bắt buộc.")
            .MaximumLength(200).WithMessage("Tiêu đề không được vượt quá 200 ký tự.")
            .When(x => x.Title is not null);
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Mô tả là bắt buộc.")
            .MaximumLength(2000).WithMessage("Mô tả không được vượt quá 2000 ký tự.")
            .When(x => x.Description is not null);
    }
}

public class ChangeIssueStatusCommandValidator : AbstractValidator<ChangeIssueStatusCommand>
{
    public ChangeIssueStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Mã sự cố là bắt buộc.");
        RuleFor(x => x.Status).IsInEnum().WithMessage("Trạng thái không hợp lệ.");
    }
}
