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
            throw new ForbiddenException("Tenants cannot list users.");

        if (_currentUser.IsStaff && request.RoleFilter != UserRole.Tenant)
            throw new ForbiddenException("Staff can only list tenants.");

        var query = _db.Users.AsNoTracking().AsQueryable();

        // Role filter
        if (request.RoleFilter.HasValue)
            query = query.Where(u => u.Role == request.RoleFilter.Value);

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
