using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Tests.Integration
{
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string UserIdHeader = "X-Test-User-Id";
        public const string UserEmailHeader = "X-Test-User-Email";
        public const string UserNameHeader = "X-Test-User-Name";
        public const string UserRoleHeader = "X-Test-User-Role";

#pragma warning disable CS0618
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }
#pragma warning restore CS0618

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var keycloakId = GetHeaderValue(UserIdHeader) ?? "test-keycloak";
            var email = GetHeaderValue(UserEmailHeader) ?? "owner@example.com";
            var fullName = GetHeaderValue(UserNameHeader) ?? "Test Owner";
            var role = NormalizeRole(GetHeaderValue(UserRoleHeader) ?? "Owner");

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, keycloakId),
                new Claim("sub", keycloakId),
                new Claim(ClaimTypes.Name, fullName),
                new Claim("preferred_username", fullName),
                new Claim(ClaimTypes.Email, email),
                new Claim("email", email),
                new Claim(ClaimTypes.Role, role),
                new Claim("realm_access", JsonSerializer.Serialize(new { roles = new[] { role.ToLowerInvariant() } }))
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            var result = AuthenticateResult.Success(ticket);

            return Task.FromResult(result);
        }

        private string? GetHeaderValue(string headerName)
            => Request.Headers.TryGetValue(headerName, out var values) ? values.ToString() : null;

        private static string NormalizeRole(string role)
        {
            role = role.Trim();
            if (role.Length == 0)
                return "Owner";

            return role.Length == 1
                ? role.ToUpperInvariant()
                : char.ToUpperInvariant(role[0]) + role[1..];
        }
    }
}
