using Application.Features.Dashboard.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Dashboard and user profile endpoints.
/// </summary>
[Authorize]
[Route("api/v1/users")]
public class UsersController : BaseApiController
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// GET /users/me/dashboard — Role-based homepage summary.
    /// </summary>
    [HttpGet("me/dashboard")]
    public async Task<IActionResult> GetMyDashboard(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyDashboardQuery(), ct);
        return OkResponse(result);
    }
}
