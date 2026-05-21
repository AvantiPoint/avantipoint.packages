using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Services.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PostmarkDotNet;
using SendGrid;

namespace AvantiPoint.Packages.Host.Admin.Extensions;

public static class HostEmailServiceExtensions
{
    public static IServiceCollection AddHostEmailServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.AddSingleton<IEmailTemplateProvider, EmbeddedEmailTemplateProvider>();

        var settings = configuration.GetSection("EmailSettings").Get<EmailSettings>() ?? new EmailSettings();

        switch (settings.Provider)
        {
            case EmailProvider.Postmark when !string.IsNullOrWhiteSpace(settings.Postmark.ServerToken):
                services.AddSingleton(_ => new PostmarkClient(settings.Postmark.ServerToken));
                services.AddScoped<IEmailService, PostmarkEmailService>();
                break;
            case EmailProvider.SendGrid when !string.IsNullOrWhiteSpace(settings.SendGrid.ApiKey):
                services.AddSingleton<ISendGridClient>(_ => new SendGridClient(settings.SendGrid.ApiKey));
                services.AddScoped<IEmailService, SendGridEmailService>();
                break;
            case EmailProvider.Smtp when !string.IsNullOrWhiteSpace(settings.Smtp.Host):
                services.AddScoped<IEmailService, SmtpEmailService>();
                break;
            case EmailProvider.AmazonSes:
                services.AddScoped<IEmailService, AmazonSesEmailService>();
                break;
            case EmailProvider.AzureCommunicationServices
                when !string.IsNullOrWhiteSpace(settings.AzureCommunicationServices.ConnectionString):
                services.AddScoped<IEmailService, AzureCommunicationServicesEmailService>();
                break;
            case EmailProvider.Mailgun when !string.IsNullOrWhiteSpace(settings.Mailgun.ApiKey):
                services.AddHttpClient(nameof(MailgunEmailService));
                services.AddScoped<IEmailService, MailgunEmailService>();
                break;
            case EmailProvider.Resend when !string.IsNullOrWhiteSpace(settings.Resend.ApiKey):
                services.AddHttpClient(nameof(ResendEmailService));
                services.AddScoped<IEmailService, ResendEmailService>();
                break;
            default:
                services.AddScoped<IEmailService, NullEmailService>();
                break;
        }

        return services;
    }
}
