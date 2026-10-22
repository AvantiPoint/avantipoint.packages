using System.Text.Json;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class OciSubjectDescriptorTests
{
    [Theory]
    [InlineData("sha256", 64)]
    [InlineData("sha512", 128)]
    public void ValidSubject_RemainsWeakAssociation(string algorithm, int length)
    {
        var digest = algorithm + ":" + new string('a', length);
        var parsed = OciManifestParser.Parse("application/vnd.oci.image.manifest.v1+json", Manifest(digest));
        Assert.Equal(digest, parsed.Subject?.Digest);
        Assert.DoesNotContain(digest, parsed.ReferencedDigests);
    }

    [Theory]
    [InlineData("sha256:../other")]
    [InlineData("sha256:%2e%2e%2f")]
    [InlineData("sha256:abc")]
    [InlineData("sha512:abc")]
    [InlineData("sha999:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("sha256:gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    [InlineData("sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("other/repository:latest")]
    [InlineData("")]
    public void InvalidSubjectDigest_IsRejected(string digest) =>
        Assert.Throws<JsonException>(() => OciManifestParser.Parse("application/vnd.oci.image.manifest.v1+json", Manifest(digest)));

    private static byte[] Manifest(string digest) => JsonSerializer.SerializeToUtf8Bytes(new {
        schemaVersion = 2, mediaType = "application/vnd.oci.image.manifest.v1+json",
        subject = new { mediaType = "application/vnd.oci.image.manifest.v1+json", digest, size = 123 }
    });
}
