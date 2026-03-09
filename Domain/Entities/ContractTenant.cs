namespace Domain.Entities;

public class ContractTenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContractId { get; set; }
    public Guid TenantUserId { get; set; }
    public bool IsMainTenant { get; set; }
    public DateOnly MoveInDate { get; set; }
    public DateOnly? MoveOutDate { get; set; }

    // Navigation properties
    public Contract? Contract { get; set; }
    public User? Tenant { get; set; }
}
