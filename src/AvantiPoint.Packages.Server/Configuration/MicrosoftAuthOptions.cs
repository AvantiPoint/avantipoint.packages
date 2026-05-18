namespace AvantiPoint.Packages.Server.Configuration;

public class MicrosoftAuthOptions : GenericAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string TenantId { get; set; } = "common";
    public string ClientSecret { get; set; } = string.Empty;
}
