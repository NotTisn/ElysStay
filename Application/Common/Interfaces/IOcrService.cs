namespace Application.Common.Interfaces;

/// <summary>
/// Parses Vietnamese Citizen Identity Card (CCCD) images via OCR.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Sends front and back CCCD images to OCR provider and returns parsed data.
    /// Returns null if the service is not configured.
    /// </summary>
    Task<CccdOcrResult?> ParseCccdAsync(Stream frontImage, Stream backImage, CancellationToken ct = default);
}

public class CccdOcrResult
{
    public string? IdNumber { get; init; }
    public string? FullName { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? Nationality { get; init; }
    public string? PlaceOfOrigin { get; init; }
    public string? PlaceOfResidence { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public DateOnly? IssuedDate { get; init; }
    public string? IssuedPlace { get; init; }
}
