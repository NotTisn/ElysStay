using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.RoomServices.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.RoomServices.Queries;

public record GetRoomServicesQuery(Guid RoomId) : IRequest<List<RoomServiceDto>>;

public class GetRoomServicesQueryHandler : IRequestHandler<GetRoomServicesQuery, List<RoomServiceDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IBuildingScopeService _buildingScope;

    public GetRoomServicesQueryHandler(IApplicationDbContext db, IBuildingScopeService buildingScope)
    {
        _db = db;
        _buildingScope = buildingScope;
    }

    public async Task<List<RoomServiceDto>> Handle(GetRoomServicesQuery request, CancellationToken cancellationToken)
    {
        var room = await _db.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoomId, cancellationToken)
            ?? throw new NotFoundException($"Room {request.RoomId} not found.");

        await _buildingScope.AuthorizeAsync(room.BuildingId, cancellationToken);

        // Get all active building services
        var buildingServices = await _db.Services
            .AsNoTracking()
            .Where(s => s.BuildingId == room.BuildingId && s.IsActive)
            .ToListAsync(cancellationToken);

        // Get existing room overrides
        var roomOverrides = await _db.RoomServices
            .AsNoTracking()
            .Where(rs => rs.RoomId == request.RoomId)
            .ToDictionaryAsync(rs => rs.ServiceId, cancellationToken);

        return buildingServices.Select(s =>
        {
            roomOverrides.TryGetValue(s.Id, out var ov);
            return new RoomServiceDto
            {
                ServiceId = s.Id,
                ServiceName = s.Name,
                Unit = s.Unit,
                BuildingUnitPrice = s.UnitPrice,
                IsMetered = s.IsMetered,
                IsEnabled = ov?.IsEnabled ?? true,
                OverrideUnitPrice = ov?.OverrideUnitPrice,
                OverrideQuantity = ov?.OverrideQuantity,
                EffectiveUnitPrice = ov?.OverrideUnitPrice ?? s.UnitPrice
            };
        })
        .OrderBy(x => x.ServiceName)
        .ToList();
    }
}
