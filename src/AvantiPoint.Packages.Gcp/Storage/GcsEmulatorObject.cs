using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
namespace AvantiPoint.Packages.Gcp.Storage;

internal sealed class GcsEmulatorObject
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("updated")]
    public DateTimeOffset? Updated { get; set; }
}
