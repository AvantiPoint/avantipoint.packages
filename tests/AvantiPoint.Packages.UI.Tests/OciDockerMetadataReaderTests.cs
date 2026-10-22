using System.Text;
using AvantiPoint.Packages.UI.Services;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class OciDockerMetadataReaderTests
{
    private const string ConfigDigest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string LayerDigest = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void ReadManifest_ProjectsConfigLayersAndIndexPlatforms()
    {
        var imageManifest = Encoding.UTF8.GetBytes($$"""
            {
              "schemaVersion": 2,
              "config": {
                "mediaType": "application/vnd.oci.image.config.v1+json",
                "size": 512,
                "digest": "{{ConfigDigest}}"
              },
              "annotations": {
                "org.opencontainers.image.revision": "abc123",
                "org.opencontainers.image.source": "https://example.test/source"
              },
              "layers": [
                {
                  "mediaType": "application/vnd.oci.image.layer.v1.tar+gzip",
                  "size": 2048,
                  "digest": "{{LayerDigest}}"
                }
              ]
            }
            """);

        var manifest = OciDockerMetadataReader.ReadManifest(imageManifest);

        Assert.Equal(ConfigDigest, manifest.ConfigDigest);
        var layer = Assert.Single(manifest.Layers);
        Assert.Equal(1, layer.Number);
        Assert.Equal(LayerDigest, layer.Digest);
        Assert.Equal(2048, layer.Size);
        Assert.Equal("abc123", manifest.Annotations["org.opencontainers.image.revision"]);
        Assert.Equal("https://example.test/source", manifest.Annotations["org.opencontainers.image.source"]);

        var indexManifest = Encoding.UTF8.GetBytes($$"""
            {
              "schemaVersion": 2,
              "manifests": [
                {
                  "digest": "{{LayerDigest}}",
                  "size": 1234,
                  "platform": { "os": "linux", "architecture": "arm64", "variant": "v8" }
                }
              ]
            }
            """);

        var index = OciDockerMetadataReader.ReadManifest(indexManifest);

        var platform = Assert.Single(index.Platforms);
        Assert.Equal("linux", platform.OperatingSystem);
        Assert.Equal("arm64", platform.Architecture);
        Assert.Equal("v8", platform.Variant);
        Assert.Equal(1234, platform.Size);
    }

    [Fact]
    public void ReadConfiguration_ProjectsRuntimeLabelsAndNewestHistoryFirst()
    {
        var config = Encoding.UTF8.GetBytes("""
            {
              "created": "2026-09-07T10:15:30Z",
              "architecture": "arm64",
              "os": "linux",
              "variant": "v8",
              "config": {
                "Entrypoint": ["dotnet"],
                "Cmd": ["App.dll", "--serve"],
                "WorkingDir": "/app",
                "User": "10001",
                "Labels": {
                  "org.opencontainers.image.source": "https://example.test/repo",
                  "org.example.unsafe": "<script>alert(1)</script>"
                }
              },
              "history": [
                { "created": "2026-09-07T10:00:00Z", "created_by": "COPY . /app" },
                { "created": "2026-09-07T10:15:00Z", "created_by": "ENTRYPOINT [dotnet]", "empty_layer": true }
              ]
            }
            """);

        var result = OciDockerMetadataReader.ReadConfiguration(config);

        Assert.Equal(new DateTimeOffset(2026, 9, 7, 10, 15, 30, TimeSpan.Zero), result.ImageCreatedAt);
        Assert.Equal("linux", result.OperatingSystem);
        Assert.Equal("arm64", result.Architecture);
        Assert.Equal("v8", result.Variant);
        Assert.Equal(["dotnet"], result.Entrypoint);
        Assert.Equal(["App.dll", "--serve"], result.Command);
        Assert.Equal("/app", result.WorkingDirectory);
        Assert.Equal("10001", result.User);
        Assert.Equal("<script>alert(1)</script>", result.Labels["org.example.unsafe"]);
        Assert.Equal("ENTRYPOINT [dotnet]", result.History[0].CreatedBy);
        Assert.True(result.History[0].EmptyLayer);
        Assert.Equal("COPY . /app", result.History[1].CreatedBy);
    }

    [Fact]
    public void HelmConfigDescriptor_IsNotTreatedAsDockerImageConfiguration()
    {
        var manifest = Encoding.UTF8.GetBytes($$"""
            {
              "schemaVersion": 2,
              "config": {
                "mediaType": "application/vnd.cncf.helm.config.v1+json",
                "digest": "{{ConfigDigest}}"
              },
              "layers": []
            }
            """);

        Assert.Null(OciDockerMetadataReader.ReadManifest(manifest).ConfigDigest);
    }
}
