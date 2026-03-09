using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands;

/// <summary>
/// PATCH /notifications/{id}/read — Mark one notification as read.
/// Auth: ALL. User can only mark own notifications.
/// </summary>
public record MarkNotificationReadCommand(Guid Id) : IRequest<Unit>;

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, Unit>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MarkNotificationReadCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.Id && n.UserId == userId, ct)
            ?? throw new NotFoundException($"Notification {request.Id} not found.");

        notification.IsRead = true;
        await _db.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
