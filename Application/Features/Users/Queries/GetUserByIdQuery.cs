using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Users.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Users.Queries;

/// <summary>
/// GET /users/{id} — Returns a single user's detail.
/// Auth: Owner sees anyone. Staff sees tenants + self. Tenant sees self only.
/// </summary>
public record GetUserByIdQuery(Guid Id) : IRequest<UserDto>;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUserByIdQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var callerId = _currentUser.GetRequiredUserId();

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.Id, ct)
            ?? throw new NotFoundException("Người dùng", request.Id);

        // Tenant: self only
        if (_currentUser.IsTenant && user.Id != callerId)
            throw new ForbiddenException("Khách thuê chỉ có thể xem hồ sơ của mình.");

        if (_currentUser.IsStaff && user.Id != callerId)
        {
            if (user.Role != UserRole.Tenant)
                throw new ForbiddenException("Nhân viên chỉ có thể xem hồ sơ của mình và khách thuê trong tòa nhà được phân công.");

            var canViewTenant = await _db.Contracts
                .AnyAsync(c => (c.TenantUserId == user.Id
                        || c.ContractTenants.Any(ctn => ctn.TenantUserId == user.Id && ctn.MoveOutDate == null))
                    && c.Room!.Building!.BuildingStaffs.Any(bs => bs.StaffId == callerId), ct);

            if (!canViewTenant)
                throw new ForbiddenException("Khách thuê này không thuộc tòa nhà nào bạn được phân công.");
        }

        if (_currentUser.IsOwner && user.Id != callerId)
        {
            var canViewUser = user.Role switch
            {
                UserRole.Owner => false,
                UserRole.Staff => await _db.StaffAssignments
                    .AnyAsync(sa => sa.StaffId == user.Id && sa.Building!.OwnerId == callerId, ct),
                UserRole.Tenant => await _db.Contracts
                    .AnyAsync(c => (c.TenantUserId == user.Id
                            || c.ContractTenants.Any(ctn => ctn.TenantUserId == user.Id && ctn.MoveOutDate == null))
                        && c.Room!.Building!.OwnerId == callerId, ct),
                _ => false
            };

            if (!canViewUser)
                throw new ForbiddenException("Người dùng này không thuộc tòa nhà nào của bạn.");
        }

        // Populate assigned buildings for staff users
        IReadOnlyList<string>? assignedBuildingNames = null;
        if (user.Role == UserRole.Staff)
        {
            assignedBuildingNames = await _db.StaffAssignments
                .Where(sa => sa.StaffId == user.Id)
                .Select(sa => sa.Building!.Name)
                .ToListAsync(ct);
        }

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            CreatedAt = user.CreatedAt,
            AssignedBuildingNames = assignedBuildingNames
        };
    }
}
