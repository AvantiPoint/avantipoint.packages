using AvantiPoint.Packages.Core.Entities.Oci;
using AvantiPoint.Packages.Registry.Oci;
using System.Text;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class OciManifestParserTests
{
    [Theory]
    [InlineData("")]
    [InlineData(",\"platform\":null")]
    public void Parse_ImageManifestWithoutPlatformObject_DoesNotFail(string platformProperty)
    {
        var manifest = $$"""
            {
              "schemaVersion": 2,
              "mediaType": "application/vnd.oci.image.manifest.v1+json",
              "config": {
                "mediaType": "application/vnd.cncf.helm.config.v1+json",
                "size": 2,
                "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
              },
              "layers": []
              {{platformProperty}}
            }
            """;

        var result = OciManifestParser.Parse(
            "application/vnd.oci.image.manifest.v1+json; charset=utf-8",
            Encoding.UTF8.GetBytes(manifest));

        Assert.Equal(OciArtifactKind.Helm, result.ArtifactKind);
        Assert.Null(result.PlatformOs);
        Assert.Null(result.PlatformArch);
    }

    [Fact]
    public void Parse_PlatformObject_ProjectsOperatingSystemAndArchitecture()
    {
        const string manifest = """
            {
              "schemaVersion": 2,
              "mediaType": "application/vnd.oci.image.manifest.v1+json",
              "config": {
                "mediaType": "application/vnd.oci.image.config.v1+json",
                "size": 2,
                "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
              },
              "layers": [],
              "platform": { "os": "linux", "architecture": "arm64" }
            }
            """;

        var result = OciManifestParser.Parse(
            "application/vnd.oci.image.manifest.v1+json",
            Encoding.UTF8.GetBytes(manifest));

        Assert.Equal(OciArtifactKind.Image, result.ArtifactKind);
        Assert.Equal("linux", result.PlatformOs);
        Assert.Equal("arm64", result.PlatformArch);
    }

    [Fact]
    public void Parse_ArtifactManifest_ProjectsSubjectTypeAndAnnotations()
    {
        const string subjectDigest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string manifest = """
            {
              "schemaVersion": 2,
              "mediaType": "application/vnd.oci.image.manifest.v1+json",
              "artifactType": "application/spdx+json",
              "config": {
                "mediaType": "application/vnd.oci.empty.v1+json",
                "size": 2,
                "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
              },
              "layers": [],
              "subject": {
                "mediaType": "application/vnd.oci.image.manifest.v1+json",
                "size": 512,
                "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
              },
              "annotations": {
                "org.opencontainers.image.created": "2026-09-08T12:00:00Z"
              }
            }
            """;

        var result = OciManifestParser.Parse(
            "application/vnd.oci.image.manifest.v1+json",
            Encoding.UTF8.GetBytes(manifest));

        Assert.Equal(OciArtifactKind.Unknown, result.ArtifactKind);
        Assert.Equal("application/spdx+json", result.ArtifactType);
        Assert.NotNull(result.Subject);
        Assert.Equal(subjectDigest, result.Subject.Digest);
        Assert.Equal(512, result.Subject.Size);
        Assert.Equal("2026-09-08T12:00:00Z", result.Annotations["org.opencontainers.image.created"]);
    }
}
