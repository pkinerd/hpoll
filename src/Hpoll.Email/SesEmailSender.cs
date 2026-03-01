namespace Hpoll.Email;

using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;

public class SesEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly IAmazonSimpleEmailService _sesClient;
    private readonly ILogger<SesEmailSender> _logger;

    public SesEmailSender(
        IAmazonSimpleEmailService sesClient,
        IOptions<EmailSettings> settings,
        ILogger<SesEmailSender> logger)
    {
        _sesClient = sesClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public Task SendEmailAsync(List<string> toAddresses, string subject, string htmlBody, CancellationToken ct = default)
    {
        return SendEmailAsync(toAddresses, subject, htmlBody, null, null, ct);
    }

    public async Task SendEmailAsync(List<string> toAddresses, string subject, string htmlBody, List<string>? ccAddresses, List<string>? bccAddresses, CancellationToken ct = default)
    {
        var destination = new Destination { ToAddresses = toAddresses };

        if (ccAddresses?.Count > 0)
            destination.CcAddresses = ccAddresses;

        if (bccAddresses?.Count > 0)
            destination.BccAddresses = bccAddresses;

        var sendRequest = new SendEmailRequest
        {
            Source = _settings.FromAddress,
            Destination = destination,
            Message = new Message
            {
                Subject = new Content(subject),
                Body = new Body
                {
                    Html = new Content { Charset = "UTF-8", Data = htmlBody }
                }
            }
        };

        var toDisplay = string.Join(", ", toAddresses);
        try
        {
            var response = await _sesClient.SendEmailAsync(sendRequest, ct);
            _logger.LogInformation("Email sent to {To}, MessageId: {MessageId}", toDisplay, response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", toDisplay);
            throw;
        }
    }
}
