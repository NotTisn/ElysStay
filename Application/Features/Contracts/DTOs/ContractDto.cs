namespace Application.Features.Contracts.DTOs;

/// <summary>
/// Contract summary DTO for list responses.
/// </summary>
public record ContractDto
{
    public required Guid Id { get; init; }
    public required Guid RoomId { get; init; }
    public required string RoomNumber { get; init; }
    public required Guid BuildingId { get; init; }
    public required string BuildingName { get; init; }
    public required Guid TenantUserId { get; init; }
    public required string TenantName { get; init; }
    public Guid? ReservationId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required DateOnly MoveInDate { get; init; }
    public required decimal MonthlyRent { get; init; }
    public required decimal DepositAmount { get; init; }
    public required string DepositStatus { get; init; }
    public required string Status { get; init; }
    public DateOnly? TerminationDate { get; init; }
    public string? TerminationNote { get; init; }
    public decimal? RefundAmount { get; init; }
    public string? Note { get; init; }
    public required Guid CreatedBy { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Contract detail DTO with roommates.
/// </summary>
public record ContractDetailDto : ContractDto
{
    public required IReadOnlyList<ContractTenantDto> Tenants { get; init; }
}

/// <summary>
/// Contract tenant (roommate) DTO.
/// </summary>
public record ContractTenantDto
{
    public required Guid Id { get; init; }
    public required Guid TenantUserId { get; init; }
    public required string TenantName { get; init; }
    public string? TenantEmail { get; init; }
    public string? TenantPhone { get; init; }
    public required bool IsMainTenant { get; init; }
    public required DateOnly MoveInDate { get; init; }
    public DateOnly? MoveOutDate { get; init; }
}
