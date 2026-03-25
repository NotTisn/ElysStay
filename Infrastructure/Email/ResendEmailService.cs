using Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Infrastructure.Email;

/// <summary>
/// Sends transactional emails via the Resend REST API (https://resend.com).
/// Free tier: 100 emails/day, 3 000/month — sufficient for small-scale rental management.
///
/// If Email:ApiKey is not configured, all sends are silently skipped (no-op mode).
/// This makes the service safe to inject everywhere without requiring configuration.
/// </summary>
public sealed class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly string _from;
    private readonly bool _enabled;

    public ResendEmailService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var section = configuration.GetSection("Email");
        var apiKey = section["ApiKey"];
        var fromEmail = section["FromEmail"] ?? "noreply@elysstay.com";
        var fromName = section["FromName"] ?? "ElysStay";
        _from = $"{fromName} <{fromEmail}>";

        _enabled = !string.IsNullOrWhiteSpace(apiKey);

        if (_enabled)
        {
            _httpClient.BaseAddress = new Uri("https://api.resend.com/");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        }
        else
        {
            _logger.LogWarning("Email service is DISABLED — Email:ApiKey is not configured. All transactional emails will be silently skipped. Configure Email:ApiKey to enable email delivery.");
        }
    }

    public async Task<bool> TrySendAsync(
        string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            _logger.LogDebug("Email skipped (not configured): {Subject} → {Email}", subject, toEmail);
            return false;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogDebug("Email skipped (no address): {Subject}", subject);
            return false;
        }

        try
        {
            var payload = new
            {
                from = _from,
                to = new[] { toEmail },
                subject,
                html = htmlBody
            };

            var response = await _httpClient.PostAsJsonAsync("emails", payload, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent: {Subject} → {Email}", subject, toEmail);
                return true;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Email API error ({StatusCode}): {Subject} → {Email}. Body: {Body}",
                (int)response.StatusCode, subject, toEmail, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email send failed: {Subject} → {Email}", subject, toEmail);
            return false;
        }
    }
}
