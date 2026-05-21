using System.Net.Mail;
using AvantiPoint.Packages.Host.Admin.Configuration;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Host.Admin.Services.Email;

public sealed class AzureCommunicationServicesEmailService(
    EmailSettings settings,
    IEmailTemplateProvider templateProvider,
    ILogger<AzureCommunicationServicesEmailService> logger)
    : BaseEmailService(settings, templateProvider, logger)
{
    protected override async Task<bool> SendInternal(MailMessage message)
    {
        var config = settings.AzureCommunicationServices;
        var client = new EmailClient(config.ConnectionString);
        var sender = string.IsNullOrEmpty(config.SenderAddress) ? message.From.Address : config.SenderAddress;

        var emailContent = new EmailContent(message.Subject) { Html = message.Body };
        var emailMessage = new EmailMessage(
            sender,
            message.To[0].Address,
            emailContent);

        var operation = await client.SendAsync(WaitUntil.Completed, emailMessage);
        Logger.LogDebug("Azure email operation {Id}", operation.Id);
        return true;
    }
}
