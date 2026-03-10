namespace Application.Features.Users.DTOs;

/// <summary>
/// User summary DTO for list responses.
/// </summary>
public record UserDto
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public string? Phone { get; init; }
    public string? AvatarUrl { get; init; }
    public required string Role { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Extended user profile for GET /users/me.
/// Includes UpdatedAt for cache-busting and profile staleness detection.
/// </summary>
public record UserProfileDto : UserDto
{
    public required DateTime UpdatedAt { get; init; }
}
