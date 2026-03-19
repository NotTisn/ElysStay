using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.TenantProfiles.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TenantProfiles.Commands;

/// <summary>
/// PUT /tenant-profiles/{userId} — Update CCCD text data.
/// Auth: Owner/Staff (must have tenant in their buildings), Tenant (own only).
/// </summary>
public class UpdateTenantProfileCommand : IRequest<TenantProfileDto>
{
    public Guid UserId { get; set; }
    public string? IdNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? PermanentAddress { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public string? IssuedPlace { get; set; }
}

public class UpdateTenantProfileCommandHandler : IRequestHandler<UpdateTenantProfileCommand, TenantProfileDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateTenantProfileCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TenantProfileDto> Handle(UpdateTenantProfileCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        // Tenant can only update own profile
        if (_currentUser.IsTenant && userId != request.UserId)
            throw new ForbiddenException("Khách thuê chỉ có thể cập nhật hồ sơ của mình.");

        // Verify user exists and has Tenant role
        var targetUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new NotFoundException($"Không tìm thấy người dùng {request.UserId}.");

        if (targetUser.Role != UserRole.Tenant)
            throw new BadRequestException("Hồ sơ khách thuê chỉ dành cho người dùng có vai trò Khách thuê.");

        // Owner/Staff: verify the tenant has a contract in one of their buildings
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
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ khách thuê của người dùng {request.UserId}.");

        // Full replacement (PUT semantics)
        profile.IdNumber = request.IdNumber;
        profile.DateOfBirth = request.DateOfBirth;
        profile.Gender = request.Gender;
        profile.PermanentAddress = request.PermanentAddress;
        profile.IssuedDate = request.IssuedDate;
        profile.IssuedPlace = request.IssuedPlace;
        profile.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

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
