using Application.Features.Invoices.Commands;
using Application.Features.Invoices.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Invoice management endpoints.
/// All authenticated users can list/view (scoped by role).
/// Owner/Staff can generate, edit, send.
/// Owner only can void.
/// </summary>
[Authorize]
public class InvoicesController : BaseApiController
{
    private readonly IMediator _mediator;

    public InvoicesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List invoices with filters and pagination.
    /// Tenant: auto-filtered to own invoices.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] Guid? buildingId,
        [FromQuery] int? billingYear,
        [FromQuery] int? billingMonth,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "createdAt:desc",
        CancellationToken ct = default)
    {
        var query = new GetInvoicesQuery
        {
            BuildingId = buildingId,
            BillingYear = billingYear,
            BillingMonth = billingMonth,
            Status = status,
            Page = page,
            PageSize = pageSize,
            Sort = sort
        };

        var result = await _mediator.Send(query, ct);
        return PagedOk(result);
    }

    /// <summary>
    /// Get invoice detail with line items.
    /// PAY-01: PaidAmount computed from SUM(payments).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetInvoice(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetInvoiceByIdQuery(id), ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Generate invoices for a building/month.
    /// IG-01: Idempotent — skips existing.
    /// Owner/Staff only.
    /// </summary>
    [HttpPost("generate")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> GenerateInvoices([FromBody] GenerateInvoicesCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return OkResponse(result, "Invoice generation completed");
    }

    /// <summary>
    /// Edit penalty/discount on an invoice.
    /// Only DRAFT or SENT invoices. Owner/Staff only.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> UpdateInvoice(Guid id, [FromBody] UpdateInvoiceRequest request, CancellationToken ct)
    {
        var command = new UpdateInvoiceCommand
        {
            Id = id,
            PenaltyAmount = request.PenaltyAmount,
            DiscountAmount = request.DiscountAmount,
            Note = request.Note
        };

        var result = await _mediator.Send(command, ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Send invoice: DRAFT → SENT.
    /// Owner/Staff only.
    /// </summary>
    [HttpPatch("{id:guid}/send")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> SendInvoice(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new SendInvoiceCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Batch send multiple invoices.
    /// Owner/Staff only.
    /// </summary>
    [HttpPost("send-batch")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> BatchSendInvoices([FromBody] BatchSendInvoicesRequest request, CancellationToken ct)
    {
        var command = new BatchSendInvoicesCommand { InvoiceIds = request.InvoiceIds };
        var sentCount = await _mediator.Send(command, ct);
        return OkResponse(new { sentCount }, $"{sentCount} invoices sent successfully");
    }

    /// <summary>
    /// Void an invoice. OWNER only.
    /// SM-12: Any status except PAID → VOID.
    /// </summary>
    [HttpPatch("{id:guid}/void")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> VoidInvoice(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new VoidInvoiceCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Record a payment on an invoice.
    /// Owner/Staff only. PAY-03: Auto-transitions status.
    /// </summary>
    [HttpPost("{id:guid}/payments")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordPaymentRequest request, CancellationToken ct)
    {
        var command = new Application.Features.Payments.Commands.RecordPaymentCommand
        {
            InvoiceId = id,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            Note = request.Note
        };

        var result = await _mediator.Send(command, ct);
        return CreatedResponse(result, message: "Payment recorded successfully");
    }
}

// ── Request DTOs ──────────────────────────────────────────────

public record UpdateInvoiceRequest
{
    public decimal? PenaltyAmount { get; init; }
    public decimal? DiscountAmount { get; init; }
    public string? Note { get; init; }
}

public record BatchSendInvoicesRequest
{
    public required IReadOnlyList<Guid> InvoiceIds { get; init; }
}

public record RecordPaymentRequest
{
    public required decimal Amount { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Note { get; init; }
}
