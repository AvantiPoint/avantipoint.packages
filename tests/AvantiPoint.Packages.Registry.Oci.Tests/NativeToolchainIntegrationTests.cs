using System.Net;
using AvantiPoint.Packages.Registry.Oci.Tests.Infrastructure;
using AvantiPoint.Packages.Registry.Tests.Shared;
using Xunit;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

/// <summary>
/// End-to-end tests using the Docker CLI against a live in-process OCI registry (build, push, pull, list tags).
/// </summary>
[Collection(nameof(OciFeedServerCollection))]
public sealed class NativeToolchainIntegrationTests : IClassFixture<OciFeedServerFixture>
{
    private readonly OciFeedServerFixture _fixture;

    public NativeToolchainIntegrationTests(OciFeedServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task NativeToolchain_BuildPushListAndPull_Succeeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        if (!ToolAvailability.IsDockerAvailable)
        {
            Assert.Skip(ToolAvailability.DockerSkipReasonValue!);
        }

        var repository = $"hello-world/{Guid.NewGuid():N}";
        var tag = "1.0.0";
        var imageTag = $"{_fixture.DockerRegistryHost}/{repository}:{tag}";
        var workDir = DockerNativeToolchainTestHelper.CreateWorkingDirectory();

        try
        {
            var dockerConfigDir = DockerNativeToolchainTestHelper.ConfigureDockerConfig(workDir, _fixture.DockerRegistryHost);

            var contextDir = RepoPathResolver.HelloWorldDockerContextDirectory;

            try
            {
                await DockerNativeToolchainTestHelper.EnsureRepositoryExistsAsync(
                    _fixture.Server.Client,
                    repository,
                    cancellationToken);

                DockerNativeToolchainTestHelper.BuildImage(contextDir, imageTag, dockerConfigDir);
                DockerNativeToolchainTestHelper.PushImage(imageTag, dockerConfigDir);

                await DockerNativeToolchainTestHelper.AssertTagsListContainsAsync(
                    _fixture.Server.Client,
                    repository,
                    tag,
                    cancellationToken);

                DockerNativeToolchainTestHelper.PullImage(imageTag, dockerConfigDir);
            }
            catch (InvalidOperationException ex) when (
                ex.Message.Contains("insecure", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("HTTP response to HTTPS", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Skip(ex.Message);
            }
        }
        finally
        {
            try
            {
                CliProcessRunner.Run("docker", $"rmi -f \"{imageTag}\"", timeout: TimeSpan.FromMinutes(2));
            }
            catch
            {
            }

            DockerNativeToolchainTestHelper.DeleteDirectory(workDir);
        }
    }

    [Fact]
    public async Task NativeToolchain_HelmPushAndListTags_Succeeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        if (!ToolAvailability.IsHelmAvailable)
        {
            Assert.Skip(ToolAvailability.HelmSkipReasonValue!);
        }

        var chartName = $"ap-helm-{Guid.NewGuid():N}";
        var version = "1.0.0";
        var registryHost = _fixture.Server.BaseAddress.Authority;
        var chartDir = HelmNativeToolchainTestHelper.PrepareChartDirectory(chartName, version);

        try
        {
            var packagePath = HelmNativeToolchainTestHelper.PackageChart(chartDir);

            try
            {
                HelmNativeToolchainTestHelper.PushChart(
                    packagePath,
                    registryHost,
                    HelmNativeToolchainTestHelper.HelmOciSegment);

                await DockerNativeToolchainTestHelper.AssertTagsListContainsAsync(
                    _fixture.Server.Client,
                    chartName,
                    version,
                    cancellationToken,
                    HelmNativeToolchainTestHelper.HelmOciSegment);
            }
            catch (InvalidOperationException ex) when (
                ex.Message.Contains("insecure", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("plain HTTP", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("HTTP response to HTTPS", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Skip(ex.Message);
            }
        }
        finally
        {
            HelmNativeToolchainTestHelper.DeleteDirectory(chartDir);
        }
    }

    [Fact]
    public async Task NativeToolchain_OciApi_IsReachable()
    {
        if (!ToolAvailability.IsDockerAvailable)
        {
            Assert.Skip(ToolAvailability.DockerSkipReasonValue!);
        }

        var response = await _fixture.Server.Client.GetAsync("/v2/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("registry/2.0", response.Headers.GetValues("Docker-Distribution-API-Version").Single());
    }
}
