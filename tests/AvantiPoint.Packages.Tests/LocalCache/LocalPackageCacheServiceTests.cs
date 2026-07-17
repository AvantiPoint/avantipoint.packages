using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Tests.LocalCache;

public sealed class LocalPackageCacheServiceTests : IDisposable
{
    private readonly string _cacheRoot = Path.Combine(
        Path.GetTempPath(),
        "avantipoint-packages-local-cache-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task TryOpenPackageAsync_WhenEnabled_ReadsNormalizedGlobalPackagesLayout()
    {
        var packagePath = CreatePackage("Example.Package", "1.2.3-BETA", [1, 2, 3]);
        var service = CreateService(new LocalCacheOptions
        {
            Enabled = true,
            Path = _cacheRoot,
            CopyToFeedStorage = true,
        });

        var entry = await service.TryOpenPackageAsync(
            "Example.Package",
            NuGetVersion.Parse("1.2.3-BETA"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(entry);
        await using (entry.Content)
        {
            using var buffer = new MemoryStream();
            await entry.Content.CopyToAsync(buffer, TestContext.Current.CancellationToken);
            Assert.Equal([1, 2, 3], buffer.ToArray());
        }

        Assert.Equal(Path.GetFullPath(packagePath), entry.Path);
        Assert.True(entry.CopyToFeedStorage);
    }

    [Fact]
    public async Task TryOpenPackageAsync_WhenDisabled_DoesNotReadExistingPackage()
    {
        CreatePackage("Example.Package", "1.0.0", [1, 2, 3]);
        var service = CreateService(new LocalCacheOptions
        {
            Enabled = false,
            Path = _cacheRoot,
        });

        var entry = await service.TryOpenPackageAsync(
            "Example.Package",
            NuGetVersion.Parse("1.0.0"),
            TestContext.Current.CancellationToken);

        Assert.Null(entry);
    }

    [Fact]
    public async Task TryOpenPackageAsync_WhenPackageIsMissing_ReturnsNull()
    {
        var service = CreateService(new LocalCacheOptions
        {
            Enabled = true,
            Path = _cacheRoot,
        });

        var entry = await service.TryOpenPackageAsync(
            "Missing.Package",
            NuGetVersion.Parse("1.0.0"),
            TestContext.Current.CancellationToken);

        Assert.Null(entry);
    }

    [Fact]
    public async Task TryOpenPackageAsync_WhenPackageIdEscapesRoot_ReturnsNull()
    {
        var service = CreateService(new LocalCacheOptions
        {
            Enabled = true,
            Path = _cacheRoot,
        });

        var entry = await service.TryOpenPackageAsync(
            "..",
            NuGetVersion.Parse("1.0.0"),
            TestContext.Current.CancellationToken);

        Assert.Null(entry);
    }

    [Fact]
    public async Task TryOpenPackageAsync_WhenConfiguredPathIsInvalid_ReturnsNull()
    {
        var service = CreateService(new LocalCacheOptions
        {
            Enabled = true,
            Path = "invalid\0path",
        });

        var entry = await service.TryOpenPackageAsync(
            "Example.Package",
            NuGetVersion.Parse("1.0.0"),
            TestContext.Current.CancellationToken);

        Assert.Null(entry);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }

    private LocalPackageCacheService CreateService(LocalCacheOptions options) =>
        new(
            Options.Create(options),
            Mock.Of<ILogger<LocalPackageCacheService>>());

    private string CreatePackage(string packageId, string version, byte[] content)
    {
        var normalizedId = packageId.ToLowerInvariant();
        var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
        var packageDirectory = Path.Combine(_cacheRoot, normalizedId, normalizedVersion);
        Directory.CreateDirectory(packageDirectory);

        var packagePath = Path.Combine(
            packageDirectory,
            $"{normalizedId}.{normalizedVersion}.nupkg");
        File.WriteAllBytes(packagePath, content);
        return packagePath;
    }
}
