using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Rooms.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Rooms.Queries;

public class GetRoomByIdQueryHandler : IRequestHandler<GetRoomByIdQuery, RoomDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public GetRoomByIdQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<RoomDto> Handle(GetRoomByIdQuery request, CancellationToken cancellationToken)
    {
        var room = await _db.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Room {request.Id} not found.");

        // Tenant: can only view rooms they have an active contract for
        if (_currentUser.IsTenant)
        {
            var userId = _currentUser.GetRequiredUserId();
            var hasContract = await _db.Contracts
                .AnyAsync(c => c.RoomId == request.Id
                    && c.Status == ContractStatus.Active
                    && (c.TenantUserId == userId || c.ContractTenants.Any(ct => ct.TenantUserId == userId)),
                    cancellationToken);
            if (!hasContract)
                throw new ForbiddenException("You can only view rooms you have an active contract for.");
        }
        else
        {
            await _buildingScope.AuthorizeAsync(room.BuildingId, cancellationToken);
        }

        return new RoomDto
        {
            Id = room.Id,
            BuildingId = room.BuildingId,
            RoomNumber = room.RoomNumber,
            Floor = room.Floor,
            Area = room.Area,
            Price = room.Price,
            MaxOccupants = room.MaxOccupants,
            Description = room.Description,
            Status = room.Status.ToString(),
            Images = room.Images,
            CreatedAt = room.CreatedAt,
            UpdatedAt = room.UpdatedAt
        };
    }
}
