using Microsoft.Extensions.Caching.Memory;

namespace AvantiPoint.Packages.Registry.Native.Storage;

/// <summary>Bounded process-local cache of digests for verified immutable objects.</summary>
public sealed class NativeChecksumCache : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 4096 });

    public async Task<string> GetAsync(string key, Func<Task<string>> compute)
    {
        if (_cache.TryGetValue<string>(key, out var cached)) return cached!;
        var digest = await compute();
        _cache.Set(key, digest, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
        });
        return digest;
    }

    public void Dispose() => _cache.Dispose();
}
