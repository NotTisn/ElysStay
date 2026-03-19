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
            ?? throw new NotFoundException("User", request.Id);

        // Tenant: self only
        if (_currentUser.IsTenant && user.Id != callerId)
            throw new ForbiddenException("Tenants can only view their own profile.");

        if (_currentUser.IsStaff && user.Id != callerId)
        {
            if (user.Role != UserRole.Tenant)
                throw new ForbiddenException("Staff can only view their own profile and tenants in assigned buildings.");

            var canViewTenant = await _db.Contracts
                .AnyAsync(c => (c.TenantUserId == user.Id
                        || c.ContractTenants.Any(ctn => ctn.TenantUserId == user.Id && ctn.MoveOutDate == null))
                    && c.Room!.Building!.BuildingStaffs.Any(bs => bs.StaffId == callerId), ct);

            if (!canViewTenant)
                throw new ForbiddenException("This tenant does not belong to any of your assigned buildings.");
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
                throw new ForbiddenException("This user does not belong to any of your buildings.");
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
            CreatedAt = user.CreatedAt
        };
    }
}
