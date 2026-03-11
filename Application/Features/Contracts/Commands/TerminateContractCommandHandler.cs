using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Commands;

public class TerminateContractCommandHandler : IRequestHandler<TerminateContractCommand, ContractDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public TerminateContractCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<ContractDto> Handle(TerminateContractCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var contract = await _db.Contracts
            .Include(c => c.Room!).ThenInclude(r => r.Building!)
            .Include(c => c.TenantUser!)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Contract", request.Id);

        // Must be active (SM-10)
        if (contract.Status != ContractStatus.Active)
            throw new ConflictException("Only active contracts can be terminated.");

        // Building scope auth
        await _buildingScope.AuthorizeAsync(contract.Room!.BuildingId, cancellationToken);

        // Validate deductions
        if (request.Deductions < 0)
            throw new BadRequestException("Deductions cannot be negative.");

        if (request.Deductions > contract.DepositAmount)
            throw new BadRequestException("Deductions cannot exceed deposit amount.");

        // Calculate refund (DEP-04)
        var refundAmount = contract.DepositAmount - request.Deductions;

        // Update contract
        contract.Status = ContractStatus.Terminated;
        contract.TerminationDate = request.TerminationDate;
        contract.TerminationNote = request.Note;
        contract.RefundAmount = refundAmount;
        contract.UpdatedAt = DateTime.UtcNow;

        // Update deposit status based on refund amount
        contract.DepositStatus = refundAmount switch
        {
            0 => DepositStatus.Forfeited,
            _ when refundAmount == contract.DepositAmount => DepositStatus.Refunded,
            _ => DepositStatus.PartiallyRefunded
        };

        // SM-04: Room → AVAILABLE (only if currently Occupied; preserve Maintenance flag)
        if (contract.Room!.Status == RoomStatus.Occupied)
        {
            contract.Room.Status = RoomStatus.Available;
            contract.Room.UpdatedAt = DateTime.UtcNow;
        }

        // Create DEPOSIT_REFUND payment (even if refund is 0, for audit trail)
        if (refundAmount > 0)
        {
            var refundPayment = new Payment
            {
                ContractId = contract.Id,
                Type = PaymentType.DepositRefund,
                Amount = refundAmount,
                Note = request.Note ?? "Deposit refund on contract termination",
                RecordedBy = userId,
                PaidAt = DateTime.UtcNow
            };
            _db.Payments.Add(refundPayment);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The room was modified by another operation. Please retry.");
        }

        return new ContractDto
        {
            Id = contract.Id,
            RoomId = contract.RoomId,
            RoomNumber = contract.Room.RoomNumber,
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
