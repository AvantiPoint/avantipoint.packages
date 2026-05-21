namespace AvantiPoint.Packages.Registry.Tests.Shared;

public static class RepoPathResolver
{
    private static readonly Lazy<string> RepoRoot = new(FindRepoRoot);

    public static string RepositoryRoot => RepoRoot.Value;

    public static string HelloWorldNpmPackageDirectory =>
        Path.Combine(RepositoryRoot, "tests", "TestAssets", "AvantiPoint.Packages.HelloWorld.NpmPackage");

    public static string HelloWorldDockerContextDirectory =>
        Path.Combine(RepositoryRoot, "tests", "TestAssets", "AvantiPoint.Packages.HelloWorld.DockerImage");

    public static string HelloWorldHelmChartDirectory =>
        Path.Combine(RepositoryRoot, "tests", "TestAssets", "AvantiPoint.Packages.HelloWorld.HelmChart");

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "APPackages.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root (APPackages.slnx).");
    }
}
