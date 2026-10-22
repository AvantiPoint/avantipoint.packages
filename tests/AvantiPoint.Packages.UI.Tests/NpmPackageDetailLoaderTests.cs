using AvantiPoint.Packages.UI.Components.Npm;
using AvantiPoint.Packages.UI.Services;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NpmPackageDetailLoaderTests
{
    [Fact]
    public void RequestedVersion_IsTheOnlyRichVersionRequestedFromTheBrowseService()
    {
        using var context = new BunitContext();
        var browse = new RecordingBrowseService();
        context.Services.AddSingleton<INpmPackageBrowseService>(browse);
        context.Services.AddSingleton<INpmPackageComparisonService>(new NoComparisonService());

        var component = context.Render<NpmPackageDetailLoader>(parameters => parameters
            .Add(parameter => parameter.PackageName, "large-history")
            .Add(parameter => parameter.RegistryUrl, "https://packages.example/npm/")
            .Add(parameter => parameter.RequestedVersion, "1.0.0"));

        component.WaitForAssertion(() =>
        {
            Assert.Equal("1.0.0", browse.LastDetailedVersion);
            Assert.Contains(
                "npm install large-history@1.0.0 --registry https://packages.example/npm/",
                component.Markup,
                StringComparison.Ordinal);
        });
    }

    private sealed class RecordingBrowseService : INpmPackageBrowseService
    {
        public string? LastDetailedVersion { get; private set; }

        public Task<IReadOnlyList<NpmPackageListItem>> SearchAsync(
            string? query,
            int skip,
            int take,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NpmPackageListItem>>([]);

        public Task<NpmPackageDetailModel?> GetPackageAsync(
            string packageName,
            string? detailedVersion = null,
            CancellationToken cancellationToken = default)
        {
            LastDetailedVersion = detailedVersion;
            return Task.FromResult<NpmPackageDetailModel?>(new(
                packageName,
                [
                    new NpmVersionListItem("2.0.0", DateTime.UtcNow, "sha-2"),
                    new NpmVersionListItem("1.0.0", DateTime.UtcNow.AddDays(-1), "sha-1"),
                ],
                new Dictionary<string, string> { ["latest"] = "2.0.0" }));
        }
    }

    private sealed class NoComparisonService : INpmPackageComparisonService
    {
        public Task<NpmPackageVersionComparison?> CompareAsync(
            string packageName,
            string baseVersion,
            string targetVersion,
            string? selectedFile = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<NpmPackageVersionComparison?>(null);
    }
}
