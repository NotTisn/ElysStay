using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Commands;

/// <summary>
/// Soft-removes a roommate from a contract (SD-02).
/// Sets MoveOutDate. Cannot remove the main tenant (IsMainTenant=true).
/// Owner/Staff only.
/// </summary>
public record RemoveContractTenantCommand : IRequest<Unit>
{
    public Guid ContractId { get; init; }
    public Guid TenantId { get; init; }
}

public class RemoveContractTenantCommandHandler : IRequestHandler<RemoveContractTenantCommand, Unit>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public RemoveContractTenantCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<Unit> Handle(RemoveContractTenantCommand request, CancellationToken cancellationToken)
    {
        _currentUser.GetRequiredUserId();

        var contract = await _db.Contracts
            .Include(c => c.Room!)
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, cancellationToken)
            ?? throw new NotFoundException("Contract", request.ContractId);

        if (contract.Status != ContractStatus.Active)
            throw new ConflictException("Roommates can only be removed from active contracts.");

        // Building scope auth
        await _buildingScope.AuthorizeAsync(contract.Room!.BuildingId, cancellationToken);

        var contractTenant = await _db.ContractTenants
            .FirstOrDefaultAsync(ct => ct.ContractId == request.ContractId &&
                                       ct.TenantUserId == request.TenantId &&
                                       ct.MoveOutDate == null, cancellationToken)
            ?? throw new NotFoundException("Active contract tenant", request.TenantId);

        // SD-02: Cannot remove main tenant
        if (contractTenant.IsMainTenant)
            throw new ConflictException("Cannot remove the main tenant from a contract. Terminate the contract instead.");

        // Soft remove: set MoveOutDate
        contractTenant.MoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
