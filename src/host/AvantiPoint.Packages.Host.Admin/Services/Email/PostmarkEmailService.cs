using System.Net.Mail;
using AvantiPoint.Packages.Host.Admin.Configuration;
using Microsoft.Extensions.Logging;
using PostmarkDotNet;

namespace AvantiPoint.Packages.Host.Admin.Services.Email;

public sealed class PostmarkEmailService(
    PostmarkClient client,
    EmailSettings settings,
    IEmailTemplateProvider templateProvider,
    ILogger<PostmarkEmailService> logger)
    : BaseEmailService(settings, templateProvider, logger)
{
    protected override async Task<bool> SendInternal(MailMessage message)
    {
        var from = message.From ?? throw new InvalidOperationException("Email sender is required.");
        var html = message.IsBodyHtml ? message.Body : null;
        var plainText = message.IsBodyHtml ? null : message.Body;
        var response = await client.SendMessageAsync(
            from.ToString(),
            message.To[0].Address,
            message.Subject ?? string.Empty,
            plainText,
            html);

        if (response.Status != PostmarkStatus.Success)
        {
            Logger.LogWarning("Postmark failed: {Status} {Error}", response.Status, response.Message);
            return false;
        }

        return true;
    }
}
