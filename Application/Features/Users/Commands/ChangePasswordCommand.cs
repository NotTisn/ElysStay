using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;

namespace Application.Features.Users.Commands;

/// <summary>
/// PUT /users/me/password — Change the authenticated user's password.
/// Verifies the current password via Keycloak ROPC, then sets the new one via admin API.
/// Auth: ALL authenticated users.
/// </summary>
public record ChangePasswordCommand : IRequest
{
    public required string CurrentPassword { get; init; }
    public required string NewPassword { get; init; }
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IKeycloakAdminService _keycloak;

    public ChangePasswordCommandHandler(ICurrentUserService currentUser, IKeycloakAdminService keycloak)
    {
        _currentUser = currentUser;
        _keycloak = keycloak;
    }

    public async Task Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var keycloakId = _currentUser.KeycloakId;
        var email = _currentUser.Email;

        // Verify current password via Keycloak Direct Access Grant
        var isValid = await _keycloak.VerifyPasswordAsync(email, request.CurrentPassword, ct);
        if (!isValid)
            throw new BadRequestException("Current password is incorrect.");

        // Set new password via Keycloak Admin API
        await _keycloak.ChangePasswordAsync(keycloakId, request.NewPassword, ct);
    }
}
