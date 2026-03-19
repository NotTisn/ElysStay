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
    private readonly IBuildingScopeService _buildingScope;
    private readonly IEmailService _emailService;

    public CreateIssueCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope,
        IEmailService emailService)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
        _emailService = emailService;
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
                ?? throw new BadRequestException("Không tìm thấy hợp đồng đang hoạt động. Không thể xác định tòa nhà.");

            buildingId = activeContract.Room!.BuildingId;

            // If tenant provided a roomId, verify it belongs to the same building
            if (request.RoomId.HasValue)
            {
                var roomExists = await _db.Rooms
                    .AnyAsync(r => r.Id == request.RoomId.Value && r.BuildingId == buildingId && r.DeletedAt == null, ct);

                if (!roomExists)
                    throw new NotFoundException($"Không tìm thấy phòng {request.RoomId} trong tòa nhà của bạn.");
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
                throw new BadRequestException("Mã tòa nhà là bắt buộc cho Chủ nhà/Nhân viên.");

            buildingId = request.BuildingId.Value;

            // Building scope authorization — prevents cross-building privilege escalation
            await _buildingScope.AuthorizeAsync(buildingId, ct);

            // Validate room belongs to building
            if (request.RoomId.HasValue)
            {
                var roomExists = await _db.Rooms
                    .AnyAsync(r => r.Id == request.RoomId.Value && r.BuildingId == buildingId && r.DeletedAt == null, ct);

                if (!roomExists)
                    throw new NotFoundException($"Không tìm thấy phòng {request.RoomId} trong tòa nhà {buildingId}.");
            }
        }

        // Load building + owner BEFORE saving — needed for notification + email
        var building = await _db.Buildings.AsNoTracking()
            .Include(b => b.Owner)
            .FirstAsync(b => b.Id == buildingId, ct);

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

        // Notify building owner about the new issue
        _db.Notifications.Add(new Domain.Entities.Notification
        {
            UserId = building.OwnerId,
            Title = "Sự cố mới",
            Message = $"Sự cố mới \"{request.Title}\" đã được báo cáo tại {building.Name}.",
            Type = "ISSUE",
            ReferenceId = issue.Id
        });

        // Single SaveChangesAsync — atomic: issue + notification saved together
        await _db.SaveChangesAsync(ct);

        // Best-effort email to building owner
        var reporter = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (building.Owner != null)
        {
            var (subject, html) = Application.Common.Email.EmailTemplates.IssueCreated(
                building.Owner.FullName, reporter?.FullName ?? "Khách thuê",
                request.Title, building.Name, null);
            await _emailService.TrySendAsync(building.Owner.Email, building.Owner.FullName, subject, html, ct);
        }

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
