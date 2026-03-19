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

    /// <summary>
    /// POST /tenant-profiles/{userId}/upload-id-images — Upload front and back CCCD images (max 5 MB each, JPEG/PNG).
    /// </summary>
    [HttpPost("{userId:guid}/upload-id-images")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadIdImages(Guid userId, IFormFile frontImage, IFormFile backImage, CancellationToken ct)
    {
        var result = await _mediator.Send(new UploadCccdImagesCommand
        {
            UserId = userId,
            FrontStream = frontImage.OpenReadStream(),
            FrontFileName = frontImage.FileName,
            FrontContentType = frontImage.ContentType,
            FrontSize = frontImage.Length,
            BackStream = backImage.OpenReadStream(),
            BackFileName = backImage.FileName,
            BackContentType = backImage.ContentType,
            BackSize = backImage.Length
        }, ct);
        return OkResponse(result, "Tải ảnh CCCD lên thành công");
    }

    /// <summary>
    /// POST /tenant-profiles/{userId}/ocr — Parse CCCD images via FPT.AI OCR. Returns data only (does not save).
    /// </summary>
    [HttpPost("{userId:guid}/ocr")]
    [Authorize(Roles = "Owner,Staff")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ParseCccdOcr(Guid userId, IFormFile frontImage, IFormFile backImage, CancellationToken ct)
    {
        var result = await _mediator.Send(new ParseCccdOcrQuery
        {
            UserId = userId,
            FrontStream = frontImage.OpenReadStream(),
            FrontContentType = frontImage.ContentType,
            FrontSize = frontImage.Length,
            BackStream = backImage.OpenReadStream(),
            BackContentType = backImage.ContentType,
            BackSize = backImage.Length
        }, ct);
        return OkResponse(result, "Phân tích CCCD thành công");
    }
}

// --- Request records ---

public record UpdateTenantProfileRequest(
    string? IdNumber,
    DateOnly? DateOfBirth,
    string? Gender,
    string? PermanentAddress,
    DateOnly? IssuedDate,
    string? IssuedPlace);
