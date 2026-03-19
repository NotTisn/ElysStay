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
            .Include(c => c.ContractTenants)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Contract", request.Id);

        // Must be active (SM-10)
        if (contract.Status != ContractStatus.Active)
            throw new ConflictException("Only active contracts can be terminated.");

        // Building scope auth
        await _buildingScope.AuthorizeAsync(contract.Room!.BuildingId, cancellationToken);

        // Validate termination date is not before contract start
        if (request.TerminationDate < contract.StartDate)
            throw new BadRequestException("Termination date cannot be before the contract start date.");

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

        // SD-02: Set MoveOutDate on all active contract tenants
        foreach (var ct in contract.ContractTenants.Where(ct => ct.MoveOutDate == null))
        {
            ct.MoveOutDate = request.TerminationDate;
        }

        // SM-04: Room → AVAILABLE (only if currently Occupied; preserve Maintenance flag)
        if (contract.Room!.Status == RoomStatus.Occupied)
        {
            contract.Room.Status = RoomStatus.Available;
            contract.Room.UpdatedAt = DateTime.UtcNow;
        }

        // Auto-void DRAFT/SENT/OVERDUE invoices for billing periods after termination
        // These invoices are no longer applicable and should not remain outstanding
        var futureInvoices = await _db.Invoices
            .Where(i => i.ContractId == contract.Id
                && (i.Status == InvoiceStatus.Draft || i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.Overdue)
                && (i.BillingYear > request.TerminationDate.Year
                    || (i.BillingYear == request.TerminationDate.Year && i.BillingMonth > request.TerminationDate.Month)))
            .ToListAsync(cancellationToken);

        foreach (var invoice in futureInvoices)
        {
            invoice.Status = InvoiceStatus.Void;
            invoice.UpdatedAt = DateTime.UtcNow;
        }

        // Always create audit trail payment for deposit disposition
        if (refundAmount > 0)
        {
            _db.Payments.Add(new Payment
            {
                ContractId = contract.Id,
                Type = PaymentType.DepositRefund,
                Amount = refundAmount,
                Note = request.Note ?? "Deposit refund on contract termination",
                RecordedBy = userId,
                PaidAt = DateTime.UtcNow
            });
        }
        else if (contract.DepositAmount > 0)
        {
            // Full forfeit — still record for audit trail (DEP-04)
            _db.Payments.Add(new Payment
            {
                ContractId = contract.Id,
                Type = PaymentType.DepositRefund,
                Amount = 0,
                Note = request.Note ?? $"Deposit fully forfeited ({contract.DepositAmount:N0} VND deducted)",
                RecordedBy = userId,
                PaidAt = DateTime.UtcNow
            });
        }

        // Notify tenant about contract termination
        _db.Notifications.Add(new Notification
        {
            UserId = contract.TenantUserId,
            Title = "Hợp đồng đã chấm dứt",
            Message = $"Hợp đồng phòng {contract.Room!.RoomNumber} tại {contract.Room.Building!.Name} đã được chấm dứt.",
            Type = "CONTRACT_TERMINATED",
            ReferenceId = contract.Id,
        });

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
