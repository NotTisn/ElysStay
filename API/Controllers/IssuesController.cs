using Application.Features.MaintenanceIssues.Commands;
using Application.Features.MaintenanceIssues.Queries;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Maintenance issue management.
/// </summary>
[Authorize]
[Route("api/v1/issues")]
public class IssuesController : BaseApiController
{
    private readonly IMediator _mediator;

    public IssuesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get a single issue by ID. All roles. Tenant sees own only (AUTH-06).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetIssueById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetIssueByIdQuery(id), ct);
        return OkResponse(result);
    }

    /// <summary>
    /// List issues (paginated). All roles. Tenant auto-filtered.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetIssues(
        [FromQuery] Guid? buildingId,
        [FromQuery] IssueStatus? status,
        [FromQuery] PriorityLevel? priority,
        [FromQuery] string? sortBy,
        [FromQuery] bool sortDesc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetIssuesQuery
        {
            BuildingId = buildingId,
            Status = status,
            Priority = priority,
            SortBy = sortBy,
            SortDesc = sortDesc,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, ct);
        return PagedOk(result);
    }

    /// <summary>
    /// Report a new maintenance issue. All roles.
    /// Tenant: buildingId auto-resolved from contract.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateIssue([FromBody] CreateIssueRequest request, CancellationToken ct)
    {
        var command = new CreateIssueCommand
        {
            BuildingId = request.BuildingId,
            RoomId = request.RoomId,
            Title = request.Title,
            Description = request.Description
        };

        var result = await _mediator.Send(command, ct);
        return CreatedResponse(result, message: "Issue reported successfully");
    }

    /// <summary>
    /// Edit issue title/description. Owner/Staff or Tenant (own).
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateIssue(Guid id, [FromBody] UpdateIssueRequest request, CancellationToken ct)
    {
        var command = new UpdateIssueCommand
        {
            Id = id,
            Title = request.Title,
            Description = request.Description
        };

        var result = await _mediator.Send(command, ct);
        return OkResponse(result, "Issue updated successfully");
    }

    /// <summary>
    /// Change issue status. Owner/Staff only.
    /// SM-13: NEW → IN_PROGRESS → RESOLVED → CLOSED (+ NEW → CLOSED shortcut).
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> ChangeIssueStatus(Guid id, [FromBody] ChangeIssueStatusRequest request, CancellationToken ct)
    {
        var command = new ChangeIssueStatusCommand
        {
            Id = id,
            Status = request.Status
        };

        var result = await _mediator.Send(command, ct);
        return OkResponse(result, "Issue status updated successfully");
    }

    // Note: POST /{id}/images requires file upload/Cloudinary — deferred.
}

// --- Request records ---

public record CreateIssueRequest(
    Guid? BuildingId,
    Guid? RoomId,
    string Title,
    string Description);

public record UpdateIssueRequest(
    string? Title,
    string? Description);

public record ChangeIssueStatusRequest(IssueStatus Status);
