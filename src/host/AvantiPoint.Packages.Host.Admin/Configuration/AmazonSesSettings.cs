namespace AvantiPoint.Packages.Host.Admin.Configuration;

public class AmazonSesSettings
{
    public string Region { get; set; } = "us-east-1";
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? ConfigurationSetName { get; set; }
}
