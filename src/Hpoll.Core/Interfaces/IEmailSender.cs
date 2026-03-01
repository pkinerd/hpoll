namespace Hpoll.Core.Interfaces;

public interface IEmailSender
{
    Task SendEmailAsync(List<string> toAddresses, string subject, string htmlBody, CancellationToken ct = default);
    Task SendEmailAsync(List<string> toAddresses, string subject, string htmlBody, List<string>? ccAddresses, List<string>? bccAddresses, CancellationToken ct = default);
}
