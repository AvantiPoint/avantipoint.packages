using System.Net.Mail;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using AvantiPoint.Packages.Host.Admin.Configuration;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Host.Admin.Services.Email;

public sealed class AmazonSesEmailService(
    EmailSettings settings,
    IEmailTemplateProvider templateProvider,
    ILogger<AmazonSesEmailService> logger)
    : BaseEmailService(settings, templateProvider, logger)
{
    protected override async Task<bool> SendInternal(MailMessage message)
    {
        var config = settings.AmazonSes;
        var credentials = !string.IsNullOrEmpty(config.AccessKey) && !string.IsNullOrEmpty(config.SecretKey)
            ? new BasicAWSCredentials(config.AccessKey, config.SecretKey)
            : null;

        using var client = credentials != null
            ? new AmazonSimpleEmailServiceV2Client(credentials, RegionEndpoint.GetBySystemName(config.Region))
            : new AmazonSimpleEmailServiceV2Client(RegionEndpoint.GetBySystemName(config.Region));

        var request = new SendEmailRequest
        {
            FromEmailAddress = message.From.Address,
            Destination = new Destination { ToAddresses = [message.To[0].Address] },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = message.Subject },
                    Body = new Body
                    {
                        Html = new Content { Data = message.Body, Charset = "UTF-8" },
                    },
                },
            },
        };

        if (!string.IsNullOrEmpty(config.ConfigurationSetName))
        {
            request.ConfigurationSetName = config.ConfigurationSetName;
        }

        var response = await client.SendEmailAsync(request);
        Logger.LogDebug("SES message id {Id}", response.MessageId);
        return true;
    }
}
