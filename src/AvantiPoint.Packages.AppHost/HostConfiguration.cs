namespace AvantiPoint.Packages.AppHost;

internal class HostConfiguration
{
    public string StorageProvider { get; set; } = "FileSystem";

    public string DatabaseProvider { get; set; } = "Sqlite";
}
