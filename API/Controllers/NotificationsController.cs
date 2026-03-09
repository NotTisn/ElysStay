using Application.Features.Notifications.Commands;
using Application.Features.Notifications.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Personal notification management.
/// </summary>
[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController : BaseApiController
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List personal notifications (paginated). All roles.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetNotificationsQuery
        {
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, ct);
        return PagedOk(result);
    }

    /// <summary>
    /// Mark a single notification as read.
    /// </summary>
    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new MarkNotificationReadCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Mark all notifications as read.
    /// </summary>
    [HttpPatch("mark-all-read")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var count = await _mediator.Send(new MarkAllNotificationsReadCommand(), ct);
        return OkResponse(new { markedRead = count }, "All notifications marked as read");
    }
}
