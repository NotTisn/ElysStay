using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Users.Commands;

/// <summary>
/// PATCH /users/{id}/status — Activate or deactivate a user.
/// Syncs the enabled state with Keycloak.
/// Auth: OWNER only. Cannot change own status or other owners.
/// </summary>
public record ChangeUserStatusCommand : IRequest
{
    public Guid UserId { get; init; }
    public required UserStatus Status { get; init; }
}

public class ChangeUserStatusCommandHandler : IRequestHandler<ChangeUserStatusCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IKeycloakAdminService _keycloak;

    public ChangeUserStatusCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IKeycloakAdminService keycloak)
    {
        _db = db;
        _currentUser = currentUser;
        _keycloak = keycloak;
    }

    public async Task Handle(ChangeUserStatusCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsOwner)
            throw new ForbiddenException("Only the owner can change user status.");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new NotFoundException("User", request.UserId);

        var callerId = _currentUser.GetRequiredUserId();

        // Guard: can't deactivate yourself
        if (user.Id == callerId)
            throw new BadRequestException("You cannot change your own status.");

        // Guard: can't change status of another owner
        if (user.Role == UserRole.Owner)
            throw new ForbiddenException("Cannot change the status of another owner.");

        // Update local DB
        user.Status = request.Status;
        user.UpdatedAt = DateTime.UtcNow;

        // AUTH-02: Sync enabled state with Keycloak
        if (user.KeycloakId is not null)
        {
            var enabled = request.Status == UserStatus.Active;
            await _keycloak.SetUserEnabledAsync(user.KeycloakId, enabled, ct);
        }

        await _db.SaveChangesAsync(ct);
    }
}
