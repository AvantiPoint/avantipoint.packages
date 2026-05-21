using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using AvantiPoint.Packages.Host.Admin.Configuration;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Host.Admin.Services.Email;

public sealed class MailgunEmailService(
    IHttpClientFactory httpClientFactory,
    EmailSettings settings,
    IEmailTemplateProvider templateProvider,
    ILogger<MailgunEmailService> logger)
    : BaseEmailService(settings, templateProvider, logger)
{
    protected override async Task<bool> SendInternal(MailMessage message)
    {
        var config = settings.Mailgun;
        var client = httpClientFactory.CreateClient(nameof(MailgunEmailService));
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{config.ApiKey}"));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

        var uri = new Uri($"{config.BaseUrl.TrimEnd('/')}/v3/{config.Domain}/messages");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["from"] = message.From.ToString(),
            ["to"] = message.To[0].Address,
            ["subject"] = message.Subject,
            ["html"] = message.Body,
        });

        var response = await client.PostAsync(uri, content);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Logger.LogWarning("Mailgun failed: {Status} {Body}", response.StatusCode, body);
            return false;
        }

        return true;
    }
}
