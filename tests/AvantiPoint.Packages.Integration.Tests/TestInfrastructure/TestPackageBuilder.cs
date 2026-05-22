using System.Net.Http.Headers;
using NuGet.Packaging;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Integration.Tests.TestInfrastructure;

/// <summary>
/// Creates and uploads in-memory .nupkg files to a feed over HTTP.
/// </summary>
internal static class TestPackageBuilder
{
    public const string DefaultApiKey = "integration-test-key";

    public static byte[] CreatePackage(string packageId, string version, string? description = null)
    {
        description ??= $"Test package {packageId} {version}";

        var builder = new PackageBuilder
        {
            Id = packageId,
            Version = NuGetVersion.Parse(version),
            Description = description,
        };
        builder.Authors.Add("Integration Test");

        var dummyFile = new PhysicalPackageFile
        {
            SourcePath = CreateTempFile(),
            TargetPath = "lib/netstandard2.0/_.dll",
        };
        builder.Files.Add(dummyFile);

        using var stream = new MemoryStream();
        builder.Save(stream);

        try
        {
            File.Delete(dummyFile.SourcePath);
        }
        catch
        {
            // Best effort cleanup
        }

        return stream.ToArray();
    }

    public static async Task PublishAsync(
        HttpClient client,
        string packageId,
        string version,
        string apiKey = DefaultApiKey,
        CancellationToken cancellationToken = default)
    {
        var bytes = CreatePackage(packageId, version);
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v2/package")
        {
            Content = content,
        };
        request.Headers.Add("X-NuGet-ApiKey", apiKey);

        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public static string GetPackageDownloadPath(string packageId, string version) =>
        $"/v3/package/{packageId.ToLowerInvariant()}/{version}/{packageId.ToLowerInvariant()}.{version}.nupkg";

    private static string CreateTempFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dll");
        File.WriteAllText(tempFile, "dummy content");
        return tempFile;
    }
}
