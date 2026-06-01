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
    private readonly IEmailService _emailService;

    public RenewContractCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope,
        IEmailService emailService)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
        _emailService = emailService;
    }

    public async Task<ContractDto> Handle(RenewContractCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var oldContract = await _db.Contracts
            .Include(c => c.Room!).ThenInclude(r => r.Building!)
            .Include(c => c.ContractTenants).ThenInclude(ct => ct.Tenant!)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Hợp đồng", request.Id);

        if (oldContract.Status != ContractStatus.Active)
            throw new ConflictException("Chỉ có thể gia hạn hợp đồng đang hoạt động.");

        // DESIGN: Deposit carry-over requires DepositStatus == Held.
        // If the deposit was already refunded or forfeited, a renewal would silently
        // create a new contract with a fictitious DepositIn payment (no real cash).
        // This guard ensures only genuinely held deposits are transferred.
        if (oldContract.DepositStatus != DepositStatus.Held)
            throw new ConflictException("Không thể gia hạn: tiền cọc không còn được giữ (trạng thái hiện tại: " + oldContract.DepositStatus + ").");

        // Building scope auth
        await _buildingScope.AuthorizeAsync(oldContract.Room!.BuildingId, cancellationToken);

        // New start date = old end date + 1
        var newStartDate = oldContract.EndDate.AddDays(1);

        if (request.NewEndDate <= newStartDate)
            throw new BadRequestException($"Ngày kết thúc mới phải sau {newStartDate}.");

        // 1. Terminate old contract (administrative — no deposit refund, no room change)
        var boundaryDate = newStartDate.AddDays(-1);
        oldContract.Status = ContractStatus.Terminated;
        oldContract.TerminationDate = boundaryDate;
        oldContract.TerminationNote = "Chấm dứt để gia hạn hợp đồng mới";
        oldContract.UpdatedAt = DateTime.UtcNow;
        // Deposit carries over — deposit status stays Held on old contract
        // (the deposit is logically transferred to the new contract)

        var oldMainTenant = oldContract.ContractTenants.First(ct => ct.IsMainTenant);

        // 2. Create new contract
        var newContract = new Contract
        {
            RoomId = oldContract.RoomId,
            StartDate = newStartDate,
            EndDate = request.NewEndDate,
            MoveInDate = newStartDate, // same as start for renewal
            MonthlyRent = request.NewMonthlyRent ?? oldContract.MonthlyRent,
            DepositAmount = oldContract.DepositAmount, // carries over
            DepositStatus = DepositStatus.Held,
            Status = ContractStatus.Active,
            Note = $"Gia hạn từ hợp đồng {oldContract.Id}",
            CreatedBy = userId,
        };
        _db.Contracts.Add(newContract);

        // Create balanced deposit audit trail for renewal carry-over:
        // 1. DEPOSIT_REFUND on old contract (closes out old deposit liability)
        // 2. DEPOSIT_IN on new contract (opens new deposit liability)
        // Without both sides, PnL double-counts the deposit.
        if (newContract.DepositAmount > 0)
        {
            _db.Payments.Add(new Payment
            {
                ContractId = oldContract.Id,
                Type = PaymentType.DepositRefund,
                Amount = newContract.DepositAmount,
                Note = $"Tiền cọc chuyển sang hợp đồng gia hạn",
                RecordedBy = userId,
                PaidAt = DateTime.UtcNow
            });

            _db.Payments.Add(new Payment
            {
                ContractId = newContract.Id,
                Type = PaymentType.DepositIn,
                Amount = newContract.DepositAmount,
                Note = $"Tiền cọc chuyển từ hợp đồng {oldContract.Id}",
                RecordedBy = userId,
                PaidAt = DateTime.UtcNow
            });
        }

        // 3. Copy active contract tenants to new contract, then mark old ones with MoveOutDate (SD-02)
        var activeTenants = oldContract.ContractTenants.Where(ct => ct.MoveOutDate == null).ToList();
        foreach (var ct in activeTenants)
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

        // SD-02: Set MoveOutDate on old contract tenants
        foreach (var ct in activeTenants)
        {
            ct.MoveOutDate = boundaryDate;
        }

        // Notify tenant about contract renewal
        _db.Notifications.Add(new Notification
        {
            UserId = oldMainTenant.TenantUserId,
            Title = "Hợp đồng đã gia hạn",
            Message = $"Hợp đồng phòng {oldContract.Room!.RoomNumber} tại {oldContract.Room.Building!.Name} đã được gia hạn đến {request.NewEndDate:dd/MM/yyyy}.",
            Type = Domain.Constants.NotificationTypes.ContractRenewed,
            ReferenceId = newContract.Id,
        });

        // Room stays OCCUPIED — no status change (CT-01)

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Contracts_RoomId_Active") == true)
        {
            throw new ConflictException("Phòng này đã có hợp đồng đang hoạt động. Phát hiện gia hạn đồng thời.");
        }

        // Best-effort email to tenant (after successful save)
        var (subject, html) = Application.Common.Email.EmailTemplates.ContractRenewed(
            oldMainTenant.Tenant!.FullName, oldContract.Room!.RoomNumber,
            oldContract.Room.Building!.Name, request.NewEndDate);
        await _emailService.TrySendAsync(oldMainTenant.Tenant.Email, oldMainTenant.Tenant.FullName, subject, html, cancellationToken);

        return new ContractDto
        {
            Id = newContract.Id,
            RoomId = newContract.RoomId,
            RoomNumber = oldContract.Room!.RoomNumber,
            BuildingId = oldContract.Room.BuildingId,
            BuildingName = oldContract.Room.Building!.Name,
            TenantUserId = oldMainTenant.TenantUserId,
            TenantName = oldMainTenant.Tenant!.FullName,
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
