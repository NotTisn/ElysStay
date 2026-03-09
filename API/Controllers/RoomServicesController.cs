using Application.Features.RoomServices.Commands;
using Application.Features.RoomServices.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Per-room service configuration (overrides vs building defaults).
/// </summary>
[Authorize]
[Route("api/v1/rooms/{roomId:guid}/services")]
public class RoomServicesController : BaseApiController
{
    private readonly IMediator _mediator;

    public RoomServicesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// View per-room service config with building defaults merged.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> GetRoomServices(Guid roomId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRoomServicesQuery(roomId), ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Bulk update room service overrides. Full replacement semantics.
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> UpdateRoomServices(Guid roomId, [FromBody] List<RoomServiceEntryRequest> entries, CancellationToken ct)
    {
        var command = new UpdateRoomServicesCommand
        {
            RoomId = roomId,
            Services = entries.Select(e => new RoomServiceEntry
            {
                ServiceId = e.ServiceId,
                IsEnabled = e.IsEnabled,
                OverrideUnitPrice = e.OverrideUnitPrice,
                OverrideQuantity = e.OverrideQuantity
            }).ToList()
        };

        var result = await _mediator.Send(command, ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Remove a single override, reverting to building default.
    /// </summary>
    [HttpDelete("{serviceId:guid}")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> RemoveOverride(Guid roomId, Guid serviceId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveRoomServiceOverrideCommand(roomId, serviceId), ct);
        return NoContent();
    }
}

public record RoomServiceEntryRequest
{
    public required Guid ServiceId { get; init; }
    public required bool IsEnabled { get; init; }
    public decimal? OverrideUnitPrice { get; init; }
    public decimal? OverrideQuantity { get; init; }
}
