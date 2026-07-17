using System.Collections.Concurrent;
using Prometheus;

namespace AvantiPoint.Feed.Platform.Metrics;

public sealed class FeedMetricsService
{
    private static readonly string[] LabelNames = ["feed", "type"];

    private readonly ConcurrentDictionary<FeedMetricKey, long> _pushCounts = new();
    private readonly ConcurrentDictionary<FeedMetricKey, long> _pullCounts = new();
    private readonly ConcurrentDictionary<FeedMetricKey, long> _blobBytes = new();
    private readonly Counter _pushCounter;
    private readonly Counter _pullCounter;
    private readonly Gauge _blobBytesGauge;

    public FeedMetricsService(IMetricFactory metricFactory)
    {
        _pushCounter = metricFactory.CreateCounter(
            "feed_push_total",
            "Completed artifact pushes by feed and protocol.",
            LabelNames);
        _pullCounter = metricFactory.CreateCounter(
            "feed_pull_total",
            "Completed artifact pulls by feed and protocol.",
            LabelNames);
        _blobBytesGauge = metricFactory.CreateGauge(
            "blob_bytes_stored",
            "Bytes currently stored in content-addressed blob storage by feed and protocol.",
            LabelNames);
    }

    public void RecordPush(SurfaceContext surface)
    {
        var key = FeedMetricKey.From(surface);
        Increment(_pushCounts, key);
        _pushCounter.WithLabels(key.FeedId, key.Protocol).Inc();
    }

    public void RecordPull(SurfaceContext surface)
    {
        var key = FeedMetricKey.From(surface);
        Increment(_pullCounts, key);
        _pullCounter.WithLabels(key.FeedId, key.Protocol).Inc();
    }

    public void RecordBlobBytes(SurfaceContext surface, long delta)
        => RecordBlobBytes(surface.FeedId, surface.Protocol, delta);

    public void RecordBlobBytes(string feedId, FeedProtocol protocol, long delta)
    {
        var key = FeedMetricKey.From(feedId, protocol);
        var current = _blobBytes.AddOrUpdate(
            key,
            _ => Math.Max(0, delta),
            (_, value) => Math.Max(0, value + delta));
        _blobBytesGauge.WithLabels(key.FeedId, key.Protocol).Set(current);
    }

    public void SetBlobBytes(SurfaceContext surface, long value)
        => SetBlobBytes(surface.FeedId, surface.Protocol, value);

    public void SetBlobBytes(string feedId, FeedProtocol protocol, long value)
    {
        var key = FeedMetricKey.From(feedId, protocol);
        var current = Math.Max(0, value);
        _blobBytes[key] = current;
        _blobBytesGauge.WithLabels(key.FeedId, key.Protocol).Set(current);
    }

    public IReadOnlyDictionary<string, long> GetPushCounts() => Snapshot(_pushCounts);

    public IReadOnlyDictionary<string, long> GetPullCounts() => Snapshot(_pullCounts);

    public IReadOnlyDictionary<string, long> GetBlobBytes() => Snapshot(_blobBytes);

    private static void Increment(ConcurrentDictionary<FeedMetricKey, long> counts, FeedMetricKey key) =>
        counts.AddOrUpdate(key, 1, (_, current) => current + 1);

    private static IReadOnlyDictionary<string, long> Snapshot(
        ConcurrentDictionary<FeedMetricKey, long> values) =>
        values.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    private readonly record struct FeedMetricKey(string FeedId, string Protocol)
    {
        public static FeedMetricKey From(SurfaceContext surface) =>
            From(surface.FeedId, surface.Protocol);

        public static FeedMetricKey From(string feedId, FeedProtocol protocol) =>
            new(feedId, protocol.ToString().ToLowerInvariant());

        public override string ToString() => $"{FeedId}:{Protocol}";
    }
}
