namespace Application.Common.Interfaces;

/// <summary>
/// Sends transactional emails. Implementations must be fault-tolerant:
/// TrySendAsync catches all exceptions and never throws.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Attempts to send an email. Returns true on success, false on failure.
    /// Never throws — all errors are logged internally.
    /// </summary>
    Task<bool> TrySendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default);
}
