namespace Hpoll.Email;

using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;

public class SesEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SesEmailSender> _logger;

    public SesEmailSender(IOptions<EmailSettings> settings, ILogger<SesEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default)
    {
        var regionEndpoint = RegionEndpoint.GetBySystemName(_settings.AwsRegion);
        using var client = new AmazonSimpleEmailServiceClient(regionEndpoint);

        var sendRequest = new SendEmailRequest
        {
            Source = _settings.FromAddress,
            Destination = new Destination { ToAddresses = new List<string> { toAddress } },
            Message = new Message
            {
                Subject = new Content(subject),
                Body = new Body
                {
                    Html = new Content { Charset = "UTF-8", Data = htmlBody }
                }
            }
        };

        try
        {
            var response = await client.SendEmailAsync(sendRequest, ct);
            _logger.LogInformation("Email sent to {To}, MessageId: {MessageId}", toAddress, response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", toAddress);
            throw;
        }
    }
}
