using System.Net;
using System.Net.Mail;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Services.Email;
using Microsoft.Extensions.Logging.Abstractions;

namespace AvantiPoint.Packages.Host.Admin.Tests.Email;

public class ResendEmailServiceTests
{
    [Fact]
    public async Task SendInternal_PostsToResendApi()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var factory = new StubHttpClientFactory(handler);
        var settings = new EmailSettings
        {
            FromAddress = "noreply@test.local",
            FromName = "Test",
            Resend = new ResendSettings { ApiKey = "re_test" },
        };
        var service = new ResendEmailService(factory, settings, new EmbeddedEmailTemplateProvider(), NullLogger<ResendEmailService>.Instance);
        using var message = new MailMessage("noreply@test.local", "user@test.local")
        {
            Subject = "Hi",
            Body = "<p>Hello</p>",
            IsBodyHtml = true,
        };

        var result = await service.SendEmail(
            "welcome.html",
            message.To[0],
            message.Subject,
            new { SiteName = "Test", Host = "https://feed.test", Username = "bob@test.local" });

        Assert.True(result);
        Assert.Equal("https://api.resend.com/emails", handler.LastRequest?.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }
}
