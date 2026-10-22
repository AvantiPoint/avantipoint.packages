using System;

namespace AvantiPoint.Packages.Core.Utilities;

/// <summary>
/// Normalizes timestamps persisted by database providers that do not preserve
/// <see cref="DateTime.Kind"/>. Package timestamps are stored as UTC throughout
/// the feed, so an unspecified value read from storage must not be interpreted
/// using the server's local time zone.
/// </summary>
public static class PackageDateTime
{
    public static DateTimeOffset AsUtcOffset(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        return new DateTimeOffset(utc);
    }
}
