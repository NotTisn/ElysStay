using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.RoomServices.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.RoomServices.Commands;

/// <summary>
/// Bulk update room service overrides (PUT semantics).
/// Services not in the list revert to building defaults (their RoomService rows are deleted).
/// </summary>
public record UpdateRoomServicesCommand : IRequest<List<RoomServiceDto>>
{
    public Guid RoomId { get; init; }
    public required List<RoomServiceEntry> Services { get; init; }
}

public record RoomServiceEntry
{
    public required Guid ServiceId { get; init; }
    public required bool IsEnabled { get; init; }
    public decimal? OverrideUnitPrice { get; init; }
    public decimal? OverrideQuantity { get; init; }
}

public class UpdateRoomServicesCommandHandler : IRequestHandler<UpdateRoomServicesCommand, List<RoomServiceDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IBuildingScopeService _buildingScope;

    public UpdateRoomServicesCommandHandler(IApplicationDbContext db, IBuildingScopeService buildingScope)
    {
        _db = db;
        _buildingScope = buildingScope;
    }

    public async Task<List<RoomServiceDto>> Handle(UpdateRoomServicesCommand request, CancellationToken cancellationToken)
    {
        var room = await _db.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoomId, cancellationToken)
            ?? throw new NotFoundException($"Room {request.RoomId} not found.");

        await _buildingScope.AuthorizeAsync(room.BuildingId, cancellationToken);

        // Validate all serviceIds belong to this building
        var validServiceIds = await _db.Services
            .Where(s => s.BuildingId == room.BuildingId && s.IsActive)
            .Select(s => s.Id)
            .ToHashSetAsync(cancellationToken);

        foreach (var entry in request.Services)
        {
            if (!validServiceIds.Contains(entry.ServiceId))
                throw new BadRequestException($"Service {entry.ServiceId} does not belong to this building or is inactive.");
        }

        // Remove all existing overrides for this room
        var existingOverrides = await _db.RoomServices
            .Where(rs => rs.RoomId == request.RoomId)
            .ToListAsync(cancellationToken);

        _db.RoomServices.RemoveRange(existingOverrides);

        // Create new overrides from request
        var newServiceIds = request.Services.Select(s => s.ServiceId).ToHashSet();
        foreach (var entry in request.Services)
        {
            // Only create RoomService rows for entries that differ from defaults
            // (disabled, or have overrides)
            if (!entry.IsEnabled || entry.OverrideUnitPrice.HasValue || entry.OverrideQuantity.HasValue)
            {
                _db.RoomServices.Add(new Domain.Entities.RoomService
                {
                    RoomId = request.RoomId,
                    ServiceId = entry.ServiceId,
                    IsEnabled = entry.IsEnabled,
                    OverrideUnitPrice = entry.OverrideUnitPrice,
                    OverrideQuantity = entry.OverrideQuantity
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Return the merged view
        var buildingServices = await _db.Services
            .AsNoTracking()
            .Where(s => s.BuildingId == room.BuildingId && s.IsActive)
            .ToListAsync(cancellationToken);

        var updatedOverrides = await _db.RoomServices
            .AsNoTracking()
            .Where(rs => rs.RoomId == request.RoomId)
            .ToDictionaryAsync(rs => rs.ServiceId, cancellationToken);

        return buildingServices.Select(s =>
        {
            updatedOverrides.TryGetValue(s.Id, out var ov);
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
