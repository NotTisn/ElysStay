using Application.Common.Interfaces;
using Domain.Enums;

namespace ElysStay.Tests.Integration.TestDoubles;

internal sealed class FakeCurrentUserService : ICurrentUserService
{
    public string KeycloakId { get; init; } = "test-keycloak";
    public Guid? UserId { get; init; }
    public UserRole Role { get; init; } = UserRole.Owner;
    public string Email { get; init; } = "owner@example.com";
    public string FullName { get; init; } = "Test Owner";
    public bool IsAuthenticated => UserId.HasValue;

    public Guid GetRequiredUserId()
        => UserId ?? throw new InvalidOperationException("UserId is not configured for the test current user.");
}

internal sealed class AllowAllBuildingScopeService : IBuildingScopeService
{
    public Task AuthorizeAsync(Guid buildingId, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class NoOpEmailService : IEmailService
{
    public Task<bool> TrySendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default)
        => Task.FromResult(true);
}