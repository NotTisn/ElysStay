using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Services.Commands;

/// <summary>
/// SD-03: Deactivates a service (sets IsActive = false). Does not hard delete.
/// </summary>
public record DeactivateServiceCommand(Guid Id) : IRequest;

public class DeactivateServiceCommandHandler : IRequestHandler<DeactivateServiceCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeactivateServiceCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeactivateServiceCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsOwner)
            throw new ForbiddenException("Only the owner can deactivate services.");

        var service = await _db.Services
            .Include(s => s.Building)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Service {request.Id} not found.");

        if (service.Building?.OwnerId != _currentUser.GetRequiredUserId())
            throw new ForbiddenException("You do not own this building.");

        service.IsActive = false;
        service.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
