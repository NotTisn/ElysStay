using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Reservations.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reservations.Commands;

/// <summary>
/// PATCH /reservations/{id}/status — Confirm or Cancel a reservation.
/// Auth: Owner/Staff (building-scoped).
/// SM-07: PENDING → CONFIRMED.
/// SM-08: PENDING/CONFIRMED → CANCELLED (with deposit handling).
/// DEP-05: Cancel: RefundAmount can be 0 (forfeit), partial, or full.
/// </summary>
public class ChangeReservationStatusCommand : IRequest<ReservationDto>
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty; // CONFIRM or CANCEL
    public decimal? RefundAmount { get; set; }
    public string? RefundNote { get; set; }
}

public class ChangeReservationStatusCommandHandler : IRequestHandler<ChangeReservationStatusCommand, ReservationDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public ChangeReservationStatusCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<ReservationDto> Handle(ChangeReservationStatusCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var reservation = await _db.RoomReservations
            .Include(r => r.Room).ThenInclude(r => r!.Building)
            .Include(r => r.TenantUser)
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy đặt phòng {request.Id}.");

        await _buildingScope.AuthorizeAsync(reservation.Room!.BuildingId, ct);

        switch (request.Action.ToUpperInvariant())
        {
            case "CONFIRM":
                HandleConfirm(reservation);
                break;

            case "CANCEL":
                HandleCancel(reservation, request, userId);
                break;

            default:
                throw new BadRequestException($"Hành động không xác định: {request.Action}. Sử dụng CONFIRM hoặc CANCEL.");
        }

        reservation.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "Phòng đã bị thay đổi bởi người dùng khác. Vui lòng thử lại.",
                "CONCURRENCY_CONFLICT");
        }

        return new ReservationDto(
            reservation.Id,
            reservation.RoomId,
            reservation.Room!.RoomNumber,
            reservation.Room.BuildingId,
            reservation.Room.Building!.Name,
            reservation.TenantUserId,
            reservation.TenantUser?.FullName,
            reservation.DepositAmount,
            reservation.Status.ToString(),
            reservation.ExpiresAt,
            reservation.Note,
            reservation.RefundAmount,
            reservation.RefundedAt,
            reservation.RefundNote,
            reservation.CreatedAt,
            reservation.UpdatedAt);
    }

    /// <summary>SM-07: PENDING → CONFIRMED.</summary>
    private static void HandleConfirm(Domain.Entities.RoomReservation reservation)
    {
        if (reservation.Status != ReservationStatus.Pending)
            throw new ConflictException(
                $"Không thể xác nhận đặt phòng ở trạng thái {reservation.Status}. Chỉ trạng thái Pending mới có thể xác nhận.",
                "INVALID_STATUS_TRANSITION");

        if (reservation.ExpiresAt <= DateTime.UtcNow)
            throw new ConflictException(
                "Không thể xác nhận đặt phòng đã hết hạn.",
                "RESERVATION_EXPIRED");

        reservation.Status = ReservationStatus.Confirmed;
    }

    /// <summary>
    /// SM-08: PENDING/CONFIRMED → CANCELLED.
    /// SM-03: Room BOOKED → AVAILABLE.
    /// Deposit handling: record DEPOSIT_IN (money was received), then DEPOSIT_REFUND if refund > 0.
    /// </summary>
    private void HandleCancel(
        Domain.Entities.RoomReservation reservation,
        ChangeReservationStatusCommand request,
        Guid userId)
    {
        if (reservation.Status != ReservationStatus.Pending && reservation.Status != ReservationStatus.Confirmed)
            throw new ConflictException(
                $"Không thể hủy đặt phòng ở trạng thái {reservation.Status}.",
                "INVALID_STATUS_TRANSITION");

        // Capture previous status before mutation — needed for deposit logic
        var wasConfirmed = reservation.Status == ReservationStatus.Confirmed;

        // Validate refund amount
        var refundAmount = request.RefundAmount ?? 0m;
        if (refundAmount < 0 || refundAmount > reservation.DepositAmount)
            throw new BadRequestException(
                $"Số tiền hoàn phải từ 0 đến {reservation.DepositAmount}.");

        reservation.Status = ReservationStatus.Cancelled;
        reservation.RefundAmount = refundAmount;
        reservation.RefundNote = request.RefundNote;
        reservation.RefundedAt = refundAmount > 0 ? DateTime.UtcNow : null;

        // Only record deposit payments when reservation was Confirmed (deposit actually received).
        // Pending reservations may not have received the deposit yet.
        if (wasConfirmed && reservation.DepositAmount > 0)
        {
            // Record deposit-in (money was received at confirmation time)
            _db.Payments.Add(new Domain.Entities.Payment
            {
                ContractId = null,
                InvoiceId = null,
                ReservationId = reservation.Id,
                Type = PaymentType.DepositIn,
                Amount = reservation.DepositAmount,
                Note = $"Tiền cọc nhận từ đặt phòng (đã hủy)",
                RecordedBy = userId,
                PaidAt = reservation.CreatedAt // Was originally received at reservation creation
            });

            // Record refund if applicable (DEP-05)
            if (refundAmount > 0)
            {
                _db.Payments.Add(new Domain.Entities.Payment
                {
                    ContractId = null,
                    InvoiceId = null,
                    ReservationId = reservation.Id,
                    Type = PaymentType.DepositRefund,
                    Amount = refundAmount,
                    Note = request.RefundNote ?? "Hoàn trả tiền cọc đặt phòng đã hủy",
                    RecordedBy = userId,
                    PaidAt = DateTime.UtcNow
                });
            }
        }

        // SM-03: Room BOOKED → AVAILABLE (only if currently Booked)
        if (reservation.Room!.Status == RoomStatus.Booked)
        {
            reservation.Room.Status = RoomStatus.Available;
            reservation.Room.UpdatedAt = DateTime.UtcNow;
        }
    }
}
