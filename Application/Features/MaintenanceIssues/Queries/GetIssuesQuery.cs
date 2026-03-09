using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.MaintenanceIssues.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Features.MaintenanceIssues.Queries;

/// <summary>
/// GET /issues — List issues (paginated).
/// Auth: ALL. Tenant auto-filtered to own issues (AUTH-06).
/// </summary>
public class GetIssuesQuery : PagedQuery, IRequest<PagedResult<MaintenanceIssueDto>>
{
    public Guid? BuildingId { get; set; }
    public IssueStatus? Status { get; set; }
    public PriorityLevel? Priority { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
}

public class GetIssuesQueryHandler : IRequestHandler<GetIssuesQuery, PagedResult<MaintenanceIssueDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetIssuesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<MaintenanceIssueDto>> Handle(GetIssuesQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var query = _db.MaintenanceIssues
            .AsNoTracking()
            .Include(i => i.Building)
            .Include(i => i.Room)
            .Include(i => i.Reporter)
            .Include(i => i.Assignee)
            .AsQueryable();

        // Role-scoped filtering
        if (_currentUser.IsOwner)
        {
            query = query.Where(i => i.Building!.OwnerId == userId);
        }
        else if (_currentUser.IsStaff)
        {
            var assignedBuildingIds = _db.StaffAssignments
                .Where(sa => sa.StaffId == userId)
                .Select(sa => sa.BuildingId);
            query = query.Where(i => assignedBuildingIds.Contains(i.BuildingId));
        }
        else if (_currentUser.IsTenant)
        {
            // AUTH-06: Tenant sees only own issues
            query = query.Where(i => i.ReportedBy == userId);
        }

        // Filters
        if (request.BuildingId.HasValue)
            query = query.Where(i => i.BuildingId == request.BuildingId.Value);

        if (request.Status.HasValue)
            query = query.Where(i => i.Status == request.Status.Value);

        if (request.Priority.HasValue)
            query = query.Where(i => i.Priority == request.Priority.Value);

        // Sort
        query = request.SortBy?.ToLowerInvariant() switch
        {
            "priority" => request.SortDesc ? query.OrderByDescending(i => i.Priority) : query.OrderBy(i => i.Priority),
            "status" => request.SortDesc ? query.OrderByDescending(i => i.Status) : query.OrderBy(i => i.Status),
            "title" => request.SortDesc ? query.OrderByDescending(i => i.Title) : query.OrderBy(i => i.Title),
            _ => query.OrderByDescending(i => i.CreatedAt)
        };

        var pagedResult = await query
            .Select(i => new MaintenanceIssueDto(
                i.Id,
                i.BuildingId,
                i.Building!.Name,
                i.RoomId,
                i.Room != null ? i.Room.RoomNumber : null,
                i.ReportedBy,
                i.Reporter != null ? i.Reporter.FullName : null,
                i.AssignedTo,
                i.Assignee != null ? i.Assignee.FullName : null,
                i.Title,
                i.Description,
                i.ImageUrls != null ? JsonSerializer.Deserialize<string[]>(i.ImageUrls) : null,
                i.Status.ToString(),
                i.Priority.ToString(),
                i.CreatedAt,
                i.UpdatedAt))
            .ToPagedResultAsync(request, ct);

        return pagedResult;
    }
}
