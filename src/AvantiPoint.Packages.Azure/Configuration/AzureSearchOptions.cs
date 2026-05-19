namespace AvantiPoint.Packages.Azure.Configuration;

public class AzureSearchOptions
{
    public string Endpoint { get; set; }

    public string ApiKey { get; set; }

    public string IndexName { get; set; } = "packages";
}
