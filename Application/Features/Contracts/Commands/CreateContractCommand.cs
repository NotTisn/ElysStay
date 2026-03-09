using Application.Features.Contracts.DTOs;
using MediatR;

namespace Application.Features.Contracts.Commands;

/// <summary>
/// Creates a new contract.
/// UQ-01: Only 1 ACTIVE contract per room (409 ROOM_OCCUPIED).
/// SM-02: BOOKED → OCCUPIED if from reservation.
/// SM-06: AVAILABLE → OCCUPIED if direct.
/// CT-04: CreatedBy = current user.
/// Auto-creates ContractTenant(IsMainTenant=true).
/// Deposit → Payment(DEPOSIT_IN).
/// DEP-02/DEP-03: Reservation deposit handling if reservationId provided.
/// </summary>
public record CreateContractCommand : IRequest<ContractDto>
{
    public required Guid RoomId { get; init; }
    public required Guid TenantUserId { get; init; }
    public Guid? ReservationId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required DateOnly MoveInDate { get; init; }
    public required decimal MonthlyRent { get; init; }
    public required decimal DepositAmount { get; init; }
    public string? Note { get; init; }
}
