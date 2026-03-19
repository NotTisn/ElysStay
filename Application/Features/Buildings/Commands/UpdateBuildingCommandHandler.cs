using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Buildings.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Buildings.Commands;

public class UpdateBuildingCommandHandler : IRequestHandler<UpdateBuildingCommand, BuildingDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateBuildingCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<BuildingDto> Handle(UpdateBuildingCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var building = await _db.Buildings
            .FirstOrDefaultAsync(b => b.Id == request.Id && b.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy tòa nhà {request.Id}.");

        // Only the owner of this building can update it
        if (building.OwnerId != userId)
            throw new ForbiddenException("Bạn không sở hữu tòa nhà này.");

        // Partial update: apply only non-null fields
        if (request.Name is not null)
            building.Name = request.Name.Trim();

        if (request.Address is not null)
            building.Address = request.Address.Trim();

        if (request.Description is not null)
            building.Description = request.Description.Trim();

        if (request.TotalFloors.HasValue)
        {
            // Prevent reducing TotalFloors below existing rooms' Floor values
            var maxFloorInUse = await _db.Rooms
                .Where(r => r.BuildingId == request.Id && r.DeletedAt == null)
                .Select(r => (int?)r.Floor)
                .MaxAsync(cancellationToken);

            if (maxFloorInUse.HasValue && request.TotalFloors.Value < maxFloorInUse.Value)
                throw new BadRequestException(
                    $"Cannot reduce total floors to {request.TotalFloors.Value}: room(s) exist on floor {maxFloorInUse.Value}.");

            building.TotalFloors = request.TotalFloors.Value;
        }

        if (request.InvoiceDueDay.HasValue)
            building.InvoiceDueDay = request.InvoiceDueDay.Value;

        building.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return new BuildingDto
        {
            Id = building.Id,
            OwnerId = building.OwnerId,
            Name = building.Name,
            Address = building.Address,
            Description = building.Description,
            TotalFloors = building.TotalFloors,
            InvoiceDueDay = building.InvoiceDueDay,
            CreatedAt = building.CreatedAt,
            UpdatedAt = building.UpdatedAt
        };
    }
}
