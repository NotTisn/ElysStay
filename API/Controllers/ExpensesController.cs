using Application.Features.Expenses.Commands;
using Application.Features.Expenses.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Expense management for buildings.
/// </summary>
[Authorize]
[Route("api/v1/expenses")]
public class ExpensesController : BaseApiController
{
    private readonly IMediator _mediator;

    public ExpensesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get a single expense by ID. Owner/Staff only.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> GetExpenseById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetExpenseByIdQuery(id), ct);
        return OkResponse(result);
    }

    /// <summary>
    /// List expenses (paginated). Owner/Staff only.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] Guid? buildingId,
        [FromQuery] Guid? roomId,
        [FromQuery] string? category,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] string sort = "expenseDate:desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetExpensesQuery
        {
            BuildingId = buildingId,
            RoomId = roomId,
            Category = category,
            FromDate = fromDate,
            ToDate = toDate,
            Sort = sort,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, ct);
        return PagedOk(result);
    }

    /// <summary>
    /// Create a new expense. Owner/Staff only.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request, CancellationToken ct)
    {
        var command = new CreateExpenseCommand
        {
            BuildingId = request.BuildingId,
            RoomId = request.RoomId,
            Category = request.Category,
            Description = request.Description,
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate
        };

        var result = await _mediator.Send(command, ct);
        return CreatedResponse(result, message: "Expense created successfully");
    }

    /// <summary>
    /// Update an expense. Owner/Staff only.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> UpdateExpense(Guid id, [FromBody] UpdateExpenseRequest request, CancellationToken ct)
    {
        var command = new UpdateExpenseCommand
        {
            Id = id,
            Category = request.Category,
            Description = request.Description,
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate
        };

        var result = await _mediator.Send(command, ct);
        return OkResponse(result, "Expense updated successfully");
    }

    /// <summary>
    /// Delete an expense. Owner only (hard delete).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> DeleteExpense(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteExpenseCommand(id), ct);
        return NoContent();
    }

    // Note: POST /{id}/receipt requires Cloudinary/file storage integration — deferred.
}

// --- Request records ---

public record CreateExpenseRequest(
    Guid BuildingId,
    Guid? RoomId,
    string Category,
    string Description,
    decimal Amount,
    DateOnly ExpenseDate);

public record UpdateExpenseRequest(
    string? Category,
    string? Description,
    decimal? Amount,
    DateOnly? ExpenseDate);
