using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.StaffAssignments.Commands;

public record UnassignStaffCommand(Guid BuildingId, Guid StaffId) : IRequest;

public class UnassignStaffCommandHandler : IRequestHandler<UnassignStaffCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UnassignStaffCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UnassignStaffCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsOwner)
            throw new ForbiddenException("Only the owner can unassign staff.");

        var userId = _currentUser.GetRequiredUserId();

        // Verify ownership
        var building = await _db.Buildings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BuildingId, cancellationToken)
            ?? throw new NotFoundException($"Building {request.BuildingId} not found.");

        if (building.OwnerId != userId)
            throw new ForbiddenException("You do not own this building.");

        var assignment = await _db.StaffAssignments
            .FirstOrDefaultAsync(sa => sa.BuildingId == request.BuildingId && sa.StaffId == request.StaffId, cancellationToken)
            ?? throw new NotFoundException("Staff assignment not found.");

        _db.StaffAssignments.Remove(assignment);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
