using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;

namespace Application.Features.Expenses.Commands;

/// <summary>
/// POST /expenses/{id}/receipt — Upload receipt image for an expense.
/// Auth: Owner/Staff (building-scoped).
/// Max 5 MB, JPEG/PNG/PDF.
/// </summary>
public class UploadExpenseReceiptCommand : IRequest<string>
{
    public Guid ExpenseId { get; init; }
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSize { get; init; }
}

public class UploadExpenseReceiptCommandHandler : IRequestHandler<UploadExpenseReceiptCommand, string>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;
    private readonly IFileUploadService _fileUpload;

    private const long MaxSize = 5 * 1024 * 1024; // 5 MB
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "application/pdf" };

    public UploadExpenseReceiptCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope,
        IFileUploadService fileUpload)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
        _fileUpload = fileUpload;
    }

    public async Task<string> Handle(UploadExpenseReceiptCommand request, CancellationToken ct)
    {
        if (request.FileSize == 0)
            throw new BadRequestException("Vui lòng chọn ảnh biên lai.");

        if (request.FileSize > MaxSize)
            throw new BadRequestException("Ảnh biên lai không được vượt quá 5 MB.");

        if (!AllowedTypes.Contains(request.ContentType))
            throw new BadRequestException("Chỉ chấp nhận file JPEG, PNG hoặc PDF.");

        var expense = await _db.Expenses.FindAsync([request.ExpenseId], ct)
            ?? throw new NotFoundException("Chi phí", request.ExpenseId);

        await _buildingScope.AuthorizeAsync(expense.BuildingId, ct);

        var url = await _fileUpload.UploadAsync(request.FileStream, request.FileName, "receipts", ct);

        if (string.IsNullOrEmpty(url))
            throw new BadRequestException("Tải ảnh lên thất bại. Vui lòng thử lại.");

        expense.ReceiptUrl = url;
        expense.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return url;
    }
}
