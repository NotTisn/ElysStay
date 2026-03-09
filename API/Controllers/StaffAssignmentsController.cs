using Application.Features.StaffAssignments.Commands;
using Application.Features.StaffAssignments.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Staff assignment management for buildings.
/// </summary>
[Authorize]
[Route("api/v1/buildings/{buildingId:guid}/staff")]
public class StaffAssignmentsController : BaseApiController
{
    private readonly IMediator _mediator;

    public StaffAssignmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List all staff assigned to a building.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> GetBuildingStaff(Guid buildingId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBuildingStaffQuery(buildingId), ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Assign a staff member to a building.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> AssignStaff(Guid buildingId, [FromBody] AssignStaffRequest request, CancellationToken ct)
    {
        var command = new AssignStaffCommand
        {
            BuildingId = buildingId,
            StaffId = request.StaffId
        };

        var result = await _mediator.Send(command, ct);
        return CreatedResponse(result, message: "Staff assigned successfully");
    }

    /// <summary>
    /// Unassign a staff member from a building.
    /// </summary>
    [HttpDelete("{staffId:guid}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> UnassignStaff(Guid buildingId, Guid staffId, CancellationToken ct)
    {
        await _mediator.Send(new UnassignStaffCommand(buildingId, staffId), ct);
        return NoContent();
    }
}

public record AssignStaffRequest
{
    public required Guid StaffId { get; init; }
}
