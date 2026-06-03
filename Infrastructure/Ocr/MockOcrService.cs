using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ocr;

public sealed class MockOcrService : IOcrService
{
    private readonly ILogger<MockOcrService> _logger;

    public MockOcrService(ILogger<MockOcrService> logger)
    {
        _logger = logger;
    }

    public Task<CccdOcrResult?> ParseCccdAsync(Stream frontImage, Stream backImage, CancellationToken ct = default)
    {
        _logger.LogInformation("MockOcrService: Simulating OCR processing...");
        
        // Mock data to simulate OCR success.
        var result = new CccdOcrResult
        {
            IdNumber = "079099123456",
            FullName = "NGUYEN VAN MOCK",
            DateOfBirth = new DateOnly(1999, 1, 1),
            Gender = "Nam",
            Nationality = "Việt Nam",
            PlaceOfOrigin = "Hồ Chí Minh",
            PlaceOfResidence = "Quận 1, TP. Hồ Chí Minh",
            ExpiryDate = new DateOnly(2039, 1, 1),
            IssuedDate = new DateOnly(2021, 1, 1),
            IssuedPlace = "Cục trưởng Cục Cảnh sát quản lý hành chính về trật tự xã hội"
        };
        
        return Task.FromResult<CccdOcrResult?>(result);
    }
}
