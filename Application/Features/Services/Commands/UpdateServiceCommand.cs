using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Services.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Services.Commands;

public record UpdateServiceCommand : IRequest<ServiceDto>
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Unit { get; init; }
    public decimal? UnitPrice { get; init; }
    public bool? IsMetered { get; init; }
}

public class UpdateServiceCommandHandler : IRequestHandler<UpdateServiceCommand, ServiceDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateServiceCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ServiceDto> Handle(UpdateServiceCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsOwner)
            throw new ForbiddenException("Only the owner can update services.");

        var service = await _db.Services
            .Include(s => s.Building)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Service {request.Id} not found.");

        if (service.Building?.OwnerId != _currentUser.GetRequiredUserId())
            throw new ForbiddenException("You do not own this building.");

        if (request.Name is not null)
            service.Name = request.Name.Trim();

        if (request.Unit is not null)
            service.Unit = request.Unit.Trim();

        // PR-03: Track price change
        if (request.UnitPrice.HasValue && request.UnitPrice.Value != service.UnitPrice)
        {
            service.PreviousUnitPrice = service.UnitPrice;
            service.PriceUpdatedAt = DateTime.UtcNow;
            service.UnitPrice = request.UnitPrice.Value;
        }

        if (request.IsMetered.HasValue)
            service.IsMetered = request.IsMetered.Value;

        service.UpdatedAt = DateTime.UtcNow;

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
