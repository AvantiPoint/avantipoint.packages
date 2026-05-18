using System;
using System.Collections.Generic;
using AvantiPoint.Packages.Core.Signing;
using NuGet.Packaging.Signing;

namespace AvantiPoint.Packages.Tests.Signing;

/// <summary>
/// Test double for <see cref="ITimestampProviderFactory"/> that records requested URIs
/// and returns a configurable provider.
/// </summary>
internal sealed class TestTimestampProviderFactory : ITimestampProviderFactory
{
    private readonly Func<Uri, ITimestampProvider?>? _providerFactory;

    public TestTimestampProviderFactory(ITimestampProvider? provider = null)
    {
        if (provider is not null)
        {
            _providerFactory = _ => provider;
        }
    }

    public TestTimestampProviderFactory(Func<Uri, ITimestampProvider?> providerFactory)
    {
        _providerFactory = providerFactory;
    }

    private readonly List<Uri> _requestedUris = [];

    public IReadOnlyList<Uri> RequestedUris => _requestedUris;

    public ITimestampProvider? TryCreate(Uri timestampServerUri)
    {
        _requestedUris.Add(timestampServerUri);
        return _providerFactory?.Invoke(timestampServerUri);
    }
}
