using Application.Features.Reservations.Commands;
using Application.Features.Reservations.Queries;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Room reservation management.
/// </summary>
[Authorize]
[Route("api/v1/reservations")]
public class ReservationsController : BaseApiController
{
    private readonly IMediator _mediator;

    public ReservationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get a single reservation by ID. Owner/Staff only.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> GetReservationById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetReservationByIdQuery(id), ct);
        return OkResponse(result);
    }

    /// <summary>
    /// List reservations (paginated). Owner/Staff only.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> GetReservations(
        [FromQuery] Guid? buildingId,
        [FromQuery] Guid? roomId,
        [FromQuery] ReservationStatus? status,
        [FromQuery] string? sortBy,
        [FromQuery] bool sortDesc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetReservationsQuery
        {
            BuildingId = buildingId,
            RoomId = roomId,
            Status = status,
            SortBy = sortBy,
            SortDesc = sortDesc,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, ct);
        return PagedOk(result);
    }

    /// <summary>
    /// Create a reservation. Owner/Staff only. Room → BOOKED.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationRequest request, CancellationToken ct)
    {
        var command = new CreateReservationCommand
        {
            RoomId = request.RoomId,
            TenantUserId = request.TenantUserId,
            DepositAmount = request.DepositAmount,
            ExpiresAt = request.ExpiresAt,
            Note = request.Note
        };

        var result = await _mediator.Send(command, ct);
        return CreatedResponse(result, message: "Reservation created successfully");
    }

    /// <summary>
    /// Confirm or cancel a reservation. Owner/Staff only.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> ChangeReservationStatus(
        Guid id,
        [FromBody] ChangeReservationStatusRequest request,
        CancellationToken ct)
    {
        var command = new ChangeReservationStatusCommand
        {
            Id = id,
            Action = request.Action,
            RefundAmount = request.RefundAmount,
            RefundNote = request.RefundNote
        };

        var result = await _mediator.Send(command, ct);
        return OkResponse(result, "Reservation status updated successfully");
    }
}

// --- Request records ---

public record CreateReservationRequest(
    Guid RoomId,
    Guid TenantUserId,
    decimal? DepositAmount,
    DateTime? ExpiresAt,
    string? Note);

public record ChangeReservationStatusRequest(
    string Action,
    decimal? RefundAmount,
    string? RefundNote);
