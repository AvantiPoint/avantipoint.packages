using System.Net.Mail;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Host.Admin.Services.Email;

public sealed class NullEmailService(ILogger<NullEmailService> logger) : IEmailService
{
    public Task<bool> SendEmail<T>(string templateName, MailAddress to, string subject, T context)
    {
        logger.LogInformation("Email disabled. Would send {Template} to {To}: {Subject}", templateName, to, subject);
        return Task.FromResult(true);
    }
}
