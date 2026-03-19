using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Auth;

/// <summary>
/// Verifies the current user has access to a specific building.
/// Owner: only own buildings.
/// Staff: must have a StaffAssignment for the building.
/// Tenant: denied (tenant access is handled per-endpoint).
/// </summary>
public class BuildingScopeService : IBuildingScopeService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public BuildingScopeService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task AuthorizeAsync(Guid buildingId, CancellationToken ct = default)
    {
        if (_currentUser.IsOwner)
        {
            var userId = _currentUser.GetRequiredUserId();
            var ownsBuilding = await _db.Buildings
                .AnyAsync(b => b.Id == buildingId && b.OwnerId == userId, ct);

            if (!ownsBuilding)
                throw new ForbiddenException("You do not own this building.");

            return;
        }

        if (_currentUser.IsStaff)
        {
            var userId = _currentUser.GetRequiredUserId();
            var isAssigned = await _db.StaffAssignments
                .AnyAsync(sa => sa.BuildingId == buildingId && sa.StaffId == userId, ct);

            if (!isAssigned)
                throw new ForbiddenException("You are not assigned to this building.");

            return;
        }

        // Tenants and other roles are denied at building scope
        throw new ForbiddenException();
    }
}
