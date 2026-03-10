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

    /// <summary>
    /// Sets a new password for a Keycloak user via the Admin API.
    /// </summary>
    Task ChangePasswordAsync(string keycloakUserId, string newPassword, CancellationToken ct = default);

    /// <summary>
    /// Verifies a user's password by attempting a Direct Access Grant (ROPC).
    /// Returns true if the credentials are valid, false otherwise.
    /// </summary>
    Task<bool> VerifyPasswordAsync(string username, string password, CancellationToken ct = default);
}
