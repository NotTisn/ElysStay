using Application.Common.Interfaces;
using Application.Features.Contracts.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Queries;

/// <summary>
/// List roommates on a contract.
/// Includes those with MoveOutDate (for history).
/// All roles can access; TENANT restricted to own contracts.
/// </summary>
public record GetContractTenantsQuery(Guid ContractId) : IRequest<IReadOnlyList<ContractTenantDto>>;

public class GetContractTenantsQueryHandler : IRequestHandler<GetContractTenantsQuery, IReadOnlyList<ContractTenantDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetContractTenantsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ContractTenantDto>> Handle(GetContractTenantsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var contract = await _db.Contracts
            .AsNoTracking()
            .Include(c => c.Room!).ThenInclude(r => r.Building!)
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, cancellationToken)
            ?? throw new Common.Exceptions.NotFoundException("Contract", request.ContractId);

        // Tenant access check
        if (_currentUser.IsTenant)
        {
            var isTenantOnContract = contract.TenantUserId == userId ||
                await _db.ContractTenants.AnyAsync(
                    ct => ct.ContractId == request.ContractId && ct.TenantUserId == userId,
                    cancellationToken);

            if (!isTenantOnContract)
                throw new Common.Exceptions.ForbiddenException("You can only view your own contract's tenants.");
        }
        else
        {
            // Owner/Staff building scope
            var building = contract.Room!.Building!;
            if (_currentUser.IsOwner && building.OwnerId != userId)
                throw new Common.Exceptions.ForbiddenException("You do not own this building.");

            if (_currentUser.IsStaff)
            {
                var isAssigned = await _db.StaffAssignments
                    .AnyAsync(sa => sa.BuildingId == building.Id && sa.StaffId == userId, cancellationToken);
                if (!isAssigned)
                    throw new Common.Exceptions.ForbiddenException("You are not assigned to this building.");
            }
        }

        return await _db.ContractTenants
            .AsNoTracking()
            .Where(ct => ct.ContractId == request.ContractId)
            .Include(ct => ct.Tenant!)
            .OrderByDescending(ct => ct.IsMainTenant)
            .ThenBy(ct => ct.MoveInDate)
            .Select(ct => new ContractTenantDto
            {
                Id = ct.Id,
                TenantUserId = ct.TenantUserId,
                TenantName = ct.Tenant!.FullName,
                TenantEmail = ct.Tenant.Email,
                TenantPhone = ct.Tenant.Phone,
                IsMainTenant = ct.IsMainTenant,
                MoveInDate = ct.MoveInDate,
                MoveOutDate = ct.MoveOutDate
            })
            .ToListAsync(cancellationToken);
    }
}
