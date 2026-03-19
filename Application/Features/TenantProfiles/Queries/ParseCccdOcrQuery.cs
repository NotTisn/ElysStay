using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;

namespace Application.Features.TenantProfiles.Queries;

/// <summary>
/// POST /tenant-profiles/{userId}/ocr — Parse CCCD images via FPT.AI OCR.
/// Returns parsed data only — does NOT auto-save to TenantProfile.
/// Auth: Owner/Staff.
/// </summary>
public class ParseCccdOcrQuery : IRequest<CccdOcrResult?>
{
    public Guid UserId { get; set; }
    public required Stream FrontStream { get; init; }
    public required string FrontContentType { get; init; }
    public required long FrontSize { get; init; }
    public required Stream BackStream { get; init; }
    public required string BackContentType { get; init; }
    public required long BackSize { get; init; }
}

public class ParseCccdOcrQueryHandler : IRequestHandler<ParseCccdOcrQuery, CccdOcrResult?>
{
    private readonly IOcrService _ocrService;

    private const long MaxSize = 5 * 1024 * 1024; // 5 MB
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png" };

    public ParseCccdOcrQueryHandler(IOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    public async Task<CccdOcrResult?> Handle(ParseCccdOcrQuery request, CancellationToken ct)
    {
        ValidateFile(request.FrontSize, request.FrontContentType, "Mặt trước");
        ValidateFile(request.BackSize, request.BackContentType, "Mặt sau");

        var result = await _ocrService.ParseCccdAsync(request.FrontStream, request.BackStream, ct);

        if (result is null)
            throw new BadRequestException("Dịch vụ OCR chưa được cấu hình hoặc không khả dụng.");

        return result;
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
