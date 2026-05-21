using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using AvantiPoint.Packages.Host.Admin.Configuration;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Host.Admin.Services.Email;

public sealed class ResendEmailService(
    IHttpClientFactory httpClientFactory,
    EmailSettings settings,
    IEmailTemplateProvider templateProvider,
    ILogger<ResendEmailService> logger)
    : BaseEmailService(settings, templateProvider, logger)
{
    protected override async Task<bool> SendInternal(MailMessage message)
    {
        var client = httpClientFactory.CreateClient(nameof(ResendEmailService));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", settings.Resend.ApiKey);

        var payload = new
        {
            from = message.From.ToString(),
            to = new[] { message.To[0].Address },
            subject = message.Subject,
            html = message.Body,
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://api.resend.com/emails", content);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Logger.LogWarning("Resend failed: {Status} {Body}", response.StatusCode, body);
            return false;
        }

        return true;
    }
}
