using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Queries;

/// <summary>
/// Gets a single contract by ID, including roommates.
/// All roles can access; TENANT restricted to own contracts.
/// </summary>
public record GetContractByIdQuery(Guid Id) : IRequest<ContractDetailDto>;

public class GetContractByIdQueryHandler : IRequestHandler<GetContractByIdQuery, ContractDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public GetContractByIdQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<ContractDetailDto> Handle(GetContractByIdQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var contract = await _db.Contracts
            .AsNoTracking()
            .Include(c => c.Room!).ThenInclude(r => r.Building!)
            .Include(c => c.TenantUser!)
            .Include(c => c.ContractTenants).ThenInclude(ct => ct.Tenant!)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Contract", request.Id);

        // TENANT: can only view their own contracts
        if (_currentUser.IsTenant)
        {
            var isTenantOnContract = contract.TenantUserId == userId ||
                contract.ContractTenants.Any(ct => ct.TenantUserId == userId);

            if (!isTenantOnContract)
                throw new ForbiddenException("You can only view your own contracts.");
        }
        else
        {
            // Owner/Staff: use building scope service for consistent auth
            await _buildingScope.AuthorizeAsync(contract.Room!.BuildingId, cancellationToken);
        }

        return new ContractDetailDto
        {
            Id = contract.Id,
            RoomId = contract.RoomId,
            RoomNumber = contract.Room!.RoomNumber,
            BuildingId = contract.Room.BuildingId,
            BuildingName = contract.Room.Building!.Name,
            TenantUserId = contract.TenantUserId,
            TenantName = contract.TenantUser!.FullName,
            ReservationId = contract.ReservationId,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            MoveInDate = contract.MoveInDate,
            MonthlyRent = contract.MonthlyRent,
            DepositAmount = contract.DepositAmount,
            DepositStatus = contract.DepositStatus.ToString(),
            Status = contract.Status.ToString(),
            TerminationDate = contract.TerminationDate,
            TerminationNote = contract.TerminationNote,
            RefundAmount = contract.RefundAmount,
            Note = contract.Note,
            CreatedBy = contract.CreatedBy,
            CreatedAt = contract.CreatedAt,
            UpdatedAt = contract.UpdatedAt,
            Tenants = contract.ContractTenants
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
                .ToList()
        };
    }
}
