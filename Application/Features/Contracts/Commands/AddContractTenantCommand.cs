using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Commands;

/// <summary>
/// Adds a roommate (ContractTenant) to an existing contract.
/// Owner/Staff only.
/// </summary>
public record AddContractTenantCommand : IRequest<ContractTenantDto>
{
    public Guid ContractId { get; init; }
    public required Guid TenantUserId { get; init; }
    public required DateOnly MoveInDate { get; init; }
}

public class AddContractTenantCommandHandler : IRequestHandler<AddContractTenantCommand, ContractTenantDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public AddContractTenantCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<ContractTenantDto> Handle(AddContractTenantCommand request, CancellationToken cancellationToken)
    {
        _currentUser.GetRequiredUserId();

        var contract = await _db.Contracts
            .Include(c => c.Room!)
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, cancellationToken)
            ?? throw new NotFoundException("Contract", request.ContractId);

        if (contract.Status != ContractStatus.Active)
            throw new ConflictException("Roommates can only be added to active contracts.");

        // Building scope auth
        await _buildingScope.AuthorizeAsync(contract.Room!.BuildingId, cancellationToken);

        // Verify tenant user exists
        var tenantUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.TenantUserId && u.Role == UserRole.Tenant, cancellationToken)
            ?? throw new NotFoundException("Tenant user", request.TenantUserId);

        // Check if already on contract (active — no MoveOutDate)
        var alreadyOnContract = await _db.ContractTenants
            .AnyAsync(ct => ct.ContractId == request.ContractId &&
                           ct.TenantUserId == request.TenantUserId &&
                           ct.MoveOutDate == null, cancellationToken);

        if (alreadyOnContract)
            throw new ConflictException("This tenant is already an active roommate on this contract.");

        var contractTenant = new ContractTenant
        {
            ContractId = request.ContractId,
            TenantUserId = request.TenantUserId,
            IsMainTenant = false,
            MoveInDate = request.MoveInDate
        };
        _db.ContractTenants.Add(contractTenant);
        await _db.SaveChangesAsync(cancellationToken);

        return new ContractTenantDto
        {
            Id = contractTenant.Id,
            TenantUserId = contractTenant.TenantUserId,
            TenantName = tenantUser.FullName,
            TenantEmail = tenantUser.Email,
            TenantPhone = tenantUser.Phone,
            IsMainTenant = contractTenant.IsMainTenant,
            MoveInDate = contractTenant.MoveInDate,
            MoveOutDate = contractTenant.MoveOutDate
        };
    }
}
