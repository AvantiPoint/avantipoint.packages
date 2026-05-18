using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AvantiPoint.Packages.Gcp.Storage;

internal static class GcsEmulatorClient
{
    public static async Task<IReadOnlyList<GcsEmulatorObject>> ListObjectsAsync(
        Uri emulatorBaseUri,
        string bucket,
        string objectPrefix,
        CancellationToken cancellationToken)
    {
        var listUri = new Uri(
            emulatorBaseUri,
            $"/storage/v1/b/{bucket}/o?prefix={Uri.EscapeDataString(objectPrefix)}");

        using var http = new HttpClient();
        var response = await http.GetFromJsonAsync<GcsListResponse>(listUri, cancellationToken);
        return response?.Items ?? [];
    }

    public static async Task<bool> ObjectExistsAsync(
        Uri emulatorBaseUri,
        string bucket,
        string objectName,
        CancellationToken cancellationToken)
    {
        var metadataUri = new Uri(
            emulatorBaseUri,
            $"/storage/v1/b/{bucket}/o/{Uri.EscapeDataString(objectName)}");

        using var http = new HttpClient();
        using var response = await http.GetAsync(metadataUri, cancellationToken);
        return response.StatusCode != HttpStatusCode.NotFound;
    }

    public static async Task DeleteObjectAsync(
        Uri emulatorBaseUri,
        string bucket,
        string objectName,
        CancellationToken cancellationToken)
    {
        var deleteUri = new Uri(
            emulatorBaseUri,
            $"/storage/v1/b/{bucket}/o/{Uri.EscapeDataString(objectName)}");

        using var http = new HttpClient();
        using var response = await http.DeleteAsync(deleteUri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    public static async Task<Stream> DownloadObjectAsync(
        Uri emulatorBaseUri,
        string bucket,
        string objectName,
        CancellationToken cancellationToken)
    {
        var downloadUri = new Uri(
            emulatorBaseUri,
            $"/download/storage/v1/b/{bucket}/o/{Uri.EscapeDataString(objectName)}?alt=media");

        using var http = new HttpClient();
        using var response = await http.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException($"Object '{objectName}' was not found in bucket '{bucket}'.");
        }

        response.EnsureSuccessStatusCode();
        var stream = new MemoryStream();
        await response.Content.CopyToAsync(stream, cancellationToken);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    private sealed class GcsListResponse
    {
        [JsonPropertyName("items")]
        public List<GcsEmulatorObject>? Items { get; set; }
    }
}

internal sealed class GcsEmulatorObject
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("updated")]
    public DateTimeOffset? Updated { get; set; }
}
