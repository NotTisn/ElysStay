using System.Security.Claims;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Auth;

/// <summary>
/// Scoped service that resolves the current user from HttpContext claims.
/// DB user ID is resolved lazily and cached per-request.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _userId;
    private bool _userIdResolved;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string KeycloakId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new ForbiddenException("No authenticated user.");

    public string Email => Principal?.FindFirstValue(ClaimTypes.Email)
        ?? Principal?.FindFirstValue("email")
        ?? string.Empty;

    public string FullName => Principal?.FindFirstValue("name")
        ?? Principal?.FindFirstValue("preferred_username")
        ?? string.Empty;

    public UserRole Role
    {
        get
        {
            var roleClaim = Principal?.FindFirstValue(ClaimTypes.Role);
            if (roleClaim is not null && Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var role))
                return role;

            return UserRole.Tenant; // Default to least-privilege
        }
    }

    public Guid? UserId
    {
        get => _userId;
        internal set
        {
            _userId = value;
            _userIdResolved = true;
        }
    }

    public Guid GetRequiredUserId()
    {
        return _userId ?? throw new ForbiddenException("User account not provisioned.");
    }

    internal bool IsUserIdResolved => _userIdResolved;
}
