using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Invoices.Commands;

internal static class InvoiceBuilder
{
    internal record Result(Invoice Invoice, List<InvoiceDetail> LineItems, List<string> Warnings);

    internal static Result Build(
        Contract contract,
        IList<Service> buildingServices,
        IReadOnlyDictionary<Guid, RoomService> roomOverrides,   
        IReadOnlyDictionary<Guid, MeterReading> meterReadings,  
        DateOnly billingPeriodStart,
        DateOnly billingPeriodEnd,
        DateOnly dueDate)
    {
        var warnings = new List<string>();

        var rentAmount = CalculateRentAmount(contract, billingPeriodStart, billingPeriodEnd);

        var lineItems = new List<InvoiceDetail>
        {
            new() { InvoiceId = Guid.Empty, Description = "Tiền phòng", Quantity = 1, UnitPrice = rentAmount, Amount = rentAmount }
        };

        var activeOccupantCount = contract.ContractTenants
            .Count(ct => ct.MoveInDate <= billingPeriodEnd
                && (ct.MoveOutDate == null || ct.MoveOutDate >= billingPeriodStart));

        var billingYear = billingPeriodStart.Year;
        var billingMonth = billingPeriodStart.Month;

        foreach (var service in buildingServices)
        {
            roomOverrides.TryGetValue(service.Id, out var roomService);

            if (roomService?.IsEnabled == false)
                continue;

            var effectivePrice = roomService?.OverrideUnitPrice ?? service.UnitPrice;

            if (service.IsMetered)
            {
                if (!meterReadings.TryGetValue(service.Id, out var reading))
                {
                    warnings.Add($"Phòng {contract.Room!.RoomNumber}: Thiếu chỉ số đồng hồ cho '{service.Name}' kỳ {billingMonth}/{billingYear}");
                    continue;
                }

                lineItems.Add(new InvoiceDetail
                {
                    InvoiceId = Guid.Empty,
                    ServiceId = service.Id,
                    Description = service.Name,
                    Quantity = reading.Consumption,
                    UnitPrice = effectivePrice,
                    Amount = reading.Consumption * effectivePrice,
                    PreviousReading = reading.PreviousReading,
                    CurrentReading = reading.CurrentReading
                });
            }
            else
            {
                var quantity = roomService?.OverrideQuantity ?? activeOccupantCount;

                if (quantity <= 0)
                {
                    warnings.Add($"Phòng {contract.Room!.RoomNumber}: Không có cư dân trong kỳ cho dịch vụ '{service.Name}', đã bỏ qua");
                    continue;
                }

                lineItems.Add(new InvoiceDetail
                {
                    InvoiceId = Guid.Empty,
                    ServiceId = service.Id,
                    Description = service.Name,
                    Quantity = quantity,
                    UnitPrice = effectivePrice,
                    Amount = quantity * effectivePrice
                });
            }
        }

        var serviceAmount = lineItems.Where(li => li.ServiceId != null).Sum(li => li.Amount);

        var invoice = new Invoice
        {
            ContractId = contract.Id,
            BillingYear = billingYear,
            BillingMonth = billingMonth,
            DiscountAmount = 0,
            PenaltyAmount = 0,
            RentAmount = rentAmount,
            ServiceAmount = serviceAmount,
            TotalAmount = rentAmount + serviceAmount,
            Status = InvoiceStatus.Draft,
            DueDate = dueDate,
        };

        foreach (var li in lineItems)
            li.InvoiceId = invoice.Id;

        return new Result(invoice, lineItems, warnings);
    }

    internal static decimal CalculateRentAmount(
        Contract contract,
        DateOnly periodStart,
        DateOnly periodEnd)
    {
        var daysInMonth = DateTime.DaysInMonth(periodStart.Year, periodStart.Month);

        var effectiveStart = contract.MoveInDate > periodStart ? contract.MoveInDate : periodStart;

        var effectiveEnd = contract.TerminationDate.HasValue && contract.TerminationDate.Value < periodEnd
            ? contract.TerminationDate.Value
            : periodEnd;

        var days = Math.Max(0, effectiveEnd.DayNumber - effectiveStart.DayNumber + 1);

        if (days == daysInMonth)
            return contract.MonthlyRent;

        return Math.Round(contract.MonthlyRent * days / daysInMonth, 0, MidpointRounding.AwayFromZero);
    }
}
