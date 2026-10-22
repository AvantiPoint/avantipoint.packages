using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Host.Admin.Services.Events;
using AvantiPoint.Packages.Host.Admin.Services.Upstreams;
using AvantiPoint.Packages.Host.Pages.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AvantiPoint.Packages.Host.Admin.Tests.Pages;

public sealed class SourceWorkflowTests : IDisposable
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly SqliteContext db;
    private readonly Mock<IPackageSourceService> service = new();
    private readonly Mock<IPackageSourceConnectionTester> tester = new();
    private readonly PackageSourcesModel page;

    public SourceWorkflowTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<SqliteContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        page = new(db, service.Object, tester.Object, Mock.Of<ISecretProtector>(), Mock.Of<IFeedRegistry>(),
            NullLogger<PackageSourcesModel>.Instance, null!, Mock.Of<IHostEventService>())
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
            Input = new() { Name = "New source", FeedUrl = "https://registry.example.test", Ecosystem = "Npm" },
        };
    }

    [Fact]
    public async Task FailedConnectionNeverCreatesSource()
    {
        tester.Setup(t => t.TestAsync(It.IsAny<PackageSource>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PackageSourceConnectionResult(false, "Unauthorized"));
        Assert.IsType<PageResult>(await page.OnPostSaveAsync());
        Assert.False(page.ModelState.IsValid);
        service.Verify(s => s.AddAsync(It.IsAny<PackageSource>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(db.PackageSources);
    }

    [Fact]
    public async Task SuccessfulTestAppendsActiveSource()
    {
        db.PackageSources.Add(Source("Existing", 99));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        tester.Setup(t => t.TestAsync(It.IsAny<PackageSource>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PackageSourceConnectionResult(true, "Reachable"));
        service.Setup(s => s.AddAsync(It.IsAny<PackageSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PackageSource source, CancellationToken _) => source);
        Assert.IsType<RedirectToPageResult>(await page.OnPostSaveAsync());
        service.Verify(s => s.AddAsync(It.Is<PackageSource>(p => p.Priority == 1 && p.IsEnabled && p.LastSyncAttemptAt != null), It.IsAny<CancellationToken>()), Times.Once);
        tester.Verify(t => t.TestAsync(It.IsAny<PackageSource>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MoveNormalizesTiedPrioritiesAndPersistsOrder()
    {
        var a = Source("A", 0); var b = Source("B", 0); var c = Source("C", 50);
        db.PackageSources.AddRange(a, b, c);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await page.OnPostMoveAsync(c.Id, "up");
        db.ChangeTracker.Clear();
        Assert.Equal(new[] { "A", "C", "B" }, db.PackageSources.OrderBy(s => s.Priority).Select(s => s.Name).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, db.PackageSources.OrderBy(s => s.Priority).Select(s => s.Priority).ToArray());
    }

    [Fact]
    public async Task InvalidMoveDoesNotChangeOrder()
    {
        Assert.IsType<BadRequestResult>(await page.OnPostMoveAsync(1, "sideways"));
        Assert.IsType<NotFoundResult>(await page.OnPostMoveAsync(999, "up"));
    }

    [Fact]
    public void EditingDoesNotChangeOrderOrResumePausedSource()
    {
        var source = Source("Existing", 12);
        source.IsEnabled = false;
        page.Input.ApplyTo(source);
        Assert.Equal(12, source.Priority);
        Assert.False(source.IsEnabled);
    }

    private static PackageSource Source(string name, int priority) => new() { Name = name, FeedUrl = "https://registry.example.test", Priority = priority };

    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
