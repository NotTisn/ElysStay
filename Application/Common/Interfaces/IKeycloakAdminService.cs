namespace Application.Common.Interfaces;

/// <summary>
/// Abstraction for Keycloak Admin REST API operations.
/// Implementation lives in Infrastructure.
/// </summary>
public interface IKeycloakAdminService
{
    /// <summary>
    /// Creates a user in Keycloak and assigns the specified realm role.
    /// Returns the Keycloak user ID.
    /// </summary>
    Task<string> CreateUserAsync(string email, string fullName, string? password, string roleName, CancellationToken ct = default);

    /// <summary>
    /// Enables or disables a Keycloak user.
    /// </summary>
    Task SetUserEnabledAsync(string keycloakUserId, bool enabled, CancellationToken ct = default);
}
