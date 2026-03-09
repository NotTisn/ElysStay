using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.MaintenanceIssues.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Features.MaintenanceIssues.Commands;

/// <summary>
/// PUT /issues/{id} — Edit issue title/description.
/// Auth: Owner/Staff (building-scoped), Tenant (own only).
/// </summary>
public class UpdateIssueCommand : IRequest<MaintenanceIssueDto>
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
}

public class UpdateIssueCommandHandler : IRequestHandler<UpdateIssueCommand, MaintenanceIssueDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public UpdateIssueCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<MaintenanceIssueDto> Handle(UpdateIssueCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var issue = await _db.MaintenanceIssues
            .Include(i => i.Building)
            .Include(i => i.Room)
            .Include(i => i.Reporter)
            .Include(i => i.Assignee)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new NotFoundException($"Issue {request.Id} not found.");

        // Auth
        if (_currentUser.IsTenant)
        {
            if (issue.ReportedBy != userId)
                throw new ForbiddenException("Tenants can only edit their own issues.");
        }
        else
        {
            await _buildingScope.AuthorizeAsync(issue.BuildingId, ct);
        }

        // Partial update
        if (request.Title is not null)
            issue.Title = request.Title;
        if (request.Description is not null)
            issue.Description = request.Description;

        issue.UpdatedAt = DateTime.UtcNow;
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
}
