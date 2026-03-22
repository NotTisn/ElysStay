using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Dashboard.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Dashboard.Queries;

/// <summary>
/// GET /reports/pnl — Profit & Loss report. Owner only.
/// PNL-01 through PNL-04: Monthly breakdown of income, deposits, expenses, net.
/// </summary>
public class GetPnlReportQuery : IRequest<PnlReportDto>
{
    public Guid? BuildingId { get; set; }
    public int Year { get; set; }
}

public class GetPnlReportQueryHandler : IRequestHandler<GetPnlReportQuery, PnlReportDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPnlReportQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PnlReportDto> Handle(GetPnlReportQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        if (!_currentUser.IsOwner)
            throw new ForbiddenException("Chỉ chủ nhà mới có thể xem báo cáo lãi lỗ.");

        // Get owner's buildings
        var buildingIds = await _db.Buildings
            .Where(b => b.OwnerId == userId && b.DeletedAt == null)
            .Select(b => b.Id)
            .ToListAsync(ct);

        if (request.BuildingId.HasValue)
        {
            if (!buildingIds.Contains(request.BuildingId.Value))
                throw new ForbiddenException("Bạn không sở hữu tòa nhà này.");
            buildingIds = [request.BuildingId.Value];
        }

        var yearStart = new DateTime(request.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd = yearStart.AddYears(1);

        // Aggregate payments by month/type in SQL.
        // PNL-04: Exclude payments tied to VOID invoices.
        var paymentBuckets = await _db.Payments
            .AsNoTracking()
            .Where(p => p.PaidAt >= yearStart && p.PaidAt < yearEnd)
            .Where(p =>
                // Contract-level payments (deposits)
                (p.ContractId != null && buildingIds.Contains(p.Contract!.Room!.BuildingId))
                ||
                // Invoice-level payments (rent)
                (p.InvoiceId != null && p.Invoice!.Status != InvoiceStatus.Void
                    && buildingIds.Contains(p.Invoice.Contract!.Room!.BuildingId))
                ||
                // Reservation-level payments (cancelled reservation deposit in/refund)
                (p.ReservationId != null && buildingIds.Contains(p.Reservation!.Room!.BuildingId)))
            .GroupBy(p => new { Month = p.PaidAt.Month, p.Type })
            .Select(g => new
            {
                g.Key.Month,
                g.Key.Type,
                Amount = g.Sum(p => p.Amount)
            })
            .ToListAsync(ct);

        var paymentByMonthType = paymentBuckets
            .ToDictionary(x => (x.Month, x.Type), x => x.Amount);

        // Aggregate expenses by month in SQL.
        var expenseBuckets = await _db.Expenses
            .AsNoTracking()
            .Where(e => buildingIds.Contains(e.BuildingId))
            .Where(e => e.ExpenseDate.Year == request.Year)
            .GroupBy(e => e.ExpenseDate.Month)
            .Select(g => new
            {
                Month = g.Key,
                Amount = g.Sum(e => e.Amount)
            })
            .ToListAsync(ct);

        var expenseByMonth = expenseBuckets.ToDictionary(x => x.Month, x => x.Amount);

        // Build monthly breakdown
        var months = new List<PnlMonthDto>();
        for (var month = 1; month <= 12; month++)
        {
            var operationalIncome = paymentByMonthType.GetValueOrDefault((month, PaymentType.RentPayment));
            var depositsReceived = paymentByMonthType.GetValueOrDefault((month, PaymentType.DepositIn));
            var depositsRefunded = paymentByMonthType.GetValueOrDefault((month, PaymentType.DepositRefund));
            var monthExpenses = expenseByMonth.GetValueOrDefault(month);

            // PNL-02: Net operational = operational income - expenses
            var netOperational = operationalIncome - monthExpenses;

            // PNL-03: Net cash flow = operational income + deposits received - deposits refunded - expenses
            var netCashFlow = operationalIncome + depositsReceived - depositsRefunded - monthExpenses;

            months.Add(new PnlMonthDto(
                month,
                operationalIncome,
                depositsReceived,
                depositsRefunded,
                monthExpenses,
                netOperational,
                netCashFlow));
        }

        return new PnlReportDto(request.BuildingId, request.Year, months);
    }
}
