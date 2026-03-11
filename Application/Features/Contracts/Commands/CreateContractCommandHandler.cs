using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Commands;

public class CreateContractCommandHandler : IRequestHandler<CreateContractCommand, ContractDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public CreateContractCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<ContractDto> Handle(CreateContractCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        // Load room with building (explicit soft-delete check)
        var room = await _db.Rooms
            .Include(r => r.Building!)
            .FirstOrDefaultAsync(r => r.Id == request.RoomId && r.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("Room", request.RoomId);

        // Building scope authorization
        await _buildingScope.AuthorizeAsync(room.BuildingId, cancellationToken);

        // Verify tenant user exists and has Tenant role
        var tenantUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.TenantUserId && u.Role == UserRole.Tenant, cancellationToken)
            ?? throw new NotFoundException("Tenant user", request.TenantUserId);

        // UQ-01: Only 1 ACTIVE contract per room
        var hasActiveContract = await _db.Contracts
            .AnyAsync(c => c.RoomId == request.RoomId && c.Status == ContractStatus.Active, cancellationToken);

        if (hasActiveContract)
            throw new ConflictException("This room already has an active contract.", "ROOM_OCCUPIED");

        // Validate room status for transition
        RoomReservation? reservation = null;
        if (request.ReservationId.HasValue)
        {
            // Contract from reservation: SM-02 (BOOKED → OCCUPIED)
            reservation = await _db.RoomReservations
                .FirstOrDefaultAsync(r => r.Id == request.ReservationId.Value, cancellationToken)
                ?? throw new NotFoundException("Reservation", request.ReservationId.Value);

            if (reservation.RoomId != request.RoomId)
                throw new BadRequestException("Reservation does not belong to the specified room.");

            if (reservation.Status != ReservationStatus.Confirmed)
                throw new ConflictException("Reservation must be Confirmed before creating a contract. Current: " + reservation.Status);

            if (room.Status != RoomStatus.Booked)
                throw new ConflictException($"Room must be in Booked status to create a contract from reservation. Current: {room.Status}");
        }
        else
        {
            // Direct contract: SM-06 (AVAILABLE → OCCUPIED)
            if (room.Status != RoomStatus.Available)
                throw new ConflictException($"Room must be Available to create a direct contract. Current: {room.Status}");
        }

        // Create the contract
        var contract = new Contract
        {
            RoomId = request.RoomId,
            TenantUserId = request.TenantUserId,
            ReservationId = request.ReservationId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MoveInDate = request.MoveInDate,
            MonthlyRent = request.MonthlyRent,
            DepositAmount = request.DepositAmount,
            DepositStatus = DepositStatus.Held,
            Status = ContractStatus.Active,
            Note = request.Note,
            CreatedBy = userId,
        };

        _db.Contracts.Add(contract);

        // Auto-create ContractTenant (IsMainTenant=true)
        var mainTenant = new ContractTenant
        {
            ContractId = contract.Id,
            TenantUserId = request.TenantUserId,
            IsMainTenant = true,
            MoveInDate = request.MoveInDate
        };
        _db.ContractTenants.Add(mainTenant);

        // Room status transition
        room.Status = RoomStatus.Occupied;
        room.UpdatedAt = DateTime.UtcNow;

        // DEP-02/DEP-03: Deposit handling
        if (reservation != null)
        {
            // Reservation → CONVERTED
            reservation.Status = ReservationStatus.Converted;
            reservation.UpdatedAt = DateTime.UtcNow;

            // Reservation deposit → Payment(DEPOSIT_IN) — only if amount > 0
            if (reservation.DepositAmount > 0)
            {
                var reservationDepositPayment = new Payment
                {
                    ContractId = contract.Id,
                    Type = PaymentType.DepositIn,
                    Amount = reservation.DepositAmount,
                    Note = "Deposit transferred from reservation",
                    RecordedBy = userId,
                    PaidAt = DateTime.UtcNow
                };
                _db.Payments.Add(reservationDepositPayment);
            }

            // If contract deposit > reservation deposit, create additional payment
            if (request.DepositAmount > reservation.DepositAmount)
            {
                var additionalDeposit = new Payment
                {
                    ContractId = contract.Id,
                    Type = PaymentType.DepositIn,
                    Amount = request.DepositAmount - reservation.DepositAmount,
                    Note = "Additional deposit payment",
                    RecordedBy = userId,
                    PaidAt = DateTime.UtcNow
                };
                _db.Payments.Add(additionalDeposit);
            }
        }
        else if (request.DepositAmount > 0)
        {
            // Direct deposit → Payment(DEPOSIT_IN) — only if amount > 0
            var depositPayment = new Payment
            {
                ContractId = contract.Id,
                Type = PaymentType.DepositIn,
                Amount = request.DepositAmount,
                Note = "Contract deposit",
                RecordedBy = userId,
                PaidAt = DateTime.UtcNow
            };
            _db.Payments.Add(depositPayment);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "Room was modified by another user. Please retry.",
                "CONCURRENCY_CONFLICT");
        }

        return new ContractDto
        {
            Id = contract.Id,
            RoomId = contract.RoomId,
            RoomNumber = room.RoomNumber,
            BuildingId = room.BuildingId,
            BuildingName = room.Building!.Name,
            TenantUserId = contract.TenantUserId,
            TenantName = tenantUser.FullName,
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
