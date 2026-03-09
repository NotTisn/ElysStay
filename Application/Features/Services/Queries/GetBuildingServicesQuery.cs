using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Services.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Services.Queries;

public record GetBuildingServicesQuery(Guid BuildingId) : IRequest<List<ServiceDto>>;

public class GetBuildingServicesQueryHandler : IRequestHandler<GetBuildingServicesQuery, List<ServiceDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IBuildingScopeService _buildingScope;

    public GetBuildingServicesQueryHandler(IApplicationDbContext db, IBuildingScopeService buildingScope)
    {
        _db = db;
        _buildingScope = buildingScope;
    }

    public async Task<List<ServiceDto>> Handle(GetBuildingServicesQuery request, CancellationToken cancellationToken)
    {
        // Verify building exists
        var exists = await _db.Buildings.AnyAsync(b => b.Id == request.BuildingId, cancellationToken);
        if (!exists)
            throw new NotFoundException($"Building {request.BuildingId} not found.");

        await _buildingScope.AuthorizeAsync(request.BuildingId, cancellationToken);

        return await _db.Services
            .AsNoTracking()
            .Where(s => s.BuildingId == request.BuildingId)
            .OrderBy(s => s.Name)
            .Select(s => new ServiceDto
            {
                Id = s.Id,
                BuildingId = s.BuildingId,
                Name = s.Name,
                Unit = s.Unit,
                UnitPrice = s.UnitPrice,
                PreviousUnitPrice = s.PreviousUnitPrice,
                PriceUpdatedAt = s.PriceUpdatedAt,
                IsMetered = s.IsMetered,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
