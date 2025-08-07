using System;
using Microsoft.Extensions.Caching.Hybrid;

namespace AvantiPoint.Packages.Hosting.Caching;

#nullable enable
public sealed class CacheSettings
{
    internal const string CachePolicyName = "NugetCaching";

    public CacheType Type { get; set; } = CacheType.Hybrid;
    public string? RedisConnection { get; set; }
    public int DefaultTTLMinutes { get; set; } = 10;
    public TimeSpan DefaultTTL => TimeSpan.FromMinutes(DefaultTTLMinutes);
}
