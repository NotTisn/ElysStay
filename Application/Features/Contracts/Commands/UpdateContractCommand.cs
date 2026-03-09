using Application.Features.Contracts.DTOs;
using MediatR;

namespace Application.Features.Contracts.Commands;

/// <summary>
/// Updates an existing contract (CT-03).
/// Can change: endDate, monthlyRent, note.
/// Cannot change: roomId, tenantUserId.
/// </summary>
public record UpdateContractCommand : IRequest<ContractDto>
{
    public Guid Id { get; init; }
    public DateOnly? EndDate { get; init; }
    public decimal? MonthlyRent { get; init; }
    public string? Note { get; init; }
}
