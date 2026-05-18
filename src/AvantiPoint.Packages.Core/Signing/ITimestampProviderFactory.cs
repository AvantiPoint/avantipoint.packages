#nullable enable

using System;
using NuGet.Packaging.Signing;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Creates <see cref="ITimestampProvider"/> instances for package signing.
/// </summary>
public interface ITimestampProviderFactory
{
    /// <summary>
    /// Creates a timestamp provider for the given RFC 3161 server URI,
    /// or <c>null</c> if the URI is invalid or provider creation fails.
    /// </summary>
    ITimestampProvider? TryCreate(Uri timestampServerUri);
}
