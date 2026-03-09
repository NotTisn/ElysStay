using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Contracts.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Queries;

public class GetContractsQueryHandler : IRequestHandler<GetContractsQuery, PagedResult<ContractDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetContractsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<ContractDto>> Handle(GetContractsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var query = _db.Contracts.AsNoTracking().AsQueryable();

        // Scope by role
        if (_currentUser.IsOwner)
        {
            query = query.Where(c => c.Room!.Building!.OwnerId == userId);
        }
        else if (_currentUser.IsStaff)
        {
            query = query.Where(c => c.Room!.Building!.BuildingStaffs.Any(bs => bs.StaffId == userId));
        }
        else if (_currentUser.IsTenant)
        {
            // Tenant sees only contracts where they are the main tenant or a roommate
            query = query.Where(c =>
                c.TenantUserId == userId ||
                c.ContractTenants.Any(ct => ct.TenantUserId == userId));
        }

        // Filters
        if (request.BuildingId.HasValue)
            query = query.Where(c => c.Room!.BuildingId == request.BuildingId.Value);

        if (request.RoomId.HasValue)
            query = query.Where(c => c.RoomId == request.RoomId.Value);

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ContractStatus>(request.Status, true, out var status))
            query = query.Where(c => c.Status == status);

        // Sort
        query = ApplySort(query, request.Sort);

        // Paginate
        return await query
            .Select(c => new ContractDto
            {
                Id = c.Id,
                RoomId = c.RoomId,
                RoomNumber = c.Room!.RoomNumber,
                BuildingId = c.Room.BuildingId,
                BuildingName = c.Room.Building!.Name,
                TenantUserId = c.TenantUserId,
                TenantName = c.TenantUser!.FullName,
                ReservationId = c.ReservationId,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                MoveInDate = c.MoveInDate,
                MonthlyRent = c.MonthlyRent,
                DepositAmount = c.DepositAmount,
                DepositStatus = c.DepositStatus.ToString(),
                Status = c.Status.ToString(),
                TerminationDate = c.TerminationDate,
                TerminationNote = c.TerminationNote,
                RefundAmount = c.RefundAmount,
                Note = c.Note,
                CreatedBy = c.CreatedBy,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToPagedResultAsync(request, cancellationToken);
    }

    private static IQueryable<Domain.Entities.Contract> ApplySort(IQueryable<Domain.Entities.Contract> query, string sort)
    {
        var parts = sort.Split(':');
        var field = parts[0].ToLowerInvariant();
        var desc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return field switch
        {
            "startdate" => desc ? query.OrderByDescending(c => c.StartDate) : query.OrderBy(c => c.StartDate),
            "enddate" => desc ? query.OrderByDescending(c => c.EndDate) : query.OrderBy(c => c.EndDate),
            "monthlyrent" => desc ? query.OrderByDescending(c => c.MonthlyRent) : query.OrderBy(c => c.MonthlyRent),
            "status" => desc ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            _ => desc ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt)
        };
    }
}
