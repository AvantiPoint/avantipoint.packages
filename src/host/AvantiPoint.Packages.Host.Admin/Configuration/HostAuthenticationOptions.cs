namespace AvantiPoint.Packages.Host.Admin.Configuration;

public class HostAuthenticationOptions
{
    public List<string> Providers { get; set; } = [];

    public MicrosoftEntraOptions MicrosoftEntra { get; set; } = new();

    public HostMicrosoftAccountOptions MicrosoftAccount { get; set; } = new();

    public HostGoogleOptions Google { get; set; } = new();

    public HostGitHubOptions GitHub { get; set; } = new();
}

