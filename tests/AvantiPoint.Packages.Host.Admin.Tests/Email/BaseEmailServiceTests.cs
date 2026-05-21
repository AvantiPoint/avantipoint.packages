using System.Net.Mail;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Services.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AvantiPoint.Packages.Host.Admin.Tests.Email;

public class BaseEmailServiceTests
{
    [Fact]
    public async Task SendEmail_RendersWelcomeTemplate()
    {
        var settings = new EmailSettings
        {
            Provider = EmailProvider.None,
            FromAddress = "noreply@test.local",
            FromName = "Test Feed",
        };
        var service = new CapturingEmailService(settings, new EmbeddedEmailTemplateProvider(), NullLogger.Instance);
        var sent = await service.SendEmail(
            "welcome.html",
            new MailAddress("user@test.local"),
            "Welcome",
            new { SiteName = "Test Feed", Host = "https://feed.test", Username = "ada@test.local" });

        Assert.True(sent);
        Assert.NotNull(service.LastMessage);
        Assert.Contains("Test Feed", service.LastMessage!.Body);
        Assert.Contains("ada@test.local", service.LastMessage.Body);
        Assert.Equal("Welcome", service.LastMessage.Subject);
        Assert.Equal("noreply@test.local", service.LastMessage.From.Address);
    }

    private sealed class CapturingEmailService(
        EmailSettings settings,
        IEmailTemplateProvider templateProvider,
        ILogger logger) : BaseEmailService(settings, templateProvider, logger)
    {
        public MailMessage? LastMessage { get; private set; }

        protected override Task<bool> SendInternal(MailMessage message)
        {
            LastMessage = message;
            return Task.FromResult(true);
        }
    }
}
