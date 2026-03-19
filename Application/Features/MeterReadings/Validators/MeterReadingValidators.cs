using Application.Features.MeterReadings.Commands;
using Application.Features.MeterReadings.Queries;
using FluentValidation;

namespace Application.Features.MeterReadings.Validators;

public class BulkUpsertMeterReadingsCommandValidator : AbstractValidator<BulkUpsertMeterReadingsCommand>
{
    public BulkUpsertMeterReadingsCommandValidator()
    {
        RuleFor(x => x.BuildingId).NotEmpty().WithMessage("Mã tòa nhà là bắt buộc.");
        RuleFor(x => x.BillingYear).InclusiveBetween(2020, 2100).WithMessage("Năm thanh toán phải từ 2020 đến 2100.");
        RuleFor(x => x.BillingMonth).InclusiveBetween(1, 12).WithMessage("Tháng thanh toán phải từ 1 đến 12.");
        RuleFor(x => x.Readings).NotEmpty().WithMessage("Cần ít nhất một chỉ số.");
        RuleFor(x => x.Readings.Count).LessThanOrEqualTo(500)
            .WithMessage("Tối đa 500 chỉ số mỗi lần.");

        RuleForEach(x => x.Readings).ChildRules(reading =>
        {
            reading.RuleFor(r => r.RoomId).NotEmpty().WithMessage("Mã phòng là bắt buộc.");
            reading.RuleFor(r => r.ServiceId).NotEmpty().WithMessage("Mã dịch vụ là bắt buộc.");
            reading.RuleFor(r => r.CurrentReading).GreaterThanOrEqualTo(0).WithMessage("Chỉ số hiện tại phải >= 0.");
        });
    }
}

public class UpdateMeterReadingCommandValidator : AbstractValidator<UpdateMeterReadingCommand>
{
    public UpdateMeterReadingCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Mã chỉ số là bắt buộc.");
    }
}

public class GetMeterReadingsQueryValidator : AbstractValidator<GetMeterReadingsQuery>
{
    public GetMeterReadingsQueryValidator()
    {
        RuleFor(x => x.BillingYear)
            .InclusiveBetween(2020, 2100)
            .WithMessage("Năm thanh toán phải từ 2020 đến 2100.");
        RuleFor(x => x.BillingMonth)
            .InclusiveBetween(1, 12)
            .WithMessage("Tháng thanh toán phải từ 1 đến 12.");
    }
}
