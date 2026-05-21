using System.Net.Mail;

namespace AvantiPoint.Packages.Host.Admin.Services.Email;

public interface IEmailService
{
    Task<bool> SendEmail<T>(string templateName, MailAddress to, string subject, T context);
}
