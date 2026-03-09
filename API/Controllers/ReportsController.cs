using Application.Features.Dashboard.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Reporting endpoints: dashboard stats and P&L.
/// </summary>
[Authorize]
[Route("api/v1/reports")]
public class ReportsController : BaseApiController
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// GET /reports/dashboard-stats — Overview stats. Owner/Staff only.
    /// </summary>
    [HttpGet("dashboard-stats")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> GetDashboardStats([FromQuery] Guid? buildingId, CancellationToken ct)
    {
        var query = new GetDashboardStatsQuery { BuildingId = buildingId };
        var result = await _mediator.Send(query, ct);
        return OkResponse(result);
    }

    /// <summary>
    /// GET /reports/pnl — Profit & Loss report. Owner only.
    /// </summary>
    [HttpGet("pnl")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> GetPnlReport([FromQuery] Guid? buildingId, [FromQuery] int year, CancellationToken ct)
    {
        var query = new GetPnlReportQuery
        {
            BuildingId = buildingId,
            Year = year
        };

        var result = await _mediator.Send(query, ct);
        return OkResponse(result);
    }
}
