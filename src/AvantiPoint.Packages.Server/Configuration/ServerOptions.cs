namespace AvantiPoint.Packages.Server.Configuration;

public class ServerOptions
{
    public bool UseNuGetUI { get; set; } = true;

    public GenericAuthOptions Authentication { get; set; } = new GenericAuthOptions();
}
