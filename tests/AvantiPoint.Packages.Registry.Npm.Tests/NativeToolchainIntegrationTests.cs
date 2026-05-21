using AvantiPoint.Packages.Registry.Npm.Tests.Infrastructure;
using AvantiPoint.Packages.Registry.Tests.Shared;

namespace AvantiPoint.Packages.Registry.Npm.Tests;

/// <summary>
/// End-to-end tests using the npm CLI against a live in-process feed (publish, view, pack).
/// </summary>
public sealed class NativeToolchainIntegrationTests : IClassFixture<NpmFeedServerFixture>
{
    private readonly NpmFeedServerFixture _fixture;

    public NativeToolchainIntegrationTests(NpmFeedServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task NativeToolchain_PublishListAndDownload_Succeeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        if (!ToolAvailability.IsNpmAvailable)
        {
            Assert.Skip(ToolAvailability.NpmSkipReasonValue!);
        }

        var version = $"1.0.0-ci.{Guid.NewGuid():N}";
        var publishDir = NpmNativeToolchainTestHelper.PreparePublishDirectory(version);

        try
        {
            NpmNativeToolchainTestHelper.WriteNpmrc(
                _fixture.NpmRegistryUrl,
                FeedTestServerHost.DefaultApiKey,
                publishDir);

            NpmNativeToolchainTestHelper.PublishPackage(publishDir, FeedTestServerHost.DefaultApiKey);

            await NpmNativeToolchainTestHelper.ViewPackageAsync(
                _fixture.NpmRegistryUrl,
                NpmNativeToolchainTestHelper.HelloWorldPackageName,
                version,
                publishDir,
                cancellationToken);

            var packDir = NpmNativeToolchainTestHelper.CreateWorkingDirectory();
            try
            {
                var tarball = NpmNativeToolchainTestHelper.PackPackage(
                    _fixture.NpmRegistryUrl,
                    NpmNativeToolchainTestHelper.HelloWorldPackageName,
                    version,
                    packDir,
                    publishDir);

                Assert.True(new FileInfo(tarball).Length > 0);
            }
            finally
            {
                NpmNativeToolchainTestHelper.DeleteDirectory(packDir);
            }
        }
        finally
        {
            NpmNativeToolchainTestHelper.DeleteDirectory(publishDir);
        }
    }
}
