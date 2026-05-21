namespace AvantiPoint.Packages.Host.Admin.Configuration;

public enum EmailProvider
{
    None,
    Postmark,
    SendGrid,
    Smtp,
    AmazonSes,
    AzureCommunicationServices,
    Mailgun,
    Resend,
}
