using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.MaintenanceIssues.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Features.MaintenanceIssues.Commands;

/// <summary>
/// POST /issues — Report a new maintenance issue.
/// Auth: ALL.
/// IS-01: Tenant buildingId auto-resolved from contract → room → building.
/// </summary>
public class CreateIssueCommand : IRequest<MaintenanceIssueDto>
{
    public Guid? BuildingId { get; set; }
    public Guid? RoomId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class CreateIssueCommandHandler : IRequestHandler<CreateIssueCommand, MaintenanceIssueDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateIssueCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MaintenanceIssueDto> Handle(CreateIssueCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();
        Guid buildingId;

        if (_currentUser.IsTenant)
        {
            // IS-01: Auto-resolve from active contract
            var activeContract = await _db.Contracts
                .Include(c => c.Room)
                .Where(c => c.Status == ContractStatus.Active)
                .Where(c => c.ContractTenants.Any(ct2 => ct2.TenantUserId == userId && ct2.MoveOutDate == null))
                .FirstOrDefaultAsync(ct)
                ?? throw new BadRequestException("No active contract found. Cannot determine building.");

            buildingId = activeContract.Room!.BuildingId;

            // If tenant provided a roomId, verify it belongs to the same building
            if (request.RoomId.HasValue)
            {
                var room = await _db.Rooms
                    .FirstOrDefaultAsync(r => r.Id == request.RoomId.Value && r.BuildingId == buildingId && r.DeletedAt == null, ct)
                    ?? throw new NotFoundException($"Room {request.RoomId} not found in your building.");
            }
            else
            {
                // Default to the contract's room
                request.RoomId = activeContract.RoomId;
            }
        }
        else
        {
            // Owner/Staff must provide buildingId
            if (!request.BuildingId.HasValue)
                throw new BadRequestException("BuildingId is required for Owner/Staff.");

            buildingId = request.BuildingId.Value;

            // Validate room belongs to building
            if (request.RoomId.HasValue)
            {
                var roomExists = await _db.Rooms
                    .AnyAsync(r => r.Id == request.RoomId.Value && r.BuildingId == buildingId && r.DeletedAt == null, ct);

                if (!roomExists)
                    throw new NotFoundException($"Room {request.RoomId} not found in building {buildingId}.");
            }
        }

        var issue = new Domain.Entities.MaintenanceIssue
        {
            BuildingId = buildingId,
            RoomId = request.RoomId,
            ReportedBy = userId,
            Title = request.Title,
            Description = request.Description,
            Status = IssueStatus.New,
            Priority = PriorityLevel.Medium
        };

        _db.MaintenanceIssues.Add(issue);
        await _db.SaveChangesAsync(ct);

        // Reload with nav
        var loaded = await _db.MaintenanceIssues
            .AsNoTracking()
            .Include(i => i.Building)
            .Include(i => i.Room)
            .Include(i => i.Reporter)
            .FirstAsync(i => i.Id == issue.Id, ct);

        return new MaintenanceIssueDto(
            loaded.Id,
            loaded.BuildingId,
            loaded.Building!.Name,
            loaded.RoomId,
            loaded.Room?.RoomNumber,
            loaded.ReportedBy,
            loaded.Reporter?.FullName,
            loaded.AssignedTo,
            null,
            loaded.Title,
            loaded.Description,
            loaded.ImageUrls != null ? JsonSerializer.Deserialize<string[]>(loaded.ImageUrls) : null,
            loaded.Status.ToString(),
            loaded.Priority.ToString(),
            loaded.CreatedAt,
            loaded.UpdatedAt);
    }
}
