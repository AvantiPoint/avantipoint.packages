namespace AvantiPoint.Packages.UI.Services;

// Owned by one component, never a singleton or a cross-user/browser cache.
// Mutations run on the owning component's render dispatcher.
internal sealed class PackageDetailTextCache : IDisposable
{
    private const int Capacity = 4;
    private readonly Dictionary<string, Entry> entries = [];
    private readonly SemaphoreSlim gate = new(1);
    private long clock;
    private bool disposed;

    internal Task<string?> GetAsync(string key, Func<CancellationToken, Task<string?>> load, bool speculative = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!speculative)
        {
            foreach (var pair in entries.Where(pair => pair.Key != key && pair.Value.Speculative).ToList())
            {
                entries.Remove(pair.Key);
                pair.Value.Cancellation.Cancel();
            }
        }
        if (entries.TryGetValue(key, out var failed) && (failed.Task.IsFaulted || failed.Task.IsCanceled))
            entries.Remove(key);
        if (entries.TryGetValue(key, out var existing))
        {
            existing.Used = ++clock;
            existing.Speculative &= speculative;
            return existing.Task;
        }
        if (entries.Count == Capacity)
        {
            var oldest = entries.MinBy(pair => pair.Value.Used);
            entries.Remove(oldest.Key);
            oldest.Value.Cancellation.Cancel();
        }
        var entry = new Entry { Used = ++clock, Speculative = speculative };
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
        internal bool Speculative;
        internal readonly CancellationTokenSource Cancellation = new();
        internal Task<string?> Task = null!;
    }
}
