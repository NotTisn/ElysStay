using Application.Common.Models;
using Application.Features.Contracts.DTOs;
using MediatR;

namespace Application.Features.Contracts.Queries;

/// <summary>
/// Lists contracts with pagination and filters.
/// All roles can access; TENANT auto-filtered to own contracts.
/// </summary>
public class GetContractsQuery : PagedQuery, IRequest<PagedResult<ContractDto>>
{
    public Guid? BuildingId { get; init; }
    public Guid? RoomId { get; init; }
    public string? Status { get; init; }
}
