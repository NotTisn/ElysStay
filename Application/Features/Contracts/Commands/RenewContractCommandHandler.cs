using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Commands;

public class RenewContractCommandHandler : IRequestHandler<RenewContractCommand, ContractDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public RenewContractCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<ContractDto> Handle(RenewContractCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var oldContract = await _db.Contracts
            .Include(c => c.Room!).ThenInclude(r => r.Building!)
            .Include(c => c.TenantUser!)
            .Include(c => c.ContractTenants)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Contract", request.Id);

        if (oldContract.Status != ContractStatus.Active)
            throw new ConflictException("Only active contracts can be renewed.");

        // Building scope auth
        await _buildingScope.AuthorizeAsync(oldContract.Room!.BuildingId, cancellationToken);

        // New start date = old end date + 1
        var newStartDate = oldContract.EndDate.AddDays(1);

        if (request.NewEndDate <= newStartDate)
            throw new BadRequestException($"New end date must be after {newStartDate}.");

        // 1. Terminate old contract (administrative — no deposit refund, no room change)
        oldContract.Status = ContractStatus.Terminated;
        oldContract.TerminationDate = DateOnly.FromDateTime(DateTime.UtcNow); // Actual renewal date
        oldContract.TerminationNote = "Administratively terminated for renewal";
        oldContract.UpdatedAt = DateTime.UtcNow;
        // Deposit carries over — deposit status stays Held on old contract
        // (the deposit is logically transferred to the new contract)

        // 2. Create new contract
        var newContract = new Contract
        {
            RoomId = oldContract.RoomId,
            TenantUserId = oldContract.TenantUserId,
            StartDate = newStartDate,
            EndDate = request.NewEndDate,
            MoveInDate = newStartDate, // same as start for renewal
            MonthlyRent = request.NewMonthlyRent ?? oldContract.MonthlyRent,
            DepositAmount = oldContract.DepositAmount, // carries over
            DepositStatus = DepositStatus.Held,
            Status = ContractStatus.Active,
            Note = $"Renewed from contract {oldContract.Id}",
            CreatedBy = userId,
        };
        _db.Contracts.Add(newContract);

        // Create deposit audit trail for the renewed contract
        if (newContract.DepositAmount > 0)
        {
            _db.Payments.Add(new Payment
            {
                ContractId = newContract.Id,
                Type = PaymentType.DepositIn,
                Amount = newContract.DepositAmount,
                Note = $"Deposit carried over from contract {oldContract.Id} (renewal)",
                RecordedBy = userId,
                PaidAt = DateTime.UtcNow
            });
        }

        // 3. Copy contract tenants to new contract
        foreach (var ct in oldContract.ContractTenants.Where(ct => ct.MoveOutDate == null))
        {
            var newTenant = new ContractTenant
            {
                ContractId = newContract.Id,
                TenantUserId = ct.TenantUserId,
                IsMainTenant = ct.IsMainTenant,
                MoveInDate = newStartDate
            };
            _db.ContractTenants.Add(newTenant);
        }

        // Room stays OCCUPIED — no status change (CT-01)

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Contracts_RoomId_Active") == true)
        {
            throw new ConflictException("An active contract already exists for this room. Concurrent renewal detected.");
        }

        return new ContractDto
        {
            Id = newContract.Id,
            RoomId = newContract.RoomId,
            RoomNumber = oldContract.Room!.RoomNumber,
            BuildingId = oldContract.Room.BuildingId,
            BuildingName = oldContract.Room.Building!.Name,
            TenantUserId = newContract.TenantUserId,
            TenantName = oldContract.TenantUser!.FullName,
            ReservationId = newContract.ReservationId,
            StartDate = newContract.StartDate,
            EndDate = newContract.EndDate,
            MoveInDate = newContract.MoveInDate,
            MonthlyRent = newContract.MonthlyRent,
            DepositAmount = newContract.DepositAmount,
            DepositStatus = newContract.DepositStatus.ToString(),
            Status = newContract.Status.ToString(),
            TerminationDate = newContract.TerminationDate,
            TerminationNote = newContract.TerminationNote,
            RefundAmount = newContract.RefundAmount,
            Note = newContract.Note,
            CreatedBy = newContract.CreatedBy,
            CreatedAt = newContract.CreatedAt,
            UpdatedAt = newContract.UpdatedAt
        };
    }
}
