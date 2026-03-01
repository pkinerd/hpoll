using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Hpoll.Core.Configuration;
using Hpoll.Email;

namespace Hpoll.Core.Tests;

public class SesEmailSenderTests
{
    private readonly Mock<IAmazonSimpleEmailService> _mockSes;
    private readonly SesEmailSender _sender;

    public SesEmailSenderTests()
    {
        _mockSes = new Mock<IAmazonSimpleEmailService>();
        var settings = Options.Create(new EmailSettings
        {
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });
        _sender = new SesEmailSender(_mockSes.Object, settings, NullLogger<SesEmailSender>.Instance);
    }

    [Fact]
    public async Task SendEmailAsync_CallsSesWithCorrectParameters()
    {
        _mockSes.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendEmailResponse { MessageId = "msg-001" });

        await _sender.SendEmailAsync(new List<string> { "user@example.com" }, "Test Subject", "<html>Body</html>");

        _mockSes.Verify(s => s.SendEmailAsync(
            It.Is<SendEmailRequest>(r =>
                r.Source == "noreply@hpoll.com" &&
                r.Destination.ToAddresses.Contains("user@example.com") &&
                r.Message.Subject.Data == "Test Subject" &&
                r.Message.Body.Html.Data == "<html>Body</html>" &&
                r.Message.Body.Html.Charset == "UTF-8"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendEmailAsync_OnSesFailure_Throws()
    {
        _mockSes.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MessageRejectedException("Bad content"));

        await Assert.ThrowsAsync<MessageRejectedException>(
            () => _sender.SendEmailAsync(new List<string> { "user@example.com" }, "Test", "<html>Bad</html>"));
    }

    [Fact]
    public async Task SendEmailAsync_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        _mockSes.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), cts.Token))
            .ReturnsAsync(new SendEmailResponse { MessageId = "msg-002" });

        await _sender.SendEmailAsync(new List<string> { "user@example.com" }, "Test", "<html>Body</html>", cts.Token);

        _mockSes.Verify(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), cts.Token), Times.Once);
    }

    [Fact]
    public async Task SendEmailAsync_WithMultipleToAddresses_SetsAllRecipients()
    {
        _mockSes.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendEmailResponse { MessageId = "msg-006" });

        var to = new List<string> { "user1@example.com", "user2@example.com" };

        await _sender.SendEmailAsync(to, "Test", "<html>Body</html>");

        _mockSes.Verify(s => s.SendEmailAsync(
            It.Is<SendEmailRequest>(r =>
                r.Destination.ToAddresses.Count == 2 &&
                r.Destination.ToAddresses.Contains("user1@example.com") &&
                r.Destination.ToAddresses.Contains("user2@example.com")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendEmailAsync_WithCcAndBcc_SetsDestinationCorrectly()
    {
        _mockSes.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendEmailResponse { MessageId = "msg-003" });

        var to = new List<string> { "user@example.com" };
        var cc = new List<string> { "cc1@example.com", "cc2@example.com" };
        var bcc = new List<string> { "bcc@example.com" };

        await _sender.SendEmailAsync(to, "Test", "<html>Body</html>", cc, bcc);

        _mockSes.Verify(s => s.SendEmailAsync(
            It.Is<SendEmailRequest>(r =>
                r.Destination.ToAddresses.Contains("user@example.com") &&
                r.Destination.CcAddresses.Count == 2 &&
                r.Destination.CcAddresses.Contains("cc1@example.com") &&
                r.Destination.CcAddresses.Contains("cc2@example.com") &&
                r.Destination.BccAddresses.Count == 1 &&
                r.Destination.BccAddresses.Contains("bcc@example.com")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendEmailAsync_WithNullCcBcc_DoesNotSetCcBcc()
    {
        _mockSes.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendEmailResponse { MessageId = "msg-004" });

        await _sender.SendEmailAsync(new List<string> { "user@example.com" }, "Test", "<html>Body</html>", null, null);

        _mockSes.Verify(s => s.SendEmailAsync(
            It.Is<SendEmailRequest>(r =>
                r.Destination.ToAddresses.Contains("user@example.com") &&
                (r.Destination.CcAddresses == null || r.Destination.CcAddresses.Count == 0) &&
                (r.Destination.BccAddresses == null || r.Destination.BccAddresses.Count == 0)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendEmailAsync_WithEmptyCcBcc_DoesNotSetCcBcc()
    {
        _mockSes.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendEmailResponse { MessageId = "msg-005" });

        await _sender.SendEmailAsync(new List<string> { "user@example.com" }, "Test", "<html>Body</html>", new List<string>(), new List<string>());

        _mockSes.Verify(s => s.SendEmailAsync(
            It.Is<SendEmailRequest>(r =>
                r.Destination.ToAddresses.Contains("user@example.com") &&
                (r.Destination.CcAddresses == null || r.Destination.CcAddresses.Count == 0) &&
                (r.Destination.BccAddresses == null || r.Destination.BccAddresses.Count == 0)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
