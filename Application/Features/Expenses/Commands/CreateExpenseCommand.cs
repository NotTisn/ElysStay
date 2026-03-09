using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Expenses.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Expenses.Commands;

/// <summary>
/// POST /expenses — Create a new expense.
/// Auth: Owner/Staff (building-scoped).
/// </summary>
public class CreateExpenseCommand : IRequest<ExpenseDto>
{
    public Guid BuildingId { get; set; }
    public Guid? RoomId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; }
}

public class CreateExpenseCommandHandler : IRequestHandler<CreateExpenseCommand, ExpenseDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public CreateExpenseCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<ExpenseDto> Handle(CreateExpenseCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();
        await _buildingScope.AuthorizeAsync(request.BuildingId, ct);

        // Validate room belongs to building (if provided)
        if (request.RoomId.HasValue)
        {
            var roomExists = await _db.Rooms
                .AnyAsync(r => r.Id == request.RoomId.Value
                    && r.BuildingId == request.BuildingId
                    && r.DeletedAt == null, ct);

            if (!roomExists)
                throw new NotFoundException($"Room {request.RoomId} not found in building {request.BuildingId}.");
        }

        var expense = new Domain.Entities.Expense
        {
            BuildingId = request.BuildingId,
            RoomId = request.RoomId,
            Category = request.Category,
            Description = request.Description,
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate,
            RecordedBy = userId
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(ct);

        // Reload with navigation properties
        var loaded = await _db.Expenses
            .AsNoTracking()
            .Include(e => e.Building)
            .Include(e => e.Room)
            .Include(e => e.Recorder)
            .FirstAsync(e => e.Id == expense.Id, ct);

        return new ExpenseDto(
            loaded.Id,
            loaded.BuildingId,
            loaded.Building!.Name,
            loaded.RoomId,
            loaded.Room?.RoomNumber,
            loaded.Category,
            loaded.Description,
            loaded.Amount,
            loaded.ReceiptUrl,
            loaded.ExpenseDate,
            loaded.RecordedBy,
            loaded.Recorder?.FullName,
            loaded.CreatedAt,
            loaded.UpdatedAt);
    }
}
