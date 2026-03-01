namespace Domain.Entities;

public class MeterReading
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContractId { get; set; }
    public Guid RoomId { get; set; }
    public int BillingMonth { get; set; }
    public int BillingYear { get; set; }
    public int OldElectricityIndex { get; set; }
    public int NewElectricityIndex { get; set; }
    public int OldWaterIndex { get; set; }
    public int NewWaterIndex { get; set; }
    public Guid? RecordedById { get; set; }
    public DateTime ReadingDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Contract? Contract { get; set; }
    public Room? Room { get; set; }
    public User? RecordedBy { get; set; }
}
