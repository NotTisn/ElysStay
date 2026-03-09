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
            throw new ForbiddenException("Tenants can only view their own profile.");

        // Owner/Staff: verify the target user exists and has Tenant role
        var targetUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new NotFoundException($"User {request.UserId} not found.");

        if (targetUser.Role != UserRole.Tenant)
            throw new BadRequestException("Tenant profile is only available for users with Tenant role.");

        // Owner/Staff: verify the tenant has a contract in one of their buildings
        if (_currentUser.IsOwner)
        {
            var hasTenantInBuildings = await _db.Contracts
                .AnyAsync(c => c.TenantUserId == request.UserId
                    && c.Room!.Building!.OwnerId == userId, ct);
            if (!hasTenantInBuildings)
                throw new ForbiddenException("This tenant does not belong to any of your buildings.");
        }
        else if (_currentUser.IsStaff)
        {
            var hasTenantInBuildings = await _db.Contracts
                .AnyAsync(c => c.TenantUserId == request.UserId
                    && c.Room!.Building!.BuildingStaffs.Any(s => s.StaffId == userId), ct);
            if (!hasTenantInBuildings)
                throw new ForbiddenException("This tenant does not belong to any of your assigned buildings.");
        }

        var profile = await _db.TenantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, ct)
            ?? throw new NotFoundException($"Tenant profile for user {request.UserId} not found.");

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
