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
public record CreateTenantCommand : IRequest<CreateUserResultDto>
{
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public string? Phone { get; init; }
    public string? Password { get; init; }
}

public class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, CreateUserResultDto>
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

    public async Task<CreateUserResultDto> Handle(CreateTenantCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedFullName = request.FullName.Trim();
        var normalizedPhone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        // Auth: Owner or Staff
        if (_currentUser.IsTenant)
            throw new ForbiddenException("Khách thuê không thể tạo khách thuê khác.");

        // UQ-07: Check email uniqueness in local DB
        var emailExists = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail, ct);
        if (emailExists)
            throw new ConflictException("Email này đã được sử dụng.", "DUPLICATE_EMAIL");

        // Phone uniqueness (IX_Users_Phone) — check before creating in Keycloak so a
        // duplicate phone fails cleanly instead of leaving an orphaned Keycloak user.
        if (normalizedPhone is not null)
        {
            var phoneExists = await _db.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Phone == normalizedPhone, ct);
            if (phoneExists)
                throw new ConflictException("Số điện thoại này đã được sử dụng.", "DUPLICATE_PHONE");
        }

        // Generate random password if not provided (spec: "Không có password → tạo ngẫu nhiên")
        var password = request.Password ?? GenerateRandomPassword();

        // Create in Keycloak first — if this fails, no local record is created
        var keycloakId = await _keycloak.CreateUserAsync(
            normalizedEmail,
            normalizedFullName,
            password,
            UserRole.Tenant.ToString().ToLowerInvariant(),
            ct);

        // Create in local DB
        var user = new User
        {
            KeycloakId = keycloakId,
            Email = normalizedEmail,
            FullName = normalizedFullName,
            Phone = normalizedPhone,
            Role = UserRole.Tenant,
            Status = UserStatus.Active
        };
        _db.Users.Add(user);

        // TP-01: Auto-create empty TenantProfile
        _db.TenantProfiles.Add(new TenantProfile { UserId = user.Id });

        await _db.SaveChangesAsync(ct);

        return new CreateUserResultDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            CreatedAt = user.CreatedAt,
            TemporaryPassword = password,
        };
    }

    private static string GenerateRandomPassword()
    {
        // Mix of uppercase, lowercase, digits, and symbol — satisfies AUTH-03 (min 8 chars)
        const string pool = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$";
        return new string(Random.Shared.GetItems<char>(pool.AsSpan(), 12));
    }
}
