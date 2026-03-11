using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.MaintenanceIssues.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Features.MaintenanceIssues.Commands;

/// <summary>
/// PATCH /issues/{id}/status — Change issue status.
/// Auth: Owner/Staff only.
/// SM-13: NEW → IN_PROGRESS → RESOLVED → CLOSED, and NEW → CLOSED (shortcut).
/// NT-03: Sends notification to reporter on status change.
/// </summary>
public class ChangeIssueStatusCommand : IRequest<MaintenanceIssueDto>
{
    public Guid Id { get; set; }
    public IssueStatus Status { get; set; }
}

public class ChangeIssueStatusCommandHandler : IRequestHandler<ChangeIssueStatusCommand, MaintenanceIssueDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public ChangeIssueStatusCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<MaintenanceIssueDto> Handle(ChangeIssueStatusCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var issue = await _db.MaintenanceIssues
            .Include(i => i.Building)
            .Include(i => i.Room)
            .Include(i => i.Reporter)
            .Include(i => i.Assignee)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new NotFoundException($"Issue {request.Id} not found.");

        await _buildingScope.AuthorizeAsync(issue.BuildingId, ct);

        // SM-13: Validate state transition
        ValidateTransition(issue.Status, request.Status);

        var oldStatus = issue.Status;
        issue.Status = request.Status;

        // Auto-assign to current user when transitioning to InProgress
        if (request.Status == IssueStatus.InProgress && !issue.AssignedTo.HasValue)
        {
            issue.AssignedTo = userId;

            // Eagerly load the assignee nav prop so the response DTO includes the name
            issue.Assignee = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        }

        issue.UpdatedAt = DateTime.UtcNow;

        // NT-03: Create notification for reporter
        var notification = new Domain.Entities.Notification
        {
            UserId = issue.ReportedBy,
            Title = "Cập nhật sự cố",
            Message = $"Sự cố \"{issue.Title}\" đã được cập nhật: {oldStatus} → {request.Status}.",
            Type = "ISSUE",
            ReferenceId = issue.Id
        };
        _db.Notifications.Add(notification);

        await _db.SaveChangesAsync(ct);

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

    private static void ValidateTransition(IssueStatus current, IssueStatus target)
    {
        var valid = (current, target) switch
        {
            (IssueStatus.New, IssueStatus.InProgress) => true,
            (IssueStatus.New, IssueStatus.Closed) => true, // shortcut for invalid/duplicate
            (IssueStatus.InProgress, IssueStatus.Resolved) => true,
            (IssueStatus.Resolved, IssueStatus.Closed) => true,
            _ => false
        };

        if (!valid)
            throw new ConflictException(
                $"Cannot transition issue from {current} to {target}.",
                "INVALID_STATUS_TRANSITION");
    }
}
