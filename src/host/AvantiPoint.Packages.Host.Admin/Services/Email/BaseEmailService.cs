using System.Net.Mail;
using System.Text;
using AvantiPoint.Packages.Host.Admin.Configuration;
using HandlebarsDotNet;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Host.Admin.Services.Email;

public abstract class BaseEmailService(
    EmailSettings settings,
    IEmailTemplateProvider templateProvider,
    ILogger logger) : IEmailService
{
    protected MailAddress From { get; } = new(settings.FromAddress, settings.FromName);
    protected ILogger Logger { get; } = logger;

    public async Task<bool> SendEmail<T>(string templateName, MailAddress to, string subject, T context)
    {
        var htmlTemplate = templateProvider.ReadTemplate(templateName);
        Handlebars.RegisterHelper("Message", (output, ctx, _) =>
            output.WriteSafeString($"{ctx["Message"]}"));
        var template = Handlebars.Compile(htmlTemplate);

        try
        {
            using var message = new MailMessage(From, to)
            {
                Body = template(context),
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                Subject = subject,
            };
            return await SendInternal(message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send email {Template} to {To}", templateName, to);
            return false;
        }
    }

    protected abstract Task<bool> SendInternal(MailMessage message);
}
