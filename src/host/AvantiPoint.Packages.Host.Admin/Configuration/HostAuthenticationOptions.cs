namespace AvantiPoint.Packages.Host.Admin.Configuration;

public class HostAuthenticationOptions
{
    public HostMicrosoftOptions Microsoft { get; set; } = new();

    public HostGoogleOptions Google { get; set; } = new();

    public HostGitHubOptions GitHub { get; set; } = new();
}
