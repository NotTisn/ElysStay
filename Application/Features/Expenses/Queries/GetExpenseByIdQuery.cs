using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Expenses.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Expenses.Queries;

/// <summary>
/// GET /expenses/{id} — Get a single expense by ID.
/// Auth: Owner/Staff only. Staff building-scoped (AUTH-05).
/// </summary>
public record GetExpenseByIdQuery(Guid Id) : IRequest<ExpenseDto>;

public class GetExpenseByIdQueryHandler : IRequestHandler<GetExpenseByIdQuery, ExpenseDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetExpenseByIdQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ExpenseDto> Handle(GetExpenseByIdQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var expense = await _db.Expenses
            .AsNoTracking()
            .Include(e => e.Building)
            .Include(e => e.Room)
            .Include(e => e.Recorder)
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new NotFoundException("Expense", request.Id);

        // Role-scoped authorization
        if (_currentUser.IsOwner)
        {
            if (expense.Building!.OwnerId != userId)
                throw new ForbiddenException("You do not own this building.");
        }
        else if (_currentUser.IsStaff)
        {
            var isAssigned = await _db.StaffAssignments
                .AnyAsync(sa => sa.BuildingId == expense.BuildingId && sa.StaffId == userId, ct);
            if (!isAssigned)
                throw new ForbiddenException("You are not assigned to this building.");
        }
        else
        {
            // Tenants cannot view expenses
            throw new ForbiddenException("Tenants cannot access expense records.");
        }

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
