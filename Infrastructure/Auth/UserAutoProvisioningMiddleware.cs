using System.Security.Claims;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Auth;

/// <summary>
/// Middleware that auto-provisions a DB User record on first authenticated request
/// if no User with the Keycloak subject ID exists yet.
/// Also resolves and caches the DB UserId on CurrentUserService for the request lifetime.
/// </summary>
public class UserAutoProvisioningMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserAutoProvisioningMiddleware> _logger;

    public UserAutoProvisioningMiddleware(RequestDelegate next, ILogger<UserAutoProvisioningMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext, CurrentUserService currentUserService)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var keycloakId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(keycloakId))
        {
            await _next(context);
            return;
        }

        // Lookup existing user by KeycloakId (IgnoreQueryFilters to find soft-deleted users too)
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId, context.RequestAborted);

        if (user is null)
        {
            // Auto-provision
            var role = ResolveRole(context.User);
            var email = context.User.FindFirstValue(ClaimTypes.Email)
                ?? context.User.FindFirstValue("email")
                ?? $"{keycloakId}@unknown";
            var fullName = context.User.FindFirstValue("name")
                ?? context.User.FindFirstValue("preferred_username")
                ?? "Unknown";
            var phone = context.User.FindFirstValue("phone_number");

            user = new User
            {
                KeycloakId = keycloakId,
                Email = email,
                FullName = fullName,
                Phone = phone,
                Role = role,
                Status = UserStatus.Active
            };

            dbContext.Users.Add(user);

            // Tenants get an empty profile (TP-01)
            if (role == UserRole.Tenant)
            {
                dbContext.TenantProfiles.Add(new TenantProfile
                {
                    UserId = user.Id
                });
            }

            await dbContext.SaveChangesAsync(context.RequestAborted);

            _logger.LogInformation("Auto-provisioned user {UserId} for Keycloak subject {KeycloakId} with role {Role}",
                user.Id, keycloakId, role);
        }

        // Cache the resolved UserId for this request
        currentUserService.UserId = user.Id;

        await _next(context);
    }

    private static UserRole ResolveRole(ClaimsPrincipal principal)
    {
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Priority: owner > staff > tenant
        if (roles.Contains("owner")) return UserRole.Owner;
        if (roles.Contains("staff")) return UserRole.Staff;
        return UserRole.Tenant;
    }
}
