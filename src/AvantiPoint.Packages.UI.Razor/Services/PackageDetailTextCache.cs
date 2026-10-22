namespace AvantiPoint.Packages.UI.Services;

// Owned by one component, never a singleton or a cross-user/browser cache.
internal sealed class PackageDetailTextCache : IDisposable
{
    private const int Capacity = 4;
    private readonly Dictionary<string, Entry> entries = [];
    private readonly SemaphoreSlim gate = new(1);
    private long clock;
    private bool disposed;

    internal Task<string?> GetAsync(string key, Func<CancellationToken, Task<string?>> load)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (entries.TryGetValue(key, out var failed) && (failed.Task.IsFaulted || failed.Task.IsCanceled))
            entries.Remove(key);
        if (entries.TryGetValue(key, out var existing))
        {
            existing.Used = ++clock;
            return existing.Task;
        }
        if (entries.Count == Capacity)
        {
            var oldest = entries.MinBy(pair => pair.Value.Used);
            entries.Remove(oldest.Key);
            oldest.Value.Cancellation.Cancel();
        }
        var entry = new Entry { Used = ++clock };
        entries.Add(key, entry);
        entry.Task = LoadAsync(entry, load);
        return entry.Task;
    }

    private async Task<string?> LoadAsync(Entry entry, Func<CancellationToken, Task<string?>> load)
    {
        await gate.WaitAsync(entry.Cancellation.Token);
        try { return await load(entry.Cancellation.Token); }
        finally { gate.Release(); }
    }

    public void Dispose()
    {
        disposed = true;
        foreach (var entry in entries.Values) entry.Cancellation.Cancel();
        entries.Clear();
        // In-flight readers release the gate themselves, including non-cooperative providers.
    }

    private sealed class Entry
    {
        internal long Used;
        internal readonly CancellationTokenSource Cancellation = new();
        internal Task<string?> Task = null!;
    }
}
