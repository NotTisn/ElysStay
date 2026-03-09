using Application.Features.MeterReadings.Commands;
using FluentValidation;

namespace Application.Features.MeterReadings.Validators;

public class BulkUpsertMeterReadingsCommandValidator : AbstractValidator<BulkUpsertMeterReadingsCommand>
{
    public BulkUpsertMeterReadingsCommandValidator()
    {
        RuleFor(x => x.BuildingId).NotEmpty().WithMessage("BuildingId is required.");
        RuleFor(x => x.BillingYear).InclusiveBetween(2020, 2100).WithMessage("BillingYear must be between 2020 and 2100 (VAL-06).");
        RuleFor(x => x.BillingMonth).InclusiveBetween(1, 12).WithMessage("BillingMonth must be between 1 and 12 (VAL-06).");
        RuleFor(x => x.Readings).NotEmpty().WithMessage("At least one reading is required.");

        RuleForEach(x => x.Readings).ChildRules(reading =>
        {
            reading.RuleFor(r => r.RoomId).NotEmpty().WithMessage("RoomId is required.");
            reading.RuleFor(r => r.ServiceId).NotEmpty().WithMessage("ServiceId is required.");
            reading.RuleFor(r => r.CurrentReading).GreaterThanOrEqualTo(0).WithMessage("CurrentReading must be >= 0.");
        });
    }
}

public class UpdateMeterReadingCommandValidator : AbstractValidator<UpdateMeterReadingCommand>
{
    public UpdateMeterReadingCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Meter reading ID is required.");
    }
}
