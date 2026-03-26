using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Infrastructure.Auth;

/// <summary>
/// Extracts realm roles from Keycloak's "realm_access" JWT claim
/// and adds them as standard ClaimTypes.Role claims.
/// </summary>
public class KeycloakClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return Task.FromResult(principal);

        var realmAccessClaim = identity.FindFirst("realm_access");
        if (realmAccessClaim is null)
            return Task.FromResult(principal);

        try
        {
            using var doc = JsonDocument.Parse(realmAccessClaim.Value);
            if (!doc.RootElement.TryGetProperty("roles", out var rolesElement))
                return Task.FromResult(principal);

            foreach (var role in rolesElement.EnumerateArray())
            {
                var roleValue = role.GetString();
                if (roleValue is null) continue;

                // Keycloak realm roles are lowercase (e.g. "owner", "staff", "tenant")
                // but ASP.NET Core [Authorize(Roles = "Owner,Staff")] is case-sensitive.
                // Normalize to PascalCase so both sides match.
                var normalized = char.ToUpperInvariant(roleValue[0]) + roleValue[1..];

                if (!identity.HasClaim(ClaimTypes.Role, normalized))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, normalized));
                }
            }
        }
        catch (JsonException)
        {
            // Malformed realm_access — ignore silently
        }

        return Task.FromResult(principal);
    }
}
