using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.MeterReadings.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.MeterReadings.Queries;

/// <summary>
/// Lists meter readings for a building/month.
/// Owner/Staff see all; Tenant sees own room only.
/// billingYear and billingMonth are required.
/// </summary>
public record GetMeterReadingsQuery : IRequest<IReadOnlyList<MeterReadingDto>>
{
    public Guid? BuildingId { get; init; }
    public required int BillingYear { get; init; }
    public required int BillingMonth { get; init; }
}

public class GetMeterReadingsQueryHandler : IRequestHandler<GetMeterReadingsQuery, IReadOnlyList<MeterReadingDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMeterReadingsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MeterReadingDto>> Handle(GetMeterReadingsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var query = _db.MeterReadings
            .AsNoTracking()
            .Where(mr => mr.BillingYear == request.BillingYear && mr.BillingMonth == request.BillingMonth)
            .AsQueryable();

        if (request.BuildingId.HasValue)
            query = query.Where(mr => mr.Room!.BuildingId == request.BuildingId.Value);

        // Scope by role
        if (_currentUser.IsOwner)
        {
            query = query.Where(mr => mr.Room!.Building!.OwnerId == userId);
        }
        else if (_currentUser.IsStaff)
        {
            query = query.Where(mr => mr.Room!.Building!.BuildingStaffs.Any(bs => bs.StaffId == userId));
        }
        else if (_currentUser.IsTenant)
        {
            // Tenant sees only readings for rooms where they have an active contract
            query = query.Where(mr => mr.Room!.Contracts.Any(c =>
                c.Status == Domain.Enums.ContractStatus.Active &&
                (c.TenantUserId == userId || c.ContractTenants.Any(ct => ct.TenantUserId == userId && ct.MoveOutDate == null))));
        }

        return await query
            .OrderBy(mr => mr.Room!.RoomNumber)
            .ThenBy(mr => mr.Service!.Name)
            .Select(mr => new MeterReadingDto
            {
                Id = mr.Id,
                RoomId = mr.RoomId,
                RoomNumber = mr.Room!.RoomNumber,
                ServiceId = mr.ServiceId,
                ServiceName = mr.Service!.Name,
                ServiceUnit = mr.Service.Unit,
                BillingYear = mr.BillingYear,
                BillingMonth = mr.BillingMonth,
                PreviousReading = mr.PreviousReading,
                CurrentReading = mr.CurrentReading,
                Consumption = mr.Consumption,
                CreatedAt = mr.CreatedAt,
                UpdatedAt = mr.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
