using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Users.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Users.Commands;

/// <summary>
/// POST /users/staff — Create a staff account in Keycloak + local DB.
/// Replaces spec's POST /auth/register-staff (auth is Keycloak-native).
/// Auth: OWNER only.
/// </summary>
public record CreateStaffCommand : IRequest<UserDto>
{
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public string? Phone { get; init; }
    public required string Password { get; init; }
}

public class CreateStaffCommandHandler : IRequestHandler<CreateStaffCommand, UserDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IKeycloakAdminService _keycloak;

    public CreateStaffCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IKeycloakAdminService keycloak)
    {
        _db = db;
        _currentUser = currentUser;
        _keycloak = keycloak;
    }

    public async Task<UserDto> Handle(CreateStaffCommand request, CancellationToken ct)
    {
        // Auth: Owner only
        if (!_currentUser.IsOwner)
            throw new ForbiddenException("Only the owner can create staff accounts.");

        // UQ-07: Email uniqueness
        var emailExists = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == request.Email, ct);
        if (emailExists)
            throw new ConflictException("A user with this email already exists.", "DUPLICATE_EMAIL");

        // Create in Keycloak first
        var keycloakId = await _keycloak.CreateUserAsync(
            request.Email.Trim(),
            request.FullName.Trim(),
            request.Password,
            UserRole.Staff.ToString(),
            ct);

        // Create in local DB
        var user = new User
        {
            KeycloakId = keycloakId,
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            Phone = request.Phone?.Trim(),
            Role = UserRole.Staff,
            Status = UserStatus.Active
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            CreatedAt = user.CreatedAt
        };
    }
}
