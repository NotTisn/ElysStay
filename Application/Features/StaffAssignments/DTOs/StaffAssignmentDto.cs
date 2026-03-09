namespace Application.Features.StaffAssignments.DTOs;

public record StaffAssignmentDto
{
    public required Guid StaffId { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public string? Phone { get; init; }
    public required DateTime AssignedAt { get; init; }
}
