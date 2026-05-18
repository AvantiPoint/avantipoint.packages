using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;

namespace AvantiPoint.Packages.Tests.Signing;

/// <summary>
/// Records timestamp requests and optionally delegates to an inner provider.
/// </summary>
internal sealed class RecordingTimestampProvider : ITimestampProvider
{
    private readonly ITimestampProvider? _inner;

    public RecordingTimestampProvider(ITimestampProvider? inner = null)
    {
        _inner = inner;
    }

    public TimestampRequest? LastRequest { get; private set; }

    public int TimestampCallCount { get; private set; }

    public async Task<PrimarySignature> TimestampSignatureAsync(
        PrimarySignature primarySignature,
        TimestampRequest request,
        ILogger logger,
        CancellationToken token)
    {
        TimestampCallCount++;
        LastRequest = request;

        if (_inner is null)
        {
            return primarySignature;
        }

        return await _inner.TimestampSignatureAsync(primarySignature, request, logger, token);
    }
}
