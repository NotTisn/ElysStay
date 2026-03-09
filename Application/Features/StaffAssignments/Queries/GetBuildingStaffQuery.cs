using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.StaffAssignments.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.StaffAssignments.Queries;

public record GetBuildingStaffQuery(Guid BuildingId) : IRequest<List<StaffAssignmentDto>>;

public class GetBuildingStaffQueryHandler : IRequestHandler<GetBuildingStaffQuery, List<StaffAssignmentDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IBuildingScopeService _buildingScope;

    public GetBuildingStaffQueryHandler(IApplicationDbContext db, IBuildingScopeService buildingScope)
    {
        _db = db;
        _buildingScope = buildingScope;
    }

    public async Task<List<StaffAssignmentDto>> Handle(GetBuildingStaffQuery request, CancellationToken cancellationToken)
    {
        var exists = await _db.Buildings.AnyAsync(b => b.Id == request.BuildingId, cancellationToken);
        if (!exists)
            throw new NotFoundException($"Building {request.BuildingId} not found.");

        await _buildingScope.AuthorizeAsync(request.BuildingId, cancellationToken);

        return await _db.StaffAssignments
            .AsNoTracking()
            .Where(sa => sa.BuildingId == request.BuildingId)
            .Include(sa => sa.Staff)
            .Select(sa => new StaffAssignmentDto
            {
                StaffId = sa.StaffId,
                Email = sa.Staff!.Email,
                FullName = sa.Staff.FullName,
                Phone = sa.Staff.Phone,
                AssignedAt = sa.AssignedAt
            })
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);
    }
}
