namespace Application.Features.TenantProfiles.DTOs;

public record TenantProfileDto(
    Guid UserId,
    string? IdNumber,
    string? IdFrontUrl,
    string? IdBackUrl,
    DateOnly? DateOfBirth,
    string? Gender,
    string? PermanentAddress,
    DateOnly? IssuedDate,
    string? IssuedPlace,
    DateTime CreatedAt,
    DateTime UpdatedAt);
