using System.Net.Mail;
using AvantiPoint.Packages.Host.Admin.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace AvantiPoint.Packages.Host.Admin.Services.Email;

public sealed class SmtpEmailService(
    EmailSettings settings,
    IEmailTemplateProvider templateProvider,
    ILogger<SmtpEmailService> logger)
    : BaseEmailService(settings, templateProvider, logger)
{
    protected override async Task<bool> SendInternal(MailMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(message.From.DisplayName, message.From.Address));
        mime.To.Add(new MailboxAddress(message.To[0].DisplayName, message.To[0].Address));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("html") { Text = message.Body };

        using var client = new MailKit.Net.Smtp.SmtpClient();
        await client.ConnectAsync(
            settings.Smtp.Host,
            settings.Smtp.Port,
            settings.Smtp.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

        if (!string.IsNullOrEmpty(settings.Smtp.Username))
        {
            await client.AuthenticateAsync(settings.Smtp.Username, settings.Smtp.Password);
        }

        await client.SendAsync(mime);
        await client.DisconnectAsync(true);
        return true;
    }
}
