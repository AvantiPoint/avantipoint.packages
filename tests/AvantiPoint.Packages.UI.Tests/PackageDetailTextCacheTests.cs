namespace AvantiPoint.Packages.UI.Tests;

public sealed class PackageDetailTextCacheTests
{
    [Fact]
    public async Task DeduplicatesAndSerializesReaders()
    {
        using var cache = new PackageDetailTextCache();
        var first = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var one = cache.GetAsync("one", _ => { calls++; return first.Task; });
        var same = cache.GetAsync("one", _ => throw new Exception("Duplicate request"));
        var two = cache.GetAsync("two", _ => { calls++; return Task.FromResult<string?>("two"); });
        Assert.Same(one, same);
        Assert.Equal(1, calls);
        first.SetResult("one");
        Assert.Equal("one", await same);
        Assert.Equal("two", await two);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task EvictsLeastRecentAndKeepsCachesIndependent()
    {
        using var cache = new PackageDetailTextCache();
        using var otherContext = new PackageDetailTextCache();
        var calls = 0;
        Task<string?> Load(CancellationToken _) { calls++; return Task.FromResult<string?>("text"); }
        foreach (var key in new[] { "a", "b", "c", "d" }) await cache.GetAsync(key, Load);
        await cache.GetAsync("a", Load);
        await cache.GetAsync("e", Load);
        await cache.GetAsync("b", Load);
        await otherContext.GetAsync("a", Load);
        Assert.Equal(7, calls);
    }

    [Fact]
    public async Task DisposalCancelsPendingReader()
    {
        var cache = new PackageDetailTextCache();
        var pending = cache.GetAsync("private", async token => { await Task.Delay(Timeout.Infinite, token); return "private"; });
        cache.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Throws<ObjectDisposedException>(() => cache.GetAsync("private", _ => Task.FromResult<string?>(null)));
    }
}
