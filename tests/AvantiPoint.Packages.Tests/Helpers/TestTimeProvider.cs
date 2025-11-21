using System;

namespace AvantiPoint.Packages.Tests.Helpers;

/// <summary>
/// A test implementation of TimeProvider that allows controlling the current time.
/// </summary>
public class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public TestTimeProvider(DateTimeOffset initialTime)
    {
        _utcNow = initialTime;
    }

    public TestTimeProvider() : this(DateTimeOffset.UtcNow)
    {
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <summary>
    /// Advances the current time by the specified duration.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        _utcNow = _utcNow.Add(duration);
    }

    /// <summary>
    /// Sets the current time to the specified value.
    /// </summary>
    public void SetUtcNow(DateTimeOffset time)
    {
        _utcNow = time;
    }

    public override long GetTimestamp()
    {
        return _utcNow.Ticks;
    }
}

