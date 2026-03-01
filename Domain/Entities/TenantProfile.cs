namespace Domain.Entities;

public class TenantProfile
{
    public Guid UserId { get; set; }
    public string IdentityCard { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string Hometown { get; set; } = string.Empty;
    public string? IdCardFrontUrl { get; set; }
    public string? IdCardBackUrl { get; set; }
    public string? Occupation { get; set; }

    public User? User { get; set; }
}
