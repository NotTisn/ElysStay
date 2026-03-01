using Domain.Enums;

namespace Domain.Entities;

public class Contract
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ContractCode { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public Guid RepresentativeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal RentAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public DepositStatus DepositStatus { get; set; } = DepositStatus.Held;
    public decimal RefundedDepositAmount { get; set; } = 0;
    public DateTime? DepositRefundDate { get; set; }
    public string PaymentCycle { get; set; } = "MONTHLY";
    public int PaymentDate { get; set; }
    public decimal ElectricityRate { get; set; }
    public decimal WaterRate { get; set; }
    public int InitialElectricityIndex { get; set; }
    public int InitialWaterIndex { get; set; }
    public string ServiceFees { get; set; } = "[]"; 
    public ContractStatus Status { get; set; } = ContractStatus.Active;
    public string? ContractFileUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public Room? Room { get; set; }
    public User? Representative { get; set; }
    public ICollection<ContractTenant> ContractTenants { get; set; } = new List<ContractTenant>();
    public ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();
}
