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
        var data = await _db.Buildings
            .AsNoTracking()
            .Where(b => b.Id == request.Id)
            .Select(b => new
            {
                Building = b,
                TotalRooms = b.Rooms.Count(),
                OccupiedRooms = b.Rooms.Count(r => r.Status == RoomStatus.Occupied)
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Building {request.Id} not found.");

        var building = data.Building;

        // AUTH-05: Building-scoped check
        await _buildingScope.AuthorizeAsync(building.Id, cancellationToken);

        var occupancyRate = data.TotalRooms > 0 ? (double)data.OccupiedRooms / data.TotalRooms : 0;

        return new BuildingDetailDto
        {
            Id = building.Id,
            OwnerId = building.OwnerId,
            Name = building.Name,
            Address = building.Address,
            Description = building.Description,
            TotalFloors = building.TotalFloors,
            InvoiceDueDay = building.InvoiceDueDay,
            TotalRooms = data.TotalRooms,
            OccupancyRate = Math.Round(occupancyRate, 2),
            CreatedAt = building.CreatedAt,
            UpdatedAt = building.UpdatedAt
        };
    }
}
