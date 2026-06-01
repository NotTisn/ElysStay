using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TenantProfiles.Commands;

/// <summary>
/// POST /tenant-profiles/{userId}/upload-id-images — Upload front and back CCCD images.
/// Auth: ALL authenticated users.
/// Max 5 MB per image, JPEG/PNG only.
/// </summary>
public class UploadCccdImagesCommand : IRequest<UploadCccdImagesResult>
{
    public Guid UserId { get; set; }
    public required Stream FrontStream { get; init; }
    public required string FrontFileName { get; init; }
    public required string FrontContentType { get; init; }
    public required long FrontSize { get; init; }
    public required Stream BackStream { get; init; }
    public required string BackFileName { get; init; }
    public required string BackContentType { get; init; }
    public required long BackSize { get; init; }
}

public record UploadCccdImagesResult(string IdFrontUrl, string IdBackUrl);

public class UploadCccdImagesCommandHandler : IRequestHandler<UploadCccdImagesCommand, UploadCccdImagesResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileUploadService _fileUpload;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    private const long MaxSize = 5 * 1024 * 1024; // 5 MB
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png" };

    public UploadCccdImagesCommandHandler(
        IApplicationDbContext db,
        IFileUploadService fileUpload,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _fileUpload = fileUpload;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<UploadCccdImagesResult> Handle(UploadCccdImagesCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        // Tenants can only upload their own CCCD
        if (_currentUser.IsTenant && request.UserId != userId)
            throw new ForbiddenException("Bạn chỉ có thể tải ảnh CCCD của mình.");

        // Owner/Staff must have building-scope access to the target user's building
        if (!_currentUser.IsTenant)
        {
            var tenantBuildingId = await _db.Contracts
                .Where(c => c.ContractTenants.Any(ct2 => ct2.TenantUserId == request.UserId) && c.Status == Domain.Enums.ContractStatus.Active)
                .Select(c => c.Room!.BuildingId)
                .FirstOrDefaultAsync(ct);
            if (tenantBuildingId != Guid.Empty)
                await _buildingScope.AuthorizeAsync(tenantBuildingId, ct);
        }

        ValidateFile(request.FrontSize, request.FrontContentType, "Mặt trước");
        ValidateFile(request.BackSize, request.BackContentType, "Mặt sau");

        var profile = await _db.TenantProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == request.UserId, ct);

        // TenantProfile might not exist yet — check User exists
        if (profile is null)
        {
            var user = await _db.Users.FindAsync([request.UserId], ct)
                ?? throw new NotFoundException("Người dùng", request.UserId);

            profile = new Domain.Entities.TenantProfile
            {
                UserId = request.UserId
            };
            _db.TenantProfiles.Add(profile);
        }

        // Upload front
        var frontUrl = await _fileUpload.UploadAsync(request.FrontStream, request.FrontFileName, "cccd", ct);

        // Upload back
        var backUrl = await _fileUpload.UploadAsync(request.BackStream, request.BackFileName, "cccd", ct);

        if (string.IsNullOrEmpty(frontUrl) || string.IsNullOrEmpty(backUrl))
            throw new BadRequestException("Tải ảnh CCCD lên thất bại. Vui lòng thử lại.");

        profile.IdFrontUrl = frontUrl;
        profile.IdBackUrl = backUrl;
        profile.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new UploadCccdImagesResult(frontUrl, backUrl);
    }

    private static void ValidateFile(long fileSize, string contentType, string label)
    {
        if (fileSize == 0)
            throw new BadRequestException($"Vui lòng chọn ảnh {label} CCCD.");

        if (fileSize > MaxSize)
            throw new BadRequestException($"Ảnh {label} không được vượt quá 5 MB.");

        if (!AllowedTypes.Contains(contentType))
            throw new BadRequestException($"Ảnh {label} chỉ chấp nhận JPEG hoặc PNG.");
    }
}
