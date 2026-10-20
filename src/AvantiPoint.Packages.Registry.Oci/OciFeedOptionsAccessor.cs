using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Registry.Oci;

public sealed class OciFeedOptionsAccessor
{
    public const string DefaultOptionsName = "OciFeed:default";

    private readonly IOptionsMonitor<OciFeedOptions> _optionsMonitor;

    public OciFeedOptionsAccessor(IOptionsMonitor<OciFeedOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public OciFeedOptions GetOptions(SurfaceContext surface)
    {
        var optionsName = string.IsNullOrEmpty(surface.OciSegment)
            ? DefaultOptionsName
            : OciSurfaceOptionsBuilder.GetOptionsName(surface.OciSegment);

        return _optionsMonitor.Get(optionsName);
    }
}
