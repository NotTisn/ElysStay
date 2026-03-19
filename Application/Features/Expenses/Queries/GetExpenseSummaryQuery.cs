using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Expenses.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Expenses.Queries;

/// <summary>
/// Returns aggregate totals for expenses using the same scope and filters as the paged list.
/// </summary>
public class GetExpenseSummaryQuery : IRequest<ExpenseSummaryDto>
{
    public Guid? BuildingId { get; set; }
    public Guid? RoomId { get; set; }
    public string? Category { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

public class GetExpenseSummaryQueryHandler : IRequestHandler<GetExpenseSummaryQuery, ExpenseSummaryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetExpenseSummaryQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ExpenseSummaryDto> Handle(GetExpenseSummaryQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var query = _db.Expenses.AsNoTracking().AsQueryable();

        if (_currentUser.IsOwner)
        {
            query = query.Where(e => e.Building!.OwnerId == userId);
        }
        else if (_currentUser.IsStaff)
        {
            var assignedBuildingIds = _db.StaffAssignments
                .Where(sa => sa.StaffId == userId)
                .Select(sa => sa.BuildingId);
            query = query.Where(e => assignedBuildingIds.Contains(e.BuildingId));
        }
        else
        {
            throw new ForbiddenException("Tenants cannot access expense summaries.");
        }

        if (request.BuildingId.HasValue)
            query = query.Where(e => e.BuildingId == request.BuildingId.Value);

        if (request.RoomId.HasValue)
            query = query.Where(e => e.RoomId == request.RoomId.Value);

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var normalizedCategory = request.Category.Trim();
            query = query.Where(e => e.Category.ToLower() == normalizedCategory.ToLower());
        }

        if (request.FromDate.HasValue)
            query = query.Where(e => e.ExpenseDate >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(e => e.ExpenseDate <= request.ToDate.Value);

        var aggregates = await query
            .GroupBy(_ => 1)
            .Select(group => new ExpenseSummaryDto(group.Sum(e => e.Amount), group.Count()))
            .FirstOrDefaultAsync(ct);

        return aggregates ?? new ExpenseSummaryDto(0, 0);
    }
}