using System.Collections.Concurrent;
using AvantiPoint.Feed.Platform;

namespace AvantiPoint.Feed.Platform.Metrics;

public sealed class FeedMetricsService
{
    private readonly ConcurrentDictionary<string, long> _pushCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _pullCounts = new(StringComparer.OrdinalIgnoreCase);

    public void RecordPush(SurfaceContext surface, string artifact) =>
        Increment(_pushCounts, BuildKey(surface, artifact));

    public void RecordPull(SurfaceContext surface, string artifact) =>
        Increment(_pullCounts, BuildKey(surface, artifact));

    public IReadOnlyDictionary<string, long> GetPushCounts() => _pushCounts;

    public IReadOnlyDictionary<string, long> GetPullCounts() => _pullCounts;

    private static void Increment(ConcurrentDictionary<string, long> counts, string key) =>
        counts.AddOrUpdate(key, 1, (_, current) => current + 1);

    private static string BuildKey(SurfaceContext surface, string artifact) =>
        $"{surface.FeedId}:{surface.Protocol}:{surface.OciSegment ?? "default"}:{artifact}";
}
