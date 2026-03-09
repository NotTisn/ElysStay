using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Commands;

public class UpdateContractCommandHandler : IRequestHandler<UpdateContractCommand, ContractDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public UpdateContractCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<ContractDto> Handle(UpdateContractCommand request, CancellationToken cancellationToken)
    {
        _currentUser.GetRequiredUserId();

        var contract = await _db.Contracts
            .Include(c => c.Room!).ThenInclude(r => r.Building!)
            .Include(c => c.TenantUser!)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Contract", request.Id);

        // Must be active
        if (contract.Status != ContractStatus.Active)
            throw new ConflictException("Only active contracts can be updated.");

        // Building scope auth
        await _buildingScope.AuthorizeAsync(contract.Room!.BuildingId, cancellationToken);

        // Apply partial updates (CT-03: cannot change roomId, tenantUserId)
        var changed = false;

        if (request.EndDate.HasValue)
        {
            if (request.EndDate.Value <= contract.StartDate)
                throw new BadRequestException("EndDate must be after StartDate.");

            contract.EndDate = request.EndDate.Value;
            changed = true;
        }

        if (request.MonthlyRent.HasValue)
        {
            if (request.MonthlyRent.Value <= 0)
                throw new BadRequestException("MonthlyRent must be greater than zero.");

            contract.MonthlyRent = request.MonthlyRent.Value;
            changed = true;
        }

        if (request.Note is not null)
        {
            contract.Note = request.Note;
            changed = true;
        }

        if (changed)
        {
            contract.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new ContractDto
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
            UpdatedAt = contract.UpdatedAt
        };
    }
}
