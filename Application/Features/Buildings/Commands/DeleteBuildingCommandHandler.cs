using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Buildings.Commands;

public class DeleteBuildingCommandHandler : IRequestHandler<DeleteBuildingCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteBuildingCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteBuildingCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var building = await _db.Buildings
            .Include(b => b.Rooms)
                .ThenInclude(r => r.Contracts)
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Building {request.Id} not found.");

        // Only the owner can delete
        if (building.OwnerId != userId)
            throw new ForbiddenException("You do not own this building.");

        // SD-04: Block if any room has an active contract
        var hasActiveContract = building.Rooms
            .SelectMany(r => r.Contracts)
            .Any(c => c.Status == ContractStatus.Active);

        if (hasActiveContract)
            throw new ConflictException(
                "Cannot delete building: one or more rooms have active contracts.",
                "ACTIVE_CONTRACT_EXISTS");

        // Soft delete building and cascade to rooms
        var now = DateTime.UtcNow;
        building.DeletedAt = now;
        building.UpdatedAt = now;

        // Cascade soft-delete to all rooms in this building
        foreach (var room in building.Rooms.Where(r => r.DeletedAt == null))
        {
            room.DeletedAt = now;
            room.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
