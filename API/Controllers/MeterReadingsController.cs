using Application.Features.MeterReadings.Commands;
using Application.Features.MeterReadings.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Meter reading management endpoints.
/// Owner/Staff can submit and edit. All roles can read (scoped).
/// </summary>
[Authorize]
[Route("api/v1/meter-readings")]
public class MeterReadingsController : BaseApiController
{
    private readonly IMediator _mediator;

    public MeterReadingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List meter readings for a building/month.
    /// Required: billingYear, billingMonth.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMeterReadings(
        [FromQuery] Guid? buildingId,
        [FromQuery] int billingYear,
        [FromQuery] int billingMonth,
        CancellationToken ct)
    {
        var query = new GetMeterReadingsQuery
        {
            BuildingId = buildingId,
            BillingYear = billingYear,
            BillingMonth = billingMonth
        };

        var result = await _mediator.Send(query, ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Bulk upsert meter readings for a building/month.
    /// Owner/Staff only.
    /// </summary>
    [HttpPost("bulk")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> BulkUpsertMeterReadings(
        [FromBody] BulkUpsertMeterReadingsCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return OkResponse(result, "Meter readings submitted successfully");
    }

    /// <summary>
    /// Edit a single meter reading.
    /// Owner/Staff only.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> UpdateMeterReading(Guid id, [FromBody] UpdateMeterReadingRequest request, CancellationToken ct)
    {
        var command = new UpdateMeterReadingCommand
        {
            Id = id,
            PreviousReading = request.PreviousReading,
            CurrentReading = request.CurrentReading
        };

        var result = await _mediator.Send(command, ct);
        return OkResponse(result);
    }
}

// ── Request DTOs ──────────────────────────────────────────────

public record UpdateMeterReadingRequest
{
    public decimal? PreviousReading { get; init; }
    public decimal? CurrentReading { get; init; }
}
