using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Expenses.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Expenses.Queries;

/// <summary>
/// GET /expenses — List expenses (paginated).
/// Auth: Owner/Staff only. Staff building-scoped (AUTH-05).
/// </summary>
public class GetExpensesQuery : PagedQuery, IRequest<PagedResult<ExpenseDto>>
{
    public Guid? BuildingId { get; set; }
    public Guid? RoomId { get; set; }
    public string? Category { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
}

public class GetExpensesQueryHandler : IRequestHandler<GetExpensesQuery, PagedResult<ExpenseDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetExpensesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<ExpenseDto>> Handle(GetExpensesQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var query = _db.Expenses
            .AsNoTracking()
            .Include(e => e.Building)
            .Include(e => e.Room)
            .Include(e => e.Recorder)
            .AsQueryable();

        // Role-scope
        if (_currentUser.IsOwner)
        {
            // Owner sees expenses for buildings they own
            query = query.Where(e => e.Building!.OwnerId == userId);
        }
        else if (_currentUser.IsStaff)
        {
            // Staff sees only expenses for assigned buildings (AUTH-05)
            var assignedBuildingIds = _db.StaffAssignments
                .Where(sa => sa.StaffId == userId)
                .Select(sa => sa.BuildingId);
            query = query.Where(e => assignedBuildingIds.Contains(e.BuildingId));
        }

        // Filters
        if (request.BuildingId.HasValue)
            query = query.Where(e => e.BuildingId == request.BuildingId.Value);

        if (request.RoomId.HasValue)
            query = query.Where(e => e.RoomId == request.RoomId.Value);

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(e => e.Category == request.Category);

        if (request.FromDate.HasValue)
            query = query.Where(e => e.ExpenseDate >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(e => e.ExpenseDate <= request.ToDate.Value);

        // Sort
        query = request.SortBy?.ToLowerInvariant() switch
        {
            "amount" => request.SortDesc ? query.OrderByDescending(e => e.Amount) : query.OrderBy(e => e.Amount),
            "expensedate" => request.SortDesc ? query.OrderByDescending(e => e.ExpenseDate) : query.OrderBy(e => e.ExpenseDate),
            "category" => request.SortDesc ? query.OrderByDescending(e => e.Category) : query.OrderBy(e => e.Category),
            _ => query.OrderByDescending(e => e.ExpenseDate)
        };

        var pagedResult = await query
            .Select(e => new ExpenseDto(
                e.Id,
                e.BuildingId,
                e.Building!.Name,
                e.RoomId,
                e.Room != null ? e.Room.RoomNumber : null,
                e.Category,
                e.Description,
                e.Amount,
                e.ReceiptUrl,
                e.ExpenseDate,
                e.RecordedBy,
                e.Recorder != null ? e.Recorder.FullName : null,
                e.CreatedAt,
                e.UpdatedAt))
            .ToPagedResultAsync(request, ct);

        return pagedResult;
    }
}
