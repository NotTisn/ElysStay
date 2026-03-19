using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;

namespace Application.Features.Users.Commands;

/// <summary>
/// POST /users/me/avatar — Upload avatar image for the current user.
/// Auth: ALL authenticated users.
/// Max 2 MB, JPEG/PNG only.
/// </summary>
public class UploadAvatarCommand : IRequest<string>
{
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSize { get; init; }
}

public class UploadAvatarCommandHandler : IRequestHandler<UploadAvatarCommand, string>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService _fileUpload;

    private const long MaxSize = 2 * 1024 * 1024; // 2 MB
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png" };

    public UploadAvatarCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileUploadService fileUpload)
    {
        _db = db;
        _currentUser = currentUser;
        _fileUpload = fileUpload;
    }

    public async Task<string> Handle(UploadAvatarCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        if (request.FileSize == 0)
            throw new BadRequestException("Vui lòng chọn ảnh đại diện.");

        if (request.FileSize > MaxSize)
            throw new BadRequestException("Ảnh đại diện không được vượt quá 2 MB.");

        if (!AllowedTypes.Contains(request.ContentType))
            throw new BadRequestException("Chỉ chấp nhận file JPEG hoặc PNG.");

        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException("Người dùng", userId);

        var url = await _fileUpload.UploadAsync(request.FileStream, request.FileName, "avatars", ct);

        if (string.IsNullOrEmpty(url))
            throw new BadRequestException("Tải ảnh lên thất bại. Vui lòng thử lại.");

        user.AvatarUrl = url;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return url;
    }
}
