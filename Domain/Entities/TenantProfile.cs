namespace Domain.Entities;

public class TenantProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string? IdNumber { get; set; }
    public string? IdFrontUrl { get; set; }
    public string? IdBackUrl { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? PermanentAddress { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public string? IssuedPlace { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? User { get; set; }
}
