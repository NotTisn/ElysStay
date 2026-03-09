using Domain.Enums;

namespace Application.Common.Interfaces;

/// <summary>
/// Provides the identity of the currently authenticated user.
/// Scoped per-request. Populated from JWT claims + DB lookup.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Keycloak subject ID (from "sub" claim).</summary>
    string KeycloakId { get; }

    /// <summary>Database user ID. Null if not yet provisioned.</summary>
    Guid? UserId { get; }

    /// <summary>User role from the realm.</summary>
    UserRole Role { get; }

    /// <summary>Email from JWT claims.</summary>
    string Email { get; }

    /// <summary>Full name from JWT claims.</summary>
    string FullName { get; }

    bool IsOwner => Role == UserRole.Owner;
    bool IsStaff => Role == UserRole.Staff;
    bool IsTenant => Role == UserRole.Tenant;
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the DB UserId, throwing ForbiddenException if not provisioned.
    /// </summary>
    Guid GetRequiredUserId();
}
