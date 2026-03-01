namespace Domain.Entities;

public class ContractTenant
{
    public Guid ContractId { get; set; }
    public Guid TenantId { get; set; }

    public Contract? Contract { get; set; }
    public User? Tenant { get; set; }
}
