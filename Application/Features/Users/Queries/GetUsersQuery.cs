using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Users.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Users.Queries;

/// <summary>
/// Lists users filtered by role with optional search and pagination.
/// GET /users/tenants → RoleFilter = Tenant.
/// GET /users/staff   → RoleFilter = Staff.
/// Auth: Owner sees all. Staff can list tenants only.
/// </summary>
public record GetUsersQuery : IRequest<PagedResult<UserDto>>
{
    public UserRole? RoleFilter { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string Sort { get; init; } = "createdAt:desc";
}

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PagedResult<UserDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUsersQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<UserDto>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        // Authorization: Owner can list anyone, Staff can list tenants only
        if (_currentUser.IsTenant)
            throw new ForbiddenException("Khách thuê không thể xem danh sách người dùng.");

        if (_currentUser.IsStaff && request.RoleFilter != UserRole.Tenant)
            throw new ForbiddenException("Nhân viên chỉ có thể xem danh sách khách thuê.");

        var userId = _currentUser.GetRequiredUserId();

        var query = _db.Users.AsNoTracking().AsQueryable();

        // Role filter
        if (request.RoleFilter.HasValue)
            query = query.Where(u => u.Role == request.RoleFilter.Value);

        // Building-scope filtering: Owner/Staff must only see users in their buildings
        if (_currentUser.IsOwner)
        {
            var ownedBuildingIds = _db.Buildings
                .Where(b => b.OwnerId == userId)
                .Select(b => b.Id);

            // Tenants: linked via Contract.TenantUserId or ContractTenants (roommates)
            var tenantIdsInOwnedBuildings = _db.Contracts
                .Where(c => ownedBuildingIds.Contains(c.Room!.BuildingId))
                .SelectMany(c => new[] { c.TenantUserId }
                    .Concat(c.ContractTenants.Select(ct => ct.TenantUserId)))
                .Distinct();

            // Staff: linked via StaffAssignments
            var staffIdsInOwnedBuildings = _db.StaffAssignments
                .Where(sa => ownedBuildingIds.Contains(sa.BuildingId))
                .Select(sa => sa.StaffId)
                .Distinct();

            query = query.Where(u =>
                u.Role == Domain.Enums.UserRole.Owner
                    ? u.Id == userId // Owner sees only themselves for Owner role
                    : u.Role == Domain.Enums.UserRole.Staff
                        ? staffIdsInOwnedBuildings.Contains(u.Id)
                        : tenantIdsInOwnedBuildings.Contains(u.Id));
        }
        else if (_currentUser.IsStaff)
        {
            var assignedBuildingIds = _db.StaffAssignments
                .Where(sa => sa.StaffId == userId)
                .Select(sa => sa.BuildingId);

            var tenantIdsInAssignedBuildings = _db.Contracts
                .Where(c => assignedBuildingIds.Contains(c.Room!.BuildingId))
                .SelectMany(c => new[] { c.TenantUserId }
                    .Concat(c.ContractTenants.Select(ct => ct.TenantUserId)))
                .Distinct();

            query = query.Where(u => tenantIdsInAssignedBuildings.Contains(u.Id));
        }

        // Search by name, email, or phone
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(u =>
                u.FullName.Contains(term) ||
                u.Email.Contains(term) ||
                (u.Phone != null && u.Phone.Contains(term)));
        }

        // Sort
        query = ApplySort(query, request.Sort);

        // Paginate
        var paging = new PagedQuery { Page = request.Page, PageSize = request.PageSize };

        return await query
            .Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                Phone = u.Phone,
                AvatarUrl = u.AvatarUrl,
                Role = u.Role.ToString(),
                Status = u.Status.ToString(),
                CreatedAt = u.CreatedAt
            })
            .ToPagedResultAsync(paging, ct);
    }

    private static IQueryable<Domain.Entities.User> ApplySort(IQueryable<Domain.Entities.User> query, string sort)
    {
        var parts = sort.Split(':');
        var field = parts[0].ToLowerInvariant();
        var desc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return field switch
        {
            "fullname" or "name" => desc ? query.OrderByDescending(u => u.FullName) : query.OrderBy(u => u.FullName),
            "email" => desc ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            _ => desc ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt)
        };
    }
}
