namespace AvantiPoint.Packages.Host.Admin.Configuration;

public class EmailSettings
{
    public EmailProvider Provider { get; set; } = EmailProvider.None;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Package Feed";

    public PostmarkSettings Postmark { get; set; } = new();

    public SendGridSettings SendGrid { get; set; } = new();

    public SmtpSettings Smtp { get; set; } = new();

    public AmazonSesSettings AmazonSes { get; set; } = new();

    public AzureCommunicationServicesSettings AzureCommunicationServices { get; set; } = new();

    public MailgunSettings Mailgun { get; set; } = new();

    public ResendSettings Resend { get; set; } = new();
}

