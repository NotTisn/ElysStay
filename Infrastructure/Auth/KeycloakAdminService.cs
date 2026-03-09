using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Auth;

/// <summary>
/// Keycloak Admin REST API client.
/// Uses client credentials grant for service account token.
/// Token is cached and auto-refreshed on expiry.
/// </summary>
public class KeycloakAdminService : IKeycloakAdminService
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakAdminOptions _options;
    private readonly ILogger<KeycloakAdminService> _logger;
    private string? _accessToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public KeycloakAdminService(HttpClient httpClient, KeycloakAdminOptions options, ILogger<KeycloakAdminService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<string> CreateUserAsync(string email, string fullName, string? password, string roleName, CancellationToken ct = default)
    {
        await EnsureTokenAsync(ct);

        // 1. Create the user
        var createPayload = new
        {
            email,
            firstName = fullName,
            enabled = true,
            emailVerified = true,
            credentials = password is not null
                ? new[] { new { type = "password", value = password, temporary = false } }
                : Array.Empty<object>(),
            username = email
        };

        var createResponse = await PostAsync($"admin/realms/{_options.Realm}/users", createPayload, ct);
        createResponse.EnsureSuccessStatusCode();

        // 2. Get the created user by email to retrieve Keycloak ID
        var usersResponse = await GetAsync<List<KeycloakUserRepresentation>>(
            $"admin/realms/{_options.Realm}/users?email={Uri.EscapeDataString(email)}&exact=true", ct);

        var keycloakUser = usersResponse?.FirstOrDefault()
            ?? throw new InvalidOperationException($"Created user '{email}' not found in Keycloak.");

        var keycloakUserId = keycloakUser.Id;

        // 3. Get the realm role
        var roleResponse = await GetAsync<KeycloakRoleRepresentation>(
            $"admin/realms/{_options.Realm}/roles/{Uri.EscapeDataString(roleName)}", ct);

        if (roleResponse is null)
            throw new InvalidOperationException($"Realm role '{roleName}' not found in Keycloak.");

        // 4. Assign the role
        var assignPayload = new[] { new { id = roleResponse.Id, name = roleResponse.Name } };
        var assignResponse = await PostAsync(
            $"admin/realms/{_options.Realm}/users/{keycloakUserId}/role-mappings/realm", assignPayload, ct);
        assignResponse.EnsureSuccessStatusCode();

        _logger.LogInformation("Created Keycloak user {Email} with role {Role}, ID: {KeycloakId}",
            email, roleName, keycloakUserId);

        return keycloakUserId;
    }

    public async Task SetUserEnabledAsync(string keycloakUserId, bool enabled, CancellationToken ct = default)
    {
        await EnsureTokenAsync(ct);

        var payload = new { enabled };
        var response = await PutAsync($"admin/realms/{_options.Realm}/users/{keycloakUserId}", payload, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Set Keycloak user {KeycloakUserId} enabled={Enabled}", keycloakUserId, enabled);
    }

    private async Task EnsureTokenAsync(CancellationToken ct)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return;

        await _tokenLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
                return;

            var tokenEndpoint = $"realms/{_options.Realm}/protocol/openid-connect/token";
            var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret
            });

            var response = await _httpClient.PostAsync(tokenEndpoint, formContent, ct);
            response.EnsureSuccessStatusCode();

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
            _accessToken = tokenResponse!.AccessToken;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 30); // 30s buffer

            _logger.LogDebug("Acquired Keycloak admin token, expires at {Expiry}", _tokenExpiry);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object payload, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Content = JsonContent.Create(payload);
        return await _httpClient.SendAsync(request, ct);
    }

    private async Task<HttpResponseMessage> PutAsync(string path, object payload, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Content = JsonContent.Create(payload);
        return await _httpClient.SendAsync(request, ct);
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct);
    }

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn
    );

    private record KeycloakUserRepresentation(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("email")] string Email
    );

    private record KeycloakRoleRepresentation(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name
    );
}

/// <summary>
/// Configuration options for Keycloak Admin API.
/// </summary>
public class KeycloakAdminOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Realm { get; set; } = "elysstay";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
