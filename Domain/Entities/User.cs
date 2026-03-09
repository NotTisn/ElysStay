using Domain.Enums;

namespace Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? KeycloakId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.Tenant;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public TenantProfile? TenantProfile { get; set; }
    public ICollection<StaffAssignment> BuildingStaffAssignments { get; set; } = new List<StaffAssignment>();
    public ICollection<Building> OwnedBuildings { get; set; } = new List<Building>();
}
