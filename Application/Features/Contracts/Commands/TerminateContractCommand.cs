using Application.Features.Contracts.DTOs;
using MediatR;

namespace Application.Features.Contracts.Commands;

/// <summary>
/// Terminates a contract (CT-02, SM-04, DEP-04).
/// Sets TerminationDate, creates DEPOSIT_REFUND payment.
/// Contract.Status → TERMINATED, Room → AVAILABLE.
/// DepositStatus updated based on refund amount.
/// SM-10: Only manual termination — no auto-expiry.
/// </summary>
public record TerminateContractCommand : IRequest<ContractDto>
{
    public Guid Id { get; init; }
    public required DateOnly TerminationDate { get; init; }
    public string? Note { get; init; }
    public decimal Deductions { get; init; } = 0;
}
