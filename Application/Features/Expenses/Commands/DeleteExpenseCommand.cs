using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Expenses.Commands;

/// <summary>
/// DELETE /expenses/{id} — Hard delete an expense.
/// Auth: Owner only.
/// </summary>
public record DeleteExpenseCommand(Guid Id) : IRequest<Unit>;

public class DeleteExpenseCommandHandler : IRequestHandler<DeleteExpenseCommand, Unit>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteExpenseCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteExpenseCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        if (!_currentUser.IsOwner)
            throw new ForbiddenException("Only owners can delete expenses.");

        var expense = await _db.Expenses
            .Include(e => e.Building)
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new NotFoundException($"Expense {request.Id} not found.");

        // Verify ownership
        if (expense.Building!.OwnerId != userId)
            throw new ForbiddenException("You can only delete expenses for your own buildings.");

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
