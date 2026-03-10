using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Users.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Users.Commands;

/// <summary>
/// POST /users/tenants — Create a tenant account in Keycloak + local DB.
/// Auto-creates an empty TenantProfile (TP-01).
/// Auth: OWNER or STAFF.
/// </summary>
public record CreateTenantCommand : IRequest<UserDto>
{
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public string? Phone { get; init; }
    public string? Password { get; init; }
}

public class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, UserDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IKeycloakAdminService _keycloak;

    public CreateTenantCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IKeycloakAdminService keycloak)
    {
        _db = db;
        _currentUser = currentUser;
        _keycloak = keycloak;
    }

    public async Task<UserDto> Handle(CreateTenantCommand request, CancellationToken ct)
    {
        // Auth: Owner or Staff
        if (_currentUser.IsTenant)
            throw new ForbiddenException("Tenants cannot create other tenants.");

        // UQ-07: Check email uniqueness in local DB
        var emailExists = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == request.Email, ct);
        if (emailExists)
            throw new ConflictException("A user with this email already exists.", "DUPLICATE_EMAIL");

        // Generate random password if not provided (spec: "Không có password → tạo ngẫu nhiên")
        var password = request.Password ?? GenerateRandomPassword();

        // Create in Keycloak first — if this fails, no local record is created
        var keycloakId = await _keycloak.CreateUserAsync(
            request.Email.Trim(),
            request.FullName.Trim(),
            password,
            UserRole.Tenant.ToString(),
            ct);

        // Create in local DB
        var user = new User
        {
            KeycloakId = keycloakId,
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            Phone = request.Phone?.Trim(),
            Role = UserRole.Tenant,
            Status = UserStatus.Active
        };
        _db.Users.Add(user);

        // TP-01: Auto-create empty TenantProfile
        _db.TenantProfiles.Add(new TenantProfile { UserId = user.Id });

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

    private static string GenerateRandomPassword()
    {
        // Mix of uppercase, lowercase, digits, and symbol — satisfies AUTH-03 (min 8 chars)
        const string pool = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$";
        return new string(Random.Shared.GetItems<char>(pool.AsSpan(), 12));
    }
}
