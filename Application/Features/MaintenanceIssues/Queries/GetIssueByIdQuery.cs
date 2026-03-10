using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.MaintenanceIssues.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Features.MaintenanceIssues.Queries;

/// <summary>
/// GET /issues/{id} — Get a single maintenance issue by ID.
/// Auth: All roles. Tenant auto-filtered to own issues (AUTH-06).
/// </summary>
public record GetIssueByIdQuery(Guid Id) : IRequest<MaintenanceIssueDto>;

public class GetIssueByIdQueryHandler : IRequestHandler<GetIssueByIdQuery, MaintenanceIssueDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetIssueByIdQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MaintenanceIssueDto> Handle(GetIssueByIdQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var issue = await _db.MaintenanceIssues
            .AsNoTracking()
            .Include(i => i.Building)
            .Include(i => i.Room)
            .Include(i => i.Reporter)
            .Include(i => i.Assignee)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new NotFoundException("MaintenanceIssue", request.Id);

        // Role-scoped authorization
        if (_currentUser.IsOwner)
        {
            if (issue.Building!.OwnerId != userId)
                throw new ForbiddenException("You do not own this building.");
        }
        else if (_currentUser.IsStaff)
        {
            var isAssigned = await _db.StaffAssignments
                .AnyAsync(sa => sa.BuildingId == issue.BuildingId && sa.StaffId == userId, ct);
            if (!isAssigned)
                throw new ForbiddenException("You are not assigned to this building.");
        }
        else if (_currentUser.IsTenant)
        {
            // AUTH-06: Tenant sees only own issues
            if (issue.ReportedBy != userId)
                throw new ForbiddenException("You can only view your own issues.");
        }

        return new MaintenanceIssueDto(
            issue.Id,
            issue.BuildingId,
            issue.Building!.Name,
            issue.RoomId,
            issue.Room?.RoomNumber,
            issue.ReportedBy,
            issue.Reporter?.FullName,
            issue.AssignedTo,
            issue.Assignee?.FullName,
            issue.Title,
            issue.Description,
            issue.ImageUrls != null ? JsonSerializer.Deserialize<string[]>(issue.ImageUrls) : null,
            issue.Status.ToString(),
            issue.Priority.ToString(),
            issue.CreatedAt,
            issue.UpdatedAt);
    }
}
