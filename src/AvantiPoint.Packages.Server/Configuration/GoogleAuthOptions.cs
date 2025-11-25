namespace AvantiPoint.Packages.Server.Configuration;

public class GoogleAuthOptions : GenericAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string HostedDomain { get; set; } = string.Empty;
}
