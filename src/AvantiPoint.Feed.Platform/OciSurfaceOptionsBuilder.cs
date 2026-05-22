using AvantiPoint.Feed.Platform.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Feed.Platform;

public sealed class OciSurfaceOptionsBuilder
{
    internal OciSurfaceOptionsBuilder(IServiceCollection services, string segment)
    {
        Segment = segment;
        Services = services;
    }

    public string Segment { get; }

    public IServiceCollection Services { get; }

    public OciSurfaceOptionsBuilder Bind(IConfigurationSection section)
    {
        Services.AddOptions<OciFeedOptions>(GetOptionsName(Segment))
            .Bind(section);
        return this;
    }

    public static string GetOptionsName(string segment) => $"OciFeed:{segment}";

    public static string GetStorageSubPrefix(string? segment) =>
        string.IsNullOrEmpty(segment) ? "oci/" : $"oci/{segment.Trim('/')}/";
}
