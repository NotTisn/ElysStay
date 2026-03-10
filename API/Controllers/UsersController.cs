using Application.Features.Dashboard.Queries;
using Application.Features.Users.Commands;
using Application.Features.Users.Queries;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers;

/// <summary>
/// User profile, management, and dashboard endpoints.
/// </summary>
[Authorize]
[Route("api/v1/users")]
public class UsersController : BaseApiController
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ───────────────────────── /me (self) ─────────────────────────

    /// <summary>
    /// GET /users/me — Returns the authenticated user's profile.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCurrentUserQuery(), ct);
        return OkResponse(result);
    }

    /// <summary>
    /// PUT /users/me — Update own fullName and/or phone.
    /// </summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateCurrentUser([FromBody] UpdateCurrentUserCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return OkResponse(result);
    }

    /// <summary>
    /// PUT /users/me/password — Change the authenticated user's password.
    /// Verifies current password before setting new one.
    /// </summary>
    [HttpPut("me/password")]
    [EnableRateLimiting("sensitive")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return OkResponse<object?>(null, "Password changed successfully");
    }

    /// <summary>
    /// GET /users/me/dashboard — Role-based homepage summary.
    /// </summary>
    [HttpGet("me/dashboard")]
    public async Task<IActionResult> GetMyDashboard(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyDashboardQuery(), ct);
        return OkResponse(result);
    }

    // ───────────────────── User Management ─────────────────────

    /// <summary>
    /// GET /users/tenants — Paginated list of tenants.
    /// Auth: OWNER or STAFF.
    /// </summary>
    [HttpGet("tenants")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> GetTenants(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "createdAt:desc",
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetUsersQuery
        {
            RoleFilter = UserRole.Tenant,
            Search = search,
            Page = page,
            PageSize = pageSize,
            Sort = sort
        }, ct);
        return PagedOk(result);
    }

    /// <summary>
    /// GET /users/staff — List of staff members.
    /// Auth: OWNER only.
    /// </summary>
    [HttpGet("staff")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> GetStaff(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "createdAt:desc",
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetUsersQuery
        {
            RoleFilter = UserRole.Staff,
            Page = page,
            PageSize = pageSize,
            Sort = sort
        }, ct);
        return PagedOk(result);
    }

    /// <summary>
    /// GET /users/{id} — User detail.
    /// Auth: OWNER sees anyone, STAFF sees tenants + self, TENANT sees self only.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUserByIdQuery(id), ct);
        return OkResponse(result);
    }

    /// <summary>
    /// POST /users/tenants — Create a tenant account.
    /// Auth: OWNER or STAFF.
    /// </summary>
    [HttpPost("tenants")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedResponse(result, message: "Tenant created successfully");
    }

    /// <summary>
    /// POST /users/staff — Create a staff account (replaces /auth/register-staff).
    /// Auth: OWNER only.
    /// </summary>
    [HttpPost("staff")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedResponse(result, message: "Staff account created successfully");
    }

    /// <summary>
    /// PATCH /users/{id}/status — Activate or deactivate a user.
    /// Auth: OWNER only.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> ChangeUserStatus(Guid id, [FromBody] ChangeUserStatusRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ChangeUserStatusCommand
        {
            UserId = id,
            Status = request.Status
        }, ct);
        return OkResponse<object?>(null, "User status updated successfully");
    }
}

/// <summary>
/// Request body for PATCH /users/{id}/status.
/// Separated from command to allow route ID merging.
/// </summary>
public record ChangeUserStatusRequest
{
    public required UserStatus Status { get; init; }
}
