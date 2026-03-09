using Application.Features.Contracts.DTOs;
using MediatR;

namespace Application.Features.Contracts.Commands;

/// <summary>
/// Renews a contract (CT-01).
/// Old contract → TERMINATED (administrative, no deposit refund).
/// New contract created with same room/tenant, deposit carries over.
/// Room stays OCCUPIED — no status change.
/// ContractTenants copied to new contract.
/// </summary>
public record RenewContractCommand : IRequest<ContractDto>
{
    public Guid Id { get; init; }
    public required DateOnly NewEndDate { get; init; }
    public decimal? NewMonthlyRent { get; init; }
}
