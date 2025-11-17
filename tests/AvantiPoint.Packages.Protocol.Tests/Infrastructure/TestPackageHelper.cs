using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Protocol.Tests.Infrastructure;

/// <summary>
/// Utilities for creating and uploading test NuGet packages.
/// </summary>
public static class TestPackageHelper
{
    /// <summary>
    /// Creates a simple test package in memory using NuGet.Packaging.
    /// </summary>
    /// <param name="packageId">Package ID</param>
    /// <param name="version">Package version</param>
    /// <param name="description">Optional package description</param>
    /// <returns>Byte array containing the .nupkg file</returns>
    public static byte[] CreatePackage(string packageId, string version, string? description = null)
    {
        description ??= $"Test package {packageId} version {version}";

        var builder = new PackageBuilder
        {
            Id = packageId,
            Version = NuGetVersion.Parse(version),
            Description = description
        };
        
        builder.Authors.Add("Test Author");

        // Add a dummy file so the package has content
        var dummyFile = new PhysicalPackageFile
        {
            SourcePath = CreateTempFile(),
            TargetPath = "lib/netstandard2.0/_.dll"
        };
        builder.Files.Add(dummyFile);

        using var stream = new MemoryStream();
        builder.Save(stream);
        
        // Clean up temp file
        try { File.Delete(dummyFile.SourcePath); } catch { }
        
        return stream.ToArray();
    }

    private static string CreateTempFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dll");
        File.WriteAllText(tempFile, "dummy content");
        return tempFile;
    }

    /// <summary>
    /// Uploads a package to the test server using HTTP PUT.
    /// </summary>
    /// <param name="httpClient">HTTP client pointing to the test server</param>
    /// <param name="packageBytes">Package bytes (from CreatePackage)</param>
    /// <param name="apiKey">API key for authentication (default: test-api-key-12345)</param>
    public static async Task<HttpResponseMessage> UploadPackageAsync(
        HttpClient httpClient,
        byte[] packageBytes,
        string apiKey = "test-api-key-12345")
    {
        using var content = new ByteArrayContent(packageBytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v2/package")
        {
            Content = content
        };
        request.Headers.Add("X-NuGet-ApiKey", apiKey);

        return await httpClient.SendAsync(request);
    }

    /// <summary>
    /// Creates and uploads a package to the test server.
    /// </summary>
    public static async Task<HttpResponseMessage> CreateAndUploadPackageAsync(
        HttpClient httpClient,
        string packageId,
        string version,
        string? description = null,
        string apiKey = "test-api-key-12345")
    {
        var package = CreatePackage(packageId, version, description);
        return await UploadPackageAsync(httpClient, package, apiKey);
    }

    /// <summary>
    /// Seeds the test server with a standard set of test packages.
    /// </summary>
    public static async Task SeedStandardPackagesAsync(HttpClient httpClient)
    {
        var packages = new[]
        {
            ("Test.PackageA", "1.0.0"),
            ("Test.PackageA", "1.1.0"),
            ("Test.PackageA", "2.0.0"),
            ("Test.PackageB", "2.0.0"),
            ("Test.PackageB", "2.1.0"),
            ("Test.PackageC", "1.0.0-beta"),
        };

        foreach (var (id, version) in packages)
        {
            var response = await CreateAndUploadPackageAsync(httpClient, id, version);
            response.EnsureSuccessStatusCode();
        }
    }
}
