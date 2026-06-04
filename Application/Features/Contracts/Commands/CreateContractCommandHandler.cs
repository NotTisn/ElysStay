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
    private readonly IEmailService _emailService;

    public CreateContractCommandHandler(
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

    public async Task<ContractDto> Handle(CreateContractCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        // Load room with building (explicit soft-delete check)
        var room = await _db.Rooms
            .Include(r => r.Building!)
            .FirstOrDefaultAsync(r => r.Id == request.RoomId && r.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("Phòng", request.RoomId);

        // Kiểm tra user có sở hữu building không 
        await _buildingScope.AuthorizeAsync(room.BuildingId, cancellationToken);

        // Verify tenant user exists and has Tenant role
        var tenantUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.TenantUserId && u.Role == UserRole.Tenant, cancellationToken)
            ?? throw new NotFoundException("Khách thuê", request.TenantUserId);

        // Tenant must be active and not soft-deleted
        if (tenantUser.Status != UserStatus.Active)
            throw new BadRequestException("Không thể tạo hợp đồng với khách thuê đã bị vô hiệu hóa.");

        if (tenantUser.DeletedAt != null)
            throw new BadRequestException("Không thể tạo hợp đồng với khách thuê đã bị xóa.");

        // UQ-01: Only 1 ACTIVE contract per room
        var hasActiveContract = await _db.Contracts
            .AnyAsync(c => c.RoomId == request.RoomId && c.Status == ContractStatus.Active, cancellationToken);

        if (hasActiveContract)
            throw new ConflictException("Phòng này đã có hợp đồng đang hoạt động.", "ROOM_OCCUPIED");

        // Validate room status for transition
        RoomReservation? reservation = null;
        if (request.ReservationId.HasValue)
        {
            // Contract from reservation: SM-02 (BOOKED → OCCUPIED)
            reservation = await _db.RoomReservations
                .FirstOrDefaultAsync(r => r.Id == request.ReservationId.Value, cancellationToken)
                ?? throw new NotFoundException("Đặt phòng", request.ReservationId.Value);

            if (reservation.RoomId != request.RoomId)
                throw new BadRequestException("Đặt phòng không thuộc phòng được chỉ định.");

            if (reservation.TenantUserId != request.TenantUserId)
                throw new BadRequestException("Khách thuê trong đặt phòng không khớp với khách thuê được chỉ định.");

            if (reservation.Status != ReservationStatus.Confirmed)
                throw new ConflictException("Đặt phòng phải được Xác nhận trước khi tạo hợp đồng. Hiện tại: " + reservation.Status);

            if (reservation.ExpiresAt <= DateTime.UtcNow)
                throw new ConflictException("Không thể tạo hợp đồng từ đặt phòng đã hết hạn.", "RESERVATION_EXPIRED");

            if (room.Status != RoomStatus.Reserved)
                throw new ConflictException($"Phòng phải ở trạng thái Đã đặt để tạo hợp đồng từ đặt phòng. Hiện tại: {room.Status}");

            if (request.DepositAmount < reservation.DepositAmount)
                throw new BadRequestException(
                    $"Tiền cọc hợp đồng ({request.DepositAmount:N0}đ) không được thấp hơn tiền cọc đặt phòng hiện tại ({reservation.DepositAmount:N0}đ).");
        }
        else
        {
            // Direct contract: SM-06 (AVAILABLE → OCCUPIED)
            if (room.Status != RoomStatus.Available)
                throw new ConflictException($"Phòng phải ở trạng thái Trống để tạo hợp đồng trực tiếp. Hiện tại: {room.Status}");
        }
        var creator = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);


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
            Status = ContractStatus.Active,
            Note = request.Note,
            CreatedBy = userId,
            Creator = creator,
            ContractTenants =
            [
                new ContractTenant
                {
                    TenantUserId = request.TenantUserId,
                    IsMainTenant = true,
                    MoveInDate = request.MoveInDate
                }
            ]
        };

        _db.Contracts.Add(contract);

        // Notify tenant about new contract
        _db.Notifications.Add(new Notification
        {
            UserId = request.TenantUserId,
            Title = "Hợp đồng mới",
            Message = $"Bạn đã có hợp đồng thuê phòng {room.RoomNumber} tại {room.Building!.Name}.",
            Type = Domain.Constants.NotificationTypes.ContractCreated,
            ReferenceId = contract.Id,
        });

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
                    Note = "Tiền cọc chuyển từ đặt phòng",
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
                    Note = "Bổ sung tiền cọc",
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
                Note = "Tiền cọc hợp đồng",
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
                "Phòng đã bị thay đổi bởi thao tác khác. Vui lòng thử lại.",
                "CONCURRENCY_CONFLICT");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Contracts_RoomId_Active") == true)
        {
            throw new ConflictException("Phòng này đã có hợp đồng đang hoạt động. Phát hiện tạo hợp đồng đồng thời.", "ROOM_OCCUPIED");
        }

        // Best-effort email to tenant (after successful save)
        var (subject, html) = Application.Common.Email.EmailTemplates.ContractCreated(
            tenantUser.FullName, room.RoomNumber, room.Building!.Name,
            request.StartDate, request.EndDate, request.MonthlyRent);
        await _emailService.TrySendAsync(tenantUser.Email, tenantUser.FullName, subject, html, cancellationToken);

        return new ContractDto
        {
            Id = contract.Id,
            RoomId = contract.RoomId,
            RoomNumber = room.RoomNumber,
            BuildingId = room.BuildingId,
            BuildingName = room.Building!.Name,
            TenantUserId = tenantUser.Id,
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
            CreatedBy = contract.Creator?.Id ?? userId,
            CreatedAt = contract.CreatedAt,
            UpdatedAt = contract.UpdatedAt
        };
    }
}
