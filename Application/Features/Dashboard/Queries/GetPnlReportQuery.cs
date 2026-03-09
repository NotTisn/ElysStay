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
            throw new ForbiddenException("Only owners can access P&L reports.");

        // Get owner's buildings
        var buildingIds = await _db.Buildings
            .Where(b => b.OwnerId == userId && b.DeletedAt == null)
            .Select(b => b.Id)
            .ToListAsync(ct);

        if (request.BuildingId.HasValue)
        {
            if (!buildingIds.Contains(request.BuildingId.Value))
                throw new ForbiddenException("You do not own this building.");
            buildingIds = [request.BuildingId.Value];
        }

        var yearStart = new DateTime(request.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd = yearStart.AddYears(1);

        // Load all payments for the year in these buildings
        // PNL-04: Exclude payments tied to VOID invoices
        var payments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.PaidAt >= yearStart && p.PaidAt < yearEnd)
            .Where(p =>
                // Contract-level payments (deposits)
                (p.ContractId != null && buildingIds.Contains(p.Contract!.Room!.BuildingId))
                ||
                // Invoice-level payments (rent)
                (p.InvoiceId != null && p.Invoice!.Status != InvoiceStatus.Void
                    && buildingIds.Contains(p.Invoice.Contract!.Room!.BuildingId)))
            .Select(p => new
            {
                Month = p.PaidAt.Month,
                p.Type,
                p.Amount
            })
            .ToListAsync(ct);

        // Include orphan deposit payments from cancelled reservations (ContractId=null, InvoiceId=null)
        // These are DEPOSIT_IN/DEPOSIT_REFUND payments that belong to rooms in these buildings
        var orphanDepositPayments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.PaidAt >= yearStart && p.PaidAt < yearEnd)
            .Where(p => p.ContractId == null && p.InvoiceId == null)
            .Where(p => p.Type == PaymentType.DepositIn || p.Type == PaymentType.DepositRefund)
            .Where(p => p.Note != null && p.Note.Contains("reservation"))
            .Select(p => new
            {
                Month = p.PaidAt.Month,
                p.Type,
                p.Amount
            })
            .ToListAsync(ct);

        // Merge orphan payments into the main payments list
        payments.AddRange(orphanDepositPayments);

        // Load expenses for the year
        var expenses = await _db.Expenses
            .AsNoTracking()
            .Where(e => buildingIds.Contains(e.BuildingId))
            .Where(e => e.ExpenseDate.Year == request.Year)
            .Select(e => new
            {
                Month = e.ExpenseDate.Month,
                e.Amount
            })
            .ToListAsync(ct);

        // Build monthly breakdown
        var months = new List<PnlMonthDto>();
        for (var month = 1; month <= 12; month++)
        {
            var monthPayments = payments.Where(p => p.Month == month).ToList();

            var operationalIncome = monthPayments
                .Where(p => p.Type == PaymentType.RentPayment)
                .Sum(p => p.Amount);

            var depositsReceived = monthPayments
                .Where(p => p.Type == PaymentType.DepositIn)
                .Sum(p => p.Amount);

            var depositsRefunded = monthPayments
                .Where(p => p.Type == PaymentType.DepositRefund)
                .Sum(p => p.Amount);

            var monthExpenses = expenses
                .Where(e => e.Month == month)
                .Sum(e => e.Amount);

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
