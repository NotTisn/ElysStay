using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Buildings.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Buildings.Queries;

public class GetBuildingByIdQueryHandler : IRequestHandler<GetBuildingByIdQuery, BuildingDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IBuildingScopeService _buildingScope;

    public GetBuildingByIdQueryHandler(IApplicationDbContext db, IBuildingScopeService buildingScope)
    {
        _db = db;
        _buildingScope = buildingScope;
    }

    public async Task<BuildingDetailDto> Handle(GetBuildingByIdQuery request, CancellationToken cancellationToken)
    {
        var building = await _db.Buildings
            .AsNoTracking()
            .Include(b => b.Rooms)
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Building {request.Id} not found.");

        // AUTH-05: Building-scoped check
        await _buildingScope.AuthorizeAsync(building.Id, cancellationToken);

        var totalRooms = building.Rooms.Count;
        var occupiedRooms = building.Rooms.Count(r => r.Status == RoomStatus.Occupied);
        var occupancyRate = totalRooms > 0 ? (double)occupiedRooms / totalRooms : 0;

        return new BuildingDetailDto
        {
            Id = building.Id,
            OwnerId = building.OwnerId,
            Name = building.Name,
            Address = building.Address,
            Description = building.Description,
            TotalFloors = building.TotalFloors,
            InvoiceDueDay = building.InvoiceDueDay,
            TotalRooms = totalRooms,
            OccupancyRate = Math.Round(occupancyRate, 2),
            CreatedAt = building.CreatedAt,
            UpdatedAt = building.UpdatedAt
        };
    }
}
