using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Notifications.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Queries;

/// <summary>
/// GET /notifications — List personal notifications (paginated).
/// Auth: ALL. Each user sees only their own.
/// </summary>
public class GetNotificationsQuery : PagedQuery, IRequest<PagedResult<NotificationDto>>
{
}

public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, PagedResult<NotificationDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetNotificationsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var query = _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt);

        var pagedResult = await query
            .Select(n => new NotificationDto(
                n.Id,
                n.UserId,
                n.Title,
                n.Message,
                n.IsRead,
                n.Type,
                n.ReferenceId,
                n.CreatedAt))
            .ToPagedResultAsync(request, ct);

        return pagedResult;
    }
}
