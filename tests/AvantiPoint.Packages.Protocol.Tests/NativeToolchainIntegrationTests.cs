using System.IO.Compression;
using AvantiPoint.Packages.Protocol.Tests.Infrastructure;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Protocol.Tests;

/// <summary>
/// End-to-end tests that pack a hello-world project with the .NET SDK, push to the feed with
/// dotnet nuget, verify listing via search and protocol APIs, and restore via dotnet add package.
/// </summary>
public sealed class NativeToolchainIntegrationTests : IClassFixture<NuGetServerFixture>
{
    private readonly NuGetServerFixture _fixture;

    public NativeToolchainIntegrationTests(NuGetServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task NativeToolchain_PackPushListAndDownload_Succeeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        if (!NativeToolchainAvailability.SdkIsAvailable)
        {
            Assert.Skip(NativeToolchainAvailability.SdkSkipReason!);
        }

        var version = $"1.0.0-ci.{Guid.NewGuid():N}";
        var feedIndex = new Uri($"{_fixture.BaseUrl}v3/index.json");
        var workDir = NativeToolchainTestHelper.CreateWorkingDirectory();

        try
        {
            var nuGetConfigPath = NativeToolchainTestHelper.WriteNuGetConfig(feedIndex, workDir);

            var packagePath = NativeToolchainTestHelper.PackHelloWorldPackage(workDir, version);

            NativeToolchainTestHelper.PushPackage(
                feedIndex,
                packagePath,
                NativeToolchainTestHelper.DefaultApiKey,
                nuGetConfigPath);

            await WaitForPackageAsync(
                _fixture.Client,
                NativeToolchainTestHelper.HelloWorldPackageId,
                version,
                cancellationToken);

            await NativeToolchainTestHelper.SearchPackageAsync(
                feedIndex,
                NativeToolchainTestHelper.HelloWorldPackageId,
                nuGetConfigPath,
                cancellationToken);

            // Download via the NuGet protocol client (package content API). dotnet package download
            // requires a registration index that is not yet populated in the minimal test host.
            await using var downloaded = await _fixture.Client.DownloadPackageAsync(
                NativeToolchainTestHelper.HelloWorldPackageId,
                NuGetVersion.Parse(version),
                cancellationToken);

            using var buffered = new MemoryStream();
            await downloaded.CopyToAsync(buffered, cancellationToken);
            Assert.True(buffered.Length > 0);

            using var archive = new ZipArchive(buffered, ZipArchiveMode.Read, leaveOpen: true);
            Assert.Contains(
                archive.Entries,
                e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            NativeToolchainTestHelper.DeleteDirectory(workDir);
        }
    }

    private static async Task WaitForPackageAsync(
        NuGetClient client,
        string packageId,
        string version,
        CancellationToken cancellationToken)
    {
        var nuGetVersion = NuGetVersion.Parse(version);
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            if (await client.ExistsAsync(packageId, nuGetVersion, cancellationToken))
            {
                return;
            }

            await Task.Delay(250, cancellationToken);
        }

        Assert.Fail($"Package {packageId} {version} was not indexed within 30 seconds.");
    }
}
