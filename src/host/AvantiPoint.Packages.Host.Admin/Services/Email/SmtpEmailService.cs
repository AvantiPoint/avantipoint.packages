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
        var from = message.From ?? throw new InvalidOperationException("Email sender is required.");
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(from.DisplayName, from.Address));
        mime.To.Add(new MailboxAddress(message.To[0].DisplayName, message.To[0].Address));
        mime.Subject = message.Subject ?? string.Empty;
        mime.Body = new TextPart("html") { Text = message.Body ?? string.Empty };

        using var client = new MailKit.Net.Smtp.SmtpClient();
        await client.ConnectAsync(
            Settings.Smtp.Host,
            Settings.Smtp.Port,
            Settings.Smtp.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

        if (!string.IsNullOrEmpty(Settings.Smtp.Username))
        {
            if (string.IsNullOrEmpty(Settings.Smtp.Password))
            {
                throw new InvalidOperationException("SMTP password is required when a username is configured.");
            }

            await client.AuthenticateAsync(Settings.Smtp.Username, Settings.Smtp.Password);
        }

        await client.SendAsync(mime);
        await client.DisconnectAsync(true);
        return true;
    }
}
