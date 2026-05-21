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
        var html = message.IsBodyHtml ? message.Body : null;
        var plainText = message.IsBodyHtml ? null : message.Body;
        var response = await client.SendMessageAsync(
            message.From.ToString(),
            message.To[0].Address,
            message.Subject,
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
