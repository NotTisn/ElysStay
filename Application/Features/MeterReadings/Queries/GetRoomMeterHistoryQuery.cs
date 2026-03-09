using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.MeterReadings.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.MeterReadings.Queries;

/// <summary>
/// Gets meter reading history for a specific room.
/// Owner/Staff/Tenant (own room).
/// </summary>
public record GetRoomMeterHistoryQuery : IRequest<IReadOnlyList<MeterReadingDto>>
{
    public required Guid RoomId { get; init; }
}

public class GetRoomMeterHistoryQueryHandler : IRequestHandler<GetRoomMeterHistoryQuery, IReadOnlyList<MeterReadingDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetRoomMeterHistoryQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MeterReadingDto>> Handle(GetRoomMeterHistoryQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var room = await _db.Rooms
            .AsNoTracking()
            .Include(r => r.Building!)
            .FirstOrDefaultAsync(r => r.Id == request.RoomId, cancellationToken)
            ?? throw new NotFoundException("Room", request.RoomId);

        // Authorization
        if (_currentUser.IsOwner)
        {
            if (room.Building!.OwnerId != userId)
                throw new ForbiddenException("You do not own this building.");
        }
        else if (_currentUser.IsStaff)
        {
            var isAssigned = await _db.StaffAssignments
                .AnyAsync(sa => sa.BuildingId == room.BuildingId && sa.StaffId == userId, cancellationToken);
            if (!isAssigned)
                throw new ForbiddenException("You are not assigned to this building.");
        }
        else if (_currentUser.IsTenant)
        {
            var hasTenantAccess = await _db.Contracts
                .AnyAsync(c => c.RoomId == request.RoomId &&
                              c.Status == Domain.Enums.ContractStatus.Active &&
                              (c.TenantUserId == userId || c.ContractTenants.Any(ct => ct.TenantUserId == userId && ct.MoveOutDate == null)),
                          cancellationToken);
            if (!hasTenantAccess)
                throw new ForbiddenException("You do not have access to this room's meter readings.");
        }

        return await _db.MeterReadings
            .AsNoTracking()
            .Where(mr => mr.RoomId == request.RoomId)
            .OrderByDescending(mr => mr.BillingYear)
            .ThenByDescending(mr => mr.BillingMonth)
            .ThenBy(mr => mr.Service!.Name)
            .Select(mr => new MeterReadingDto
            {
                Id = mr.Id,
                RoomId = mr.RoomId,
                RoomNumber = room.RoomNumber, // already loaded
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
