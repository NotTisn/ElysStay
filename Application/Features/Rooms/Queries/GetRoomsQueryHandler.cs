using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Rooms.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Rooms.Queries;

public class GetRoomsQueryHandler : IRequestHandler<GetRoomsQuery, PagedResult<RoomDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetRoomsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<RoomDto>> Handle(GetRoomsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var query = _db.Rooms.AsNoTracking().AsQueryable();

        // Scope by role
        if (_currentUser.IsOwner)
        {
            query = query.Where(r => r.Building!.OwnerId == userId);
        }
        else if (_currentUser.IsStaff)
        {
            query = query.Where(r => r.Building!.BuildingStaffs.Any(bs => bs.StaffId == userId));
        }
        else
        {
            throw new ForbiddenException("Tenants cannot list rooms.");
        }

        // Filters
        if (request.BuildingId.HasValue)
            query = query.Where(r => r.BuildingId == request.BuildingId.Value);

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<RoomStatus>(request.Status, true, out var status))
            query = query.Where(r => r.Status == status);

        if (request.Floor.HasValue)
            query = query.Where(r => r.Floor == request.Floor.Value);

        // Sort
        query = ApplySort(query, request.Sort);

        // Paginate
        var paging = new PagedQuery { Page = request.Page, PageSize = request.PageSize };

        return await query
            .Select(r => new RoomDto
            {
                Id = r.Id,
                BuildingId = r.BuildingId,
                RoomNumber = r.RoomNumber,
                Floor = r.Floor,
                Area = r.Area,
                Price = r.Price,
                MaxOccupants = r.MaxOccupants,
                Description = r.Description,
                Status = r.Status.ToString(),
                Images = r.Images,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToPagedResultAsync(paging, cancellationToken);
    }

    private static IQueryable<Domain.Entities.Room> ApplySort(IQueryable<Domain.Entities.Room> query, string sort)
    {
        var parts = sort.Split(':');
        var field = parts[0].ToLowerInvariant();
        var desc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return field switch
        {
            "roomnumber" => desc ? query.OrderByDescending(r => r.RoomNumber) : query.OrderBy(r => r.RoomNumber),
            "floor" => desc ? query.OrderByDescending(r => r.Floor) : query.OrderBy(r => r.Floor),
            "price" => desc ? query.OrderByDescending(r => r.Price) : query.OrderBy(r => r.Price),
            "status" => desc ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            _ => desc ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt)
        };
    }
}
