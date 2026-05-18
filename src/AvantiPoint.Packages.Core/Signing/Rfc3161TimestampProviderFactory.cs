using System;
using Microsoft.Extensions.Logging;
using NuGet.Packaging.Signing;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Default factory that creates <see cref="Rfc3161TimestampProvider"/> instances.
/// </summary>
public class Rfc3161TimestampProviderFactory(ILogger<Rfc3161TimestampProviderFactory> logger) : ITimestampProviderFactory
{
    public ITimestampProvider? TryCreate(Uri timestampServerUri)
    {
        try
        {
            return new Rfc3161TimestampProvider(timestampServerUri);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to create RFC 3161 timestamp provider for {TimestampServerUrl}",
                timestampServerUri);
            return null;
        }
    }
}
