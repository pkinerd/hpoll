namespace Hpoll.Core.Interfaces;

/// <summary>
/// Sends HTML emails via the configured email provider (AWS SES).
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends an HTML email to the specified recipients.</summary>
    Task SendEmailAsync(List<string> toAddresses, string subject, string htmlBody, CancellationToken ct = default);

    /// <summary>Sends an HTML email to the specified recipients with optional CC and BCC addresses.</summary>
    Task SendEmailAsync(List<string> toAddresses, string subject, string htmlBody, List<string>? ccAddresses, List<string>? bccAddresses, CancellationToken ct = default);
}
