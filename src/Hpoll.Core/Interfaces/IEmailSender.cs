namespace Hpoll.Core.Interfaces;

public interface IEmailSender
{
    Task SendEmailAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default);
}
