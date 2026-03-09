using Application.Features.Payments.Commands;
using Application.Features.Payments.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Payment management endpoints.
/// Owner/Staff can record. All can read (scoped).
/// </summary>
[Authorize]
public class PaymentsController : BaseApiController
{
    private readonly IMediator _mediator;

    public PaymentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Payment history with filters and pagination.
    /// Filter by type: RentPayment / DepositIn / DepositRefund.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPayments(
        [FromQuery] Guid? buildingId,
        [FromQuery] string? type,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "paidAt:desc",
        CancellationToken ct = default)
    {
        var query = new GetPaymentsQuery
        {
            BuildingId = buildingId,
            Type = type,
            FromDate = fromDate,
            ToDate = toDate,
            Page = page,
            PageSize = pageSize,
            Sort = sort
        };

        var result = await _mediator.Send(query, ct);
        return PagedOk(result);
    }

    /// <summary>
    /// Batch record payments. PAY-06: All-or-nothing.
    /// Owner/Staff only.
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> BatchRecordPayments([FromBody] BatchRecordPaymentsCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return OkResponse(result, $"{result.Count} payments recorded successfully");
    }
}
