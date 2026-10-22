using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using AvantiPoint.Packages.Host.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Admin.Tests.Pages;

public sealed class HostAuditLogServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly HostSqliteContext _context;

    public HostAuditLogServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new HostSqliteContext(
            new DbContextOptionsBuilder<HostSqliteContext>()
                .UseSqlite(_connection)
                .Options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestTwoHundredEvents_OnSqlite()
    {
        var timestamp = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
        _context.HostAuditEvents.AddRange(Enumerable.Range(0, 205).Select(index => new HostAuditEvent
        {
            Timestamp = timestamp.AddMinutes(index),
            Actor = "test",
            EventType = "package.published",
            Subject = $"Package.{index}",
        }));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new HostAuditLogService(_context);

        var events = await service.GetRecentAsync(
            eventType: null,
            limit: 200,
            TestContext.Current.CancellationToken);

        Assert.Equal(200, events.Count);
        Assert.Equal("Package.204", events[0].Subject);
        Assert.Equal("Package.5", events[^1].Subject);
        Assert.True(events.Zip(events.Skip(1), (current, next) => current.Id > next.Id).All(result => result));
    }

    [Fact]
    public async Task GetRecentAsync_FiltersByEventType_OnSqlite()
    {
        _context.HostAuditEvents.AddRange(
            CreateEvent("package.published", "Package.One"),
            CreateEvent("package.deleted", "Package.Two"),
            CreateEvent("package.published", "Package.Three"));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new HostAuditLogService(_context);

        var events = await service.GetRecentAsync(
            "package.published",
            limit: 200,
            TestContext.Current.CancellationToken);

        Assert.Equal(["Package.Three", "Package.One"], events.Select(e => e.Subject));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static HostAuditEvent CreateEvent(string eventType, string subject) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Actor = "test",
        EventType = eventType,
        Subject = subject,
    };
}
