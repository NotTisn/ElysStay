namespace Domain.Entities;

public class ContractTenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContractId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }

    // Navigation properties
    public Contract? Contract { get; set; }
    public User? Tenant { get; set; }
}
