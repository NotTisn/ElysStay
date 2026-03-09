using Application.Features.TenantProfiles.Commands;
using Application.Features.TenantProfiles.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Tenant CCCD profile management.
/// </summary>
[Authorize]
[Route("api/v1/tenant-profiles")]
public class TenantProfilesController : BaseApiController
{
    private readonly IMediator _mediator;

    public TenantProfilesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// View a tenant's CCCD profile.
    /// </summary>
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetProfile(Guid userId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantProfileQuery(userId), ct);
        return OkResponse(result);
    }

    /// <summary>
    /// Update a tenant's CCCD text data (full replacement).
    /// </summary>
    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> UpdateProfile(Guid userId, [FromBody] UpdateTenantProfileRequest request, CancellationToken ct)
    {
        var command = new UpdateTenantProfileCommand
        {
            UserId = userId,
            IdNumber = request.IdNumber,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            PermanentAddress = request.PermanentAddress,
            IssuedDate = request.IssuedDate,
            IssuedPlace = request.IssuedPlace
        };

        var result = await _mediator.Send(command, ct);
        return OkResponse(result, "Tenant profile updated successfully");
    }

    // Note: OCR endpoint (POST /{userId}/ocr) and image upload (POST /{userId}/upload-id-images)
    // require Cloudinary/file storage integration — deferred until infra is wired.
}

// --- Request records ---

public record UpdateTenantProfileRequest(
    string? IdNumber,
    DateOnly? DateOfBirth,
    string? Gender,
    string? PermanentAddress,
    DateOnly? IssuedDate,
    string? IssuedPlace);
