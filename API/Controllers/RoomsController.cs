using Application.Features.MeterReadings.Queries;
using Application.Features.Rooms.Commands;
using Application.Features.Rooms.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Room management endpoints.
/// </summary>
[Authorize]
public class RoomsController : BaseApiController
{
    private readonly IMediator _mediator;

    public RoomsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List rooms across all buildings (optionally filtered by buildingId).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> GetRooms(
        [FromQuery] Guid? buildingId,
        [FromQuery] string? status,
        [FromQuery] int? floor,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "createdAt:desc",
        CancellationToken ct = default)
    {
        var query = new GetRoomsQuery
        {
            BuildingId = buildingId,
            Status = status,
            Floor = floor,
            Page = page,
            PageSize = pageSize,
            Sort = sort
        };

        var result = await _mediator.Send(query, ct);
        return PagedOk(result);
    }

    /// <summary>
    /// List rooms in a specific building.
    /// </summary>
    [HttpGet("/api/v1/buildings/{buildingId:guid}/rooms")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> GetRoomsByBuilding(
        Guid buildingId,
        [FromQuery] string? status,
        [FromQuery] int? floor,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "createdAt:desc",
        CancellationToken ct = default)
    {
        var query = new GetRoomsQuery
        {
            BuildingId = buildingId,
            Status = status,
            Floor = floor,
            Page = page,
            PageSize = pageSize,
            Sort = sort
        };

        var result = await _mediator.Send(query, ct);
        return PagedOk(result);
    }

    /// <summary>
    /// Get room detail by ID. Tenants can view their own room.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRoom(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRoomByIdQuery(id), ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Create a room in a building.
    /// </summary>
    [HttpPost("/api/v1/buildings/{buildingId:guid}/rooms")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> CreateRoom(Guid buildingId, [FromBody] CreateRoomRequest request, CancellationToken ct)
    {
        var command = new CreateRoomCommand
        {
            BuildingId = buildingId,
            RoomNumber = request.RoomNumber,
            Floor = request.Floor,
            Area = request.Area,
            Price = request.Price,
            MaxOccupants = request.MaxOccupants,
            Description = request.Description
        };

        var result = await _mediator.Send(command, ct);
        return CreatedResponse(result, message: "Room created successfully");
    }

    /// <summary>
    /// Update a room. Partial update.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> UpdateRoom(Guid id, [FromBody] UpdateRoomRequest request, CancellationToken ct)
    {
        var command = new UpdateRoomCommand
        {
            Id = id,
            RoomNumber = request.RoomNumber,
            Floor = request.Floor,
            Area = request.Area,
            Price = request.Price,
            MaxOccupants = request.MaxOccupants,
            Description = request.Description
        };

        var result = await _mediator.Send(command, ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Soft-delete a room. OWNER only.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> DeleteRoom(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteRoomCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Manual room status change. Only AVAILABLE ↔ MAINTENANCE (SM-05).
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> ChangeRoomStatus(Guid id, [FromBody] ChangeRoomStatusRequest request, CancellationToken ct)
    {
        var command = new ChangeRoomStatusCommand
        {
            Id = id,
            Status = request.Status
        };

        var result = await _mediator.Send(command, ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Meter reading history for a room.
    /// Owner/Staff/Tenant (own room).
    /// </summary>
    [HttpGet("{id:guid}/meter-history")]
    public async Task<IActionResult> GetRoomMeterHistory(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRoomMeterHistoryQuery { RoomId = id }, ct);
        return OkResponse(result);
    }
}

public record CreateRoomRequest
{
    public required string RoomNumber { get; init; }
    public required int Floor { get; init; }
    public required decimal Area { get; init; }
    public required decimal Price { get; init; }
    public int MaxOccupants { get; init; } = 2;
    public string? Description { get; init; }
}

public record UpdateRoomRequest
{
    public string? RoomNumber { get; init; }
    public int? Floor { get; init; }
    public decimal? Area { get; init; }
    public decimal? Price { get; init; }
    public int? MaxOccupants { get; init; }
    public string? Description { get; init; }
}

public record ChangeRoomStatusRequest
{
    public required string Status { get; init; }
}
