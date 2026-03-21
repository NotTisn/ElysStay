using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Expenses.Commands;

/// <summary>
/// DELETE /expenses/{id} — Soft-delete an expense (preserves financial audit trail).
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
            throw new ForbiddenException("Chỉ chủ nhà mới có thể xóa chi phí.");

        var expense = await _db.Expenses
            .Include(e => e.Building)
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy chi phí {request.Id}.");

        // Verify ownership
        if (expense.Building!.OwnerId != userId)
            throw new ForbiddenException("Bạn chỉ có thể xóa chi phí của tòa nhà mình.");

        // Soft-delete: preserve financial audit trail
        expense.DeletedAt = DateTime.UtcNow;
        expense.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
