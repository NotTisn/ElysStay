using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;

namespace Application.Features.MaintenanceIssues.Commands;

/// <summary>
/// POST /issues/{id}/images — Upload up to 3 images for a maintenance issue.
/// Auth: ALL authenticated users.
/// Max 3 MB per image, JPEG/PNG only.
/// </summary>
public class UploadIssueImagesCommand : IRequest<IReadOnlyList<string>>
{
    public Guid IssueId { get; init; }
    public required IReadOnlyList<FileUploadItem> Files { get; init; }
}

public class FileUploadItem
{
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSize { get; init; }
}

public class UploadIssueImagesCommandHandler : IRequestHandler<UploadIssueImagesCommand, IReadOnlyList<string>>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileUploadService _fileUpload;

    private const long MaxSizePerFile = 3 * 1024 * 1024; // 3 MB
    private const int MaxFiles = 3;
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png" };

    public UploadIssueImagesCommandHandler(IApplicationDbContext db, IFileUploadService fileUpload)
    {
        _db = db;
        _fileUpload = fileUpload;
    }

    public async Task<IReadOnlyList<string>> Handle(UploadIssueImagesCommand request, CancellationToken ct)
    {
        if (request.Files is null || request.Files.Count == 0)
            throw new BadRequestException("Vui lòng chọn ít nhất 1 ảnh.");

        if (request.Files.Count > MaxFiles)
            throw new BadRequestException($"Tối đa {MaxFiles} ảnh cho mỗi sự cố.");

        foreach (var file in request.Files)
        {
            if (file.FileSize > MaxSizePerFile)
                throw new BadRequestException($"Ảnh \"{file.FileName}\" vượt quá 3 MB.");

            if (!AllowedTypes.Contains(file.ContentType))
                throw new BadRequestException($"File \"{file.FileName}\" không phải JPEG hoặc PNG.");
        }

        var issue = await _db.MaintenanceIssues.FindAsync([request.IssueId], ct)
            ?? throw new NotFoundException("Sự cố", request.IssueId);

        // Parse existing URLs (if any) so we don't exceed total limit
        var existingUrls = string.IsNullOrWhiteSpace(issue.ImageUrls)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(issue.ImageUrls) ?? new List<string>();

        if (existingUrls.Count + request.Files.Count > MaxFiles)
            throw new BadRequestException(
                $"Sự cố đã có {existingUrls.Count} ảnh. Tối đa {MaxFiles} ảnh.");

        var uploadedUrls = new List<string>();

        foreach (var file in request.Files)
        {
            var url = await _fileUpload.UploadAsync(file.FileStream, file.FileName, "issues", ct);

            if (!string.IsNullOrEmpty(url))
                uploadedUrls.Add(url);
        }

        if (uploadedUrls.Count == 0)
            throw new BadRequestException("Tải ảnh lên thất bại. Vui lòng thử lại.");

        existingUrls.AddRange(uploadedUrls);
        issue.ImageUrls = JsonSerializer.Serialize(existingUrls);
        issue.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return uploadedUrls;
    }
}
