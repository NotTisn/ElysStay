using Application.Features.Services.Commands;
using Application.Features.Services.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Building service (fee configuration) management.
/// </summary>
[Authorize]
public class ServicesController : BaseApiController
{
    private readonly IMediator _mediator;

    public ServicesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List all services for a building.
    /// </summary>
    [HttpGet("/api/v1/buildings/{buildingId:guid}/services")]
    [Authorize(Roles = "Owner,Staff,Tenant")]
    public async Task<IActionResult> GetBuildingServices(Guid buildingId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBuildingServicesQuery(buildingId), ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Create a service for a building.
    /// </summary>
    [HttpPost("/api/v1/buildings/{buildingId:guid}/services")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> CreateService(Guid buildingId, [FromBody] CreateServiceRequest request, CancellationToken ct)
    {
        var command = new CreateServiceCommand
        {
            BuildingId = buildingId,
            Name = request.Name,
            Unit = request.Unit,
            UnitPrice = request.UnitPrice,
            IsMetered = request.IsMetered
        };

        var result = await _mediator.Send(command, ct);
        return CreatedResponse(result, message: "Service created successfully");
    }

    /// <summary>
    /// Update a service. PR-03: price change tracked.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> UpdateService(Guid id, [FromBody] UpdateServiceRequest request, CancellationToken ct)
    {
        var command = new UpdateServiceCommand
        {
            Id = id,
            Name = request.Name,
            Unit = request.Unit,
            UnitPrice = request.UnitPrice,
            IsMetered = request.IsMetered
        };

        var result = await _mediator.Send(command, ct);
        return OkResponse(result);
    }

    /// <summary>
    /// SD-03: Deactivate a service (soft).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> DeactivateService(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeactivateServiceCommand(id), ct);
        return NoContent();
    }
}

public record CreateServiceRequest
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required decimal UnitPrice { get; init; }
    public required bool IsMetered { get; init; }
}

public record UpdateServiceRequest
{
    public string? Name { get; init; }
    public string? Unit { get; init; }
    public decimal? UnitPrice { get; init; }
    public bool? IsMetered { get; init; }
}
