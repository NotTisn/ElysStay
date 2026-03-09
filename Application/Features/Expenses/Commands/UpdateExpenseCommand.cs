using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Expenses.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Expenses.Commands;

/// <summary>
/// PUT /expenses/{id} — Update an existing expense.
/// Auth: Owner/Staff (building-scoped).
/// </summary>
public class UpdateExpenseCommand : IRequest<ExpenseDto>
{
    public Guid Id { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public DateOnly? ExpenseDate { get; set; }
}

public class UpdateExpenseCommandHandler : IRequestHandler<UpdateExpenseCommand, ExpenseDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IBuildingScopeService _buildingScope;

    public UpdateExpenseCommandHandler(IApplicationDbContext db, IBuildingScopeService buildingScope)
    {
        _db = db;
        _buildingScope = buildingScope;
    }

    public async Task<ExpenseDto> Handle(UpdateExpenseCommand request, CancellationToken ct)
    {
        var expense = await _db.Expenses
            .Include(e => e.Building)
            .Include(e => e.Room)
            .Include(e => e.Recorder)
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new NotFoundException($"Expense {request.Id} not found.");

        await _buildingScope.AuthorizeAsync(expense.BuildingId, ct);

        // Partial update
        if (request.Category is not null)
            expense.Category = request.Category;
        if (request.Description is not null)
            expense.Description = request.Description;
        if (request.Amount.HasValue)
            expense.Amount = request.Amount.Value;
        if (request.ExpenseDate.HasValue)
            expense.ExpenseDate = request.ExpenseDate.Value;

        expense.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new ExpenseDto(
            expense.Id,
            expense.BuildingId,
            expense.Building!.Name,
            expense.RoomId,
            expense.Room?.RoomNumber,
            expense.Category,
            expense.Description,
            expense.Amount,
            expense.ReceiptUrl,
            expense.ExpenseDate,
            expense.RecordedBy,
            expense.Recorder?.FullName,
            expense.CreatedAt,
            expense.UpdatedAt);
    }
}
