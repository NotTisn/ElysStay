using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Buildings.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Buildings.Queries;

public class GetBuildingsQueryHandler : IRequestHandler<GetBuildingsQuery, PagedResult<BuildingDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetBuildingsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<BuildingDto>> Handle(GetBuildingsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        IQueryable<Domain.Entities.Building> query = _db.Buildings.AsNoTracking();

        // AUTH-05: Scope by role
        if (_currentUser.IsOwner)
        {
            query = query.Where(b => b.OwnerId == userId);
        }
        else if (_currentUser.IsStaff)
        {
            // Staff only sees buildings they are assigned to
            query = query.Where(b => b.BuildingStaffs.Any(bs => bs.StaffId == userId));
        }
        else
        {
            throw new ForbiddenException("Tenants cannot list buildings.");
        }

        // Filters
        if (!string.IsNullOrWhiteSpace(request.Name))
            query = query.Where(b => b.Name.Contains(request.Name));

        if (!string.IsNullOrWhiteSpace(request.Address))
            query = query.Where(b => b.Address.Contains(request.Address));

        // Sorting
        query = ApplySort(query, request.Sort);

        // Pagination
        var paging = new PagedQuery { Page = request.Page, PageSize = request.PageSize };

        var pagedResult = await query
            .Select(b => new BuildingDto
            {
                Id = b.Id,
                OwnerId = b.OwnerId,
                Name = b.Name,
                Address = b.Address,
                Description = b.Description,
                TotalFloors = b.TotalFloors,
                InvoiceDueDay = b.InvoiceDueDay,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt
            })
            .ToPagedResultAsync(paging, cancellationToken);

        return pagedResult;
    }

    private static IQueryable<Domain.Entities.Building> ApplySort(IQueryable<Domain.Entities.Building> query, string sort)
    {
        var parts = sort.Split(':');
        var field = parts[0].ToLowerInvariant();
        var desc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return field switch
        {
            "name" => desc ? query.OrderByDescending(b => b.Name) : query.OrderBy(b => b.Name),
            "address" => desc ? query.OrderByDescending(b => b.Address) : query.OrderBy(b => b.Address),
            "updatedat" => desc ? query.OrderByDescending(b => b.UpdatedAt) : query.OrderBy(b => b.UpdatedAt),
            _ => desc ? query.OrderByDescending(b => b.CreatedAt) : query.OrderBy(b => b.CreatedAt) // default: createdAt
        };
    }
}
