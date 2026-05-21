using System.Net.Mail;
using AvantiPoint.Packages.Host.Admin.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace AvantiPoint.Packages.Host.Admin.Services.Email;

public sealed class SendGridEmailService(
    ISendGridClient client,
    EmailSettings settings,
    IEmailTemplateProvider templateProvider,
    ILogger<SendGridEmailService> logger)
    : BaseEmailService(settings, templateProvider, logger)
{
    protected override async Task<bool> SendInternal(MailMessage message)
    {
        var from = new EmailAddress(message.From.Address, message.From.DisplayName);
        var to = new EmailAddress(message.To[0].Address, message.To[0].DisplayName);
        var msg = MailHelper.CreateSingleEmail(from, to, message.Subject, null, message.Body);
        var response = await client.SendEmailAsync(msg);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("SendGrid failed: {Status}", response.StatusCode);
            return false;
        }

        return true;
    }
}
