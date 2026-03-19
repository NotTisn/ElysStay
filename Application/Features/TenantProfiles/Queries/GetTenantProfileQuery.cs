using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.TenantProfiles.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TenantProfiles.Queries;

/// <summary>
/// GET /tenant-profiles/{userId} — View CCCD profile.
/// Auth: Owner/Staff (must have tenant in their buildings), Tenant (own only).
/// </summary>
public record GetTenantProfileQuery(Guid UserId) : IRequest<TenantProfileDto>;

public class GetTenantProfileQueryHandler : IRequestHandler<GetTenantProfileQuery, TenantProfileDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetTenantProfileQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TenantProfileDto> Handle(GetTenantProfileQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        // Tenant can only view own profile
        if (_currentUser.IsTenant && userId != request.UserId)
            throw new ForbiddenException("Khách thuê chỉ có thể xem hồ sơ của mình.");

        // Owner/Staff: verify the target user exists and has Tenant role
        var targetUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new NotFoundException($"Không tìm thấy người dùng {request.UserId}.");

        if (targetUser.Role != UserRole.Tenant)
            throw new BadRequestException("Hồ sơ khách thuê chỉ dành cho người dùng có vai trò Khách thuê.");

        // Owner/Staff: verify the tenant has a contract in one of their buildings.
        // Include both main-tenant and roommate relationships.
        if (_currentUser.IsOwner)
        {
            var hasTenantInBuildings = await _db.Contracts
                .AnyAsync(c => (c.TenantUserId == request.UserId
                        || c.ContractTenants.Any(ctn => ctn.TenantUserId == request.UserId && ctn.MoveOutDate == null))
                    && c.Room!.Building!.OwnerId == userId, ct);
            if (!hasTenantInBuildings)
                throw new ForbiddenException("Khách thuê này không thuộc tòa nhà nào của bạn.");
        }
        else if (_currentUser.IsStaff)
        {
            var hasTenantInBuildings = await _db.Contracts
                .AnyAsync(c => (c.TenantUserId == request.UserId
                        || c.ContractTenants.Any(ctn => ctn.TenantUserId == request.UserId && ctn.MoveOutDate == null))
                    && c.Room!.Building!.BuildingStaffs.Any(s => s.StaffId == userId), ct);
            if (!hasTenantInBuildings)
                throw new ForbiddenException("Khách thuê này không thuộc tòa nhà nào bạn được phân công.");
        }

        var profile = await _db.TenantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ khách thuê của người dùng {request.UserId}.");

        return new TenantProfileDto(
            profile.UserId,
            profile.IdNumber,
            profile.IdFrontUrl,
            profile.IdBackUrl,
            profile.DateOfBirth,
            profile.Gender,
            profile.PermanentAddress,
            profile.IssuedDate,
            profile.IssuedPlace,
            profile.CreatedAt,
            profile.UpdatedAt);
    }
}
