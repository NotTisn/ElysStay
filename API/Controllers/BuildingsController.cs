using Application.Features.Buildings.Commands;
using Application.Features.Buildings.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Building management endpoints.
/// Requires authentication. Owner-only for create/update/delete.
/// </summary>
[Authorize]
public class BuildingsController : BaseApiController
{
    private readonly IMediator _mediator;

    public BuildingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List buildings with optional name/address filter and pagination.
    /// Owner sees all their buildings. Staff sees only assigned buildings.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> GetBuildings(
        [FromQuery] string? name,
        [FromQuery] string? address,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "createdAt:desc",
        CancellationToken ct = default)
    {
        var query = new GetBuildingsQuery
        {
            Name = name,
            Address = address,
            Page = page,
            PageSize = pageSize,
            Sort = sort
        };

        var result = await _mediator.Send(query, ct);
        return PagedOk(result);
    }

    /// <summary>
    /// Get building detail with room stats (totalRooms, occupancyRate).
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> GetBuilding(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBuildingByIdQuery(id), ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Create a new building. OWNER only.
    /// Auto-creates 5 default services (BD-01).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> CreateBuilding([FromBody] CreateBuildingCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedResponse(result, message: "Building created successfully");
    }

    /// <summary>
    /// Update a building. OWNER only. Partial update.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> UpdateBuilding(Guid id, [FromBody] UpdateBuildingRequest request, CancellationToken ct)
    {
        var command = new UpdateBuildingCommand
        {
            Id = id,
            Name = request.Name,
            Address = request.Address,
            Description = request.Description,
            TotalFloors = request.TotalFloors,
            InvoiceDueDay = request.InvoiceDueDay
        };

        var result = await _mediator.Send(command, ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Soft-delete a building. OWNER only.
    /// Returns 409 if any room has an active contract (SD-04).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> DeleteBuilding(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteBuildingCommand(id), ct);
        return NoContent();
    }
}

/// <summary>
/// Request body for PUT /buildings/{id}.
/// Separated from the command to allow the route ID to be merged in.
/// </summary>
public record UpdateBuildingRequest
{
    public string? Name { get; init; }
    public string? Address { get; init; }
    public string? Description { get; init; }
    public int? TotalFloors { get; init; }
    public int? InvoiceDueDay { get; init; }
}
