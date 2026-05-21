namespace AvantiPoint.Packages.Protocol.Tests.Infrastructure;

/// <summary>
/// Locates repository paths from the test output directory.
/// </summary>
internal static class RepoPathResolver
{
    private static readonly Lazy<string> RepoRoot = new(FindRepoRoot);

    public static string RepositoryRoot => RepoRoot.Value;

    public static string HelloWorldTestPackageProjectPath =>
        Path.Combine(RepositoryRoot, "tests", "TestAssets", "AvantiPoint.Packages.HelloWorld.TestPackage", "AvantiPoint.Packages.HelloWorld.TestPackage.csproj");

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
