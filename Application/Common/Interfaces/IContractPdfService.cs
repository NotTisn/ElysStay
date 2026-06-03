namespace Application.Common.Interfaces;

public interface IContractPdfService
{
    byte[] Generate(ContractPdfData data);
}

public class ContractPdfData
{
    public required string BuildingName { get; init; }
    public required string RoomNumber { get; init; }
    public required string OwnerName { get; init; }
    public required string TenantName { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required decimal MonthlyRent { get; init; }
    public required decimal DepositAmount { get; init; }
    public string? Note { get; init; }
}
