using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Npm;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Publishers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AvantiPoint.Packages.Host.Admin.Tests.Publishers;

public sealed class NpmDownstreamPublisherTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteContext _context;

    public NpmDownstreamPublisherTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new SqliteContext(new DbContextOptionsBuilder<SqliteContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task PushAsync_MissingTarball_ReturnsFalse_InsteadOfThrowing()
    {
        // A file-backed IPathBlobStore throws FileNotFoundException/DirectoryNotFoundException for a
        // missing blob instead of returning null (matching PathBlobStore.GetAsync's real behavior).
        // A single package's tarball going missing must fail only that package - not throw out of
        // PushAsync and abort the caller's (SyndicationService's) whole group promotion loop.
        SeedNpmVersion("some-package", "1.0.0");
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var pathStore = new Mock<IPathBlobStore>();
        pathStore
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        var storageFactory = new Mock<IStorageBackendFactory>();
        storageFactory.Setup(f => f.CreatePathStore("npm/")).Returns(pathStore.Object);

        var publisher = new NpmDownstreamPublisher(
            _context,
            storageFactory.Object,
            new NullSecretProtector(),
            Mock.Of<IHttpClientFactory>(MockBehavior.Strict),
            Mock.Of<ILogger<NpmDownstreamPublisher>>());

        var target = new HostPublishTarget { Name = "npm-registry", Protocol = PublishTargetProtocol.Npm, PublishEndpoint = "https://registry.example.com" };

        var pushed = await publisher.PushAsync("some-package", version: null, target, TestContext.Current.CancellationToken);

        Assert.False(pushed);
    }

    [Fact]
    public async Task PushAsync_NoMatchingVersion_ReturnsFalse()
    {
        var publisher = new NpmDownstreamPublisher(
            _context,
            Mock.Of<IStorageBackendFactory>(MockBehavior.Strict),
            new NullSecretProtector(),
            Mock.Of<IHttpClientFactory>(MockBehavior.Strict),
            Mock.Of<ILogger<NpmDownstreamPublisher>>());

        var target = new HostPublishTarget { Name = "npm-registry", Protocol = PublishTargetProtocol.Npm, PublishEndpoint = "https://registry.example.com" };

        var pushed = await publisher.PushAsync("does-not-exist", version: null, target, TestContext.Current.CancellationToken);

        Assert.False(pushed);
    }

    private void SeedNpmVersion(string name, string version)
    {
        _context.NpmPackages.Add(new NpmPackage
        {
            Name = name,
            Versions =
            [
                new NpmVersion
                {
                    Version = version,
                    TarballPath = $"{name}/-/{name}-{version}.tgz",
                    Shasum = "deadbeef",
                    PackumentJson = "{}",
                    Origin = PackageOrigin.Published,
                },
            ],
        });
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
