using Application.Features.Contracts.Commands;
using Application.Features.Contracts.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Contract management endpoints.
/// All authenticated users can list/view (scoped by role).
/// Owner/Staff can create, update, terminate, renew.
/// </summary>
[Authorize]
public class ContractsController : BaseApiController
{
    private readonly IMediator _mediator;

    public ContractsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List contracts with filters and pagination.
    /// Owner/Staff: see all contracts in their buildings.
    /// Tenant: auto-filtered to own contracts.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetContracts(
        [FromQuery] Guid? buildingId,
        [FromQuery] Guid? roomId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "createdAt:desc",
        CancellationToken ct = default)
    {
        var query = new GetContractsQuery
        {
            BuildingId = buildingId,
            RoomId = roomId,
            Status = status,
            Page = page,
            PageSize = pageSize,
            Sort = sort
        };

        var result = await _mediator.Send(query, ct);
        return PagedOk(result);
    }

    /// <summary>
    /// Get contract detail with roommates.
    /// Tenant: own contract only.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetContract(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetContractByIdQuery(id), ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Create a new contract. Owner/Staff only.
    /// UQ-01: 1 active contract per room (409).
    /// SM-02/SM-06: Room status transitions.
    /// Auto-creates ContractTenant + deposit Payment.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> CreateContract([FromBody] CreateContractCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedResponse(result, message: "Contract created successfully");
    }

    /// <summary>
    /// Update a contract (endDate, monthlyRent, note). Owner/Staff only.
    /// Cannot change roomId or tenantUserId (CT-03).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> UpdateContract(Guid id, [FromBody] UpdateContractRequest request, CancellationToken ct)
    {
        var command = new UpdateContractCommand
        {
            Id = id,
            EndDate = request.EndDate,
            MonthlyRent = request.MonthlyRent,
            Note = request.Note
        };

        var result = await _mediator.Send(command, ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Terminate a contract. Owner/Staff only.
    /// CT-02, SM-04: Room → AVAILABLE.
    /// DEP-04: Deposit refund calculation.
    /// </summary>
    [HttpPatch("{id:guid}/terminate")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> TerminateContract(Guid id, [FromBody] TerminateContractRequest request, CancellationToken ct)
    {
        var command = new TerminateContractCommand
        {
            Id = id,
            TerminationDate = request.TerminationDate,
            Note = request.Note,
            Deductions = request.Deductions
        };

        var result = await _mediator.Send(command, ct);
        return OkResponse(result, "Contract terminated successfully");
    }

    /// <summary>
    /// Renew a contract. Owner/Staff only.
    /// CT-01: Old → TERMINATED, new contract created.
    /// Deposit and roommates carry over.
    /// </summary>
    [HttpPatch("{id:guid}/renew")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> RenewContract(Guid id, [FromBody] RenewContractRequest request, CancellationToken ct)
    {
        var command = new RenewContractCommand
        {
            Id = id,
            NewEndDate = request.NewEndDate,
            NewMonthlyRent = request.NewMonthlyRent
        };

        var result = await _mediator.Send(command, ct);
        return CreatedResponse(result, message: "Contract renewed successfully");
    }

    // ── Contract Tenant (Roommate) Endpoints ──────────────────────

    /// <summary>
    /// List roommates on a contract.
    /// Includes those with MoveOutDate (historical).
    /// </summary>
    [HttpGet("{id:guid}/tenants")]
    public async Task<IActionResult> GetContractTenants(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetContractTenantsQuery(id), ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Add a roommate to a contract. Owner/Staff only.
    /// </summary>
    [HttpPost("{id:guid}/tenants")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> AddContractTenant(Guid id, [FromBody] AddContractTenantRequest request, CancellationToken ct)
    {
        var command = new AddContractTenantCommand
        {
            ContractId = id,
            TenantUserId = request.TenantUserId,
            MoveInDate = request.MoveInDate
        };

        var result = await _mediator.Send(command, ct);
        return CreatedResponse(result, message: "Roommate added successfully");
    }

    /// <summary>
    /// Soft-remove a roommate from a contract. Owner/Staff only.
    /// SD-02: Sets MoveOutDate. Cannot remove main tenant.
    /// </summary>
    [HttpDelete("{id:guid}/tenants/{tenantId:guid}")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> RemoveContractTenant(Guid id, Guid tenantId, CancellationToken ct)
    {
        var command = new RemoveContractTenantCommand
        {
            ContractId = id,
            TenantId = tenantId
        };

        await _mediator.Send(command, ct);
        return NoContent();
    }
}

// ── Request DTOs ──────────────────────────────────────────────

public record UpdateContractRequest
{
    public DateOnly? EndDate { get; init; }
    public decimal? MonthlyRent { get; init; }
    public string? Note { get; init; }
}

public record TerminateContractRequest
{
    public required DateOnly TerminationDate { get; init; }
    public string? Note { get; init; }
    public decimal Deductions { get; init; } = 0;
}

public record RenewContractRequest
{
    public required DateOnly NewEndDate { get; init; }
    public decimal? NewMonthlyRent { get; init; }
}

public record AddContractTenantRequest
{
    public required Guid TenantUserId { get; init; }
    public required DateOnly MoveInDate { get; init; }
}
