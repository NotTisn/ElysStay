using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Services.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Services.Commands;

public record CreateServiceCommand : IRequest<ServiceDto>
{
    public Guid BuildingId { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required decimal UnitPrice { get; init; }
    public required bool IsMetered { get; init; }
}

public class CreateServiceCommandHandler : IRequestHandler<CreateServiceCommand, ServiceDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateServiceCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ServiceDto> Handle(CreateServiceCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsOwner)
            throw new ForbiddenException("Only the owner can create services.");

        var building = await _db.Buildings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BuildingId, cancellationToken)
            ?? throw new NotFoundException($"Building {request.BuildingId} not found.");

        if (building.OwnerId != _currentUser.GetRequiredUserId())
            throw new ForbiddenException("You do not own this building.");

        // UQ-05: Duplicate service name check within building
        var duplicateExists = await _db.Services
            .AnyAsync(s => s.BuildingId == request.BuildingId
                && s.Name.ToLower() == request.Name.Trim().ToLower(), cancellationToken);
        if (duplicateExists)
            throw new ConflictException($"A service named '{request.Name.Trim()}' already exists in this building.", "DUPLICATE_SERVICE_NAME");

        var service = new Service
        {
            BuildingId = request.BuildingId,
            Name = request.Name.Trim(),
            Unit = request.Unit.Trim(),
            UnitPrice = request.UnitPrice,
            IsMetered = request.IsMetered
        };

        _db.Services.Add(service);
        await _db.SaveChangesAsync(cancellationToken);

        return new ServiceDto
        {
            Id = service.Id,
            BuildingId = service.BuildingId,
            Name = service.Name,
            Unit = service.Unit,
            UnitPrice = service.UnitPrice,
            PreviousUnitPrice = service.PreviousUnitPrice,
            PriceUpdatedAt = service.PriceUpdatedAt,
            IsMetered = service.IsMetered,
            IsActive = service.IsActive,
            CreatedAt = service.CreatedAt,
            UpdatedAt = service.UpdatedAt
        };
    }
}
