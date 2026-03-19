using System.Net.Http.Headers;
using System.Text.Json;
using Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ocr;

/// <summary>
/// Integrates with FPT.AI eKYC API for Vietnamese CCCD parsing.
/// Free tier: 50 requests/month.
/// If not configured, ParseCccdAsync returns null.
/// </summary>
public sealed class FptAiOcrService : IOcrService
{
    private readonly HttpClient _http;
    private readonly ILogger<FptAiOcrService> _logger;
    private readonly string _apiKey;
    private readonly bool _enabled;

    private const string FrontUrl = "https://api.fpt.ai/vision/idr/vnm";
    private const string BackUrl = "https://api.fpt.ai/vision/idr/vnm";

    public FptAiOcrService(HttpClient http, IConfiguration configuration, ILogger<FptAiOcrService> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = configuration["FptAi:ApiKey"] ?? string.Empty;
        _enabled = !string.IsNullOrWhiteSpace(_apiKey);

        if (!_enabled)
            _logger.LogInformation("FPT.AI OCR not configured — CCCD parsing will be disabled.");
    }

    public async Task<CccdOcrResult?> ParseCccdAsync(Stream frontImage, Stream backImage, CancellationToken ct = default)
    {
        if (!_enabled)
            return null;

        try
        {
            var frontData = await CallOcrAsync(frontImage, "front", ct);
            var backData = await CallOcrAsync(backImage, "back", ct);

            return MergeResults(frontData, backData);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FPT.AI OCR failed");
            return null;
        }
    }

    private async Task<JsonElement?> CallOcrAsync(Stream image, string side, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(image);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(streamContent, "image", $"{side}.jpg");

        using var request = new HttpRequestMessage(HttpMethod.Post, side == "front" ? FrontUrl : BackUrl);
        request.Headers.Add("api-key", _apiKey);
        request.Content = content;

        var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("FPT.AI OCR {Side} returned {StatusCode}", side, response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("data", out var dataArray) && dataArray.GetArrayLength() > 0)
            return dataArray[0];

        return null;
    }

    private static CccdOcrResult MergeResults(JsonElement? front, JsonElement? back)
    {
        return new CccdOcrResult
        {
            IdNumber = GetString(front, "id"),
            FullName = GetString(front, "name"),
            DateOfBirth = ParseDate(GetString(front, "dob")),
            Gender = GetString(front, "sex"),
            Nationality = GetString(front, "nationality"),
            PlaceOfOrigin = GetString(front, "home"),
            PlaceOfResidence = GetString(front, "address"),
            ExpiryDate = ParseDate(GetString(front, "doe")),
            IssuedDate = ParseDate(GetString(back, "issue_date")),
            IssuedPlace = GetString(back, "issue_loc")
        };
    }

    private static string? GetString(JsonElement? element, string propertyName)
    {
        if (element is null) return null;
        return element.Value.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // FPT.AI returns dates as dd/MM/yyyy
        return DateOnly.TryParseExact(value, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var date)
            ? date : null;
    }
}
