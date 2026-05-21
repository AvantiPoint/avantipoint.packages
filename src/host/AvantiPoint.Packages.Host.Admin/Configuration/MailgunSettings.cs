namespace AvantiPoint.Packages.Host.Admin.Configuration;

public class MailgunSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.mailgun.net";
}
