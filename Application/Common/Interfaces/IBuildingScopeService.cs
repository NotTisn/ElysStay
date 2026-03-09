namespace Application.Common.Interfaces;

/// <summary>
/// Checks whether the current user has access to resources scoped to a building.
/// Throws ForbiddenException if not authorized.
/// </summary>
public interface IBuildingScopeService
{
    /// <summary>
    /// Verifies the current user can access resources in the given building.
    /// Owner: always allowed.
    /// Staff: must have StaffAssignment for buildingId.
    /// Tenant: always denied (tenant access is per-endpoint).
    /// </summary>
    Task AuthorizeAsync(Guid buildingId, CancellationToken ct = default);
}
