using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class OciSubjectOrderingHttpTests(OciTestWebApplicationFactory factory)
    : IClassFixture<OciTestWebApplicationFactory>
{
    private const string MediaType = "application/vnd.oci.image.manifest.v1+json";
    private static string Digest(byte[] bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    [Theory]
    [InlineData("/v2/", false)]
    [InlineData("/docker/v2/", false)]
    [InlineData("/v2/", true)]
    public async Task ReferrerBeforeSubject_IsAcceptedWithoutImportingAnotherRepository(string prefix, bool foreignSubject)
    {
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<FeedOptions>(options => options.Authentication.AllowAnonymousPull = false)));
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IContext>().RunMigrationsAsync(TestContext.Current.CancellationToken);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "integration-test-key");
        var repository = "team/referrers/app/" + Guid.NewGuid().ToString("N");
        var foreign = "other/" + Guid.NewGuid().ToString("N");
        var root = prefix + repository;
        var blob = Encoding.UTF8.GetBytes("{}");
        var blobDigest = Digest(blob);
        using var started = await client.PostAsync(root + "/blobs/uploads/", null);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        using var uploaded = await client.PutAsync(started.Headers.Location + "?digest=" + blobDigest, new ByteArrayContent(blob));
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        byte[] Manifest(object? subject) => JsonSerializer.SerializeToUtf8Bytes(new {
            schemaVersion = 2, mediaType = MediaType, artifactType = "application/example+json",
            config = new { mediaType = "application/vnd.oci.empty.v1+json", size = blob.Length, digest = blobDigest },
            layers = Array.Empty<object>(), subject });
        // Omit subject, rather than serializing a null descriptor.
        var subject = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 2, mediaType = MediaType,
            config = new { mediaType = "application/vnd.oci.image.config.v1+json", size = blob.Length, digest = blobDigest },
            layers = Array.Empty<object>() });
        var subjectDigest = Digest(subject);
        if (foreignSubject)
        {
            using var foreignPut = await Put(client, prefix + foreign, subjectDigest, subject);
            Assert.Equal(HttpStatusCode.Created, foreignPut.StatusCode);
        }
        var artifact = Manifest(new { mediaType = MediaType, size = subject.Length, digest = subjectDigest });
        var artifactDigest = Digest(artifact);
        // Unauthorized writes still cannot create an association.
        using var anonymous = app.CreateClient();
        using var denied = await Put(anonymous, root, artifactDigest, artifact);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var accepted = await Put(client, root, artifactDigest, artifact);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Equal(subjectDigest, Assert.Single(accepted.Headers.GetValues("OCI-Subject")));
        using var missing = await client.GetAsync(root + "/manifests/" + subjectDigest);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var referrers = await client.GetAsync(root + "/referrers/" + subjectDigest);
        Assert.Equal(HttpStatusCode.OK, referrers.StatusCode);
        using var index = JsonDocument.Parse(await referrers.Content.ReadAsStringAsync());
        Assert.Equal(artifactDigest, Assert.Single(index.RootElement.GetProperty("manifests").EnumerateArray()).GetProperty("digest").GetString());
        if (foreignSubject)
        {
            using var foreignReferrers = await client.GetAsync(prefix + foreign + "/referrers/" + subjectDigest);
            using var foreignIndex = JsonDocument.Parse(await foreignReferrers.Content.ReadAsStringAsync());
            Assert.Empty(foreignIndex.RootElement.GetProperty("manifests").EnumerateArray());
            using var foreignArtifact = await client.GetAsync(prefix + foreign + "/manifests/" + artifactDigest);
            Assert.Equal(HttpStatusCode.NotFound, foreignArtifact.StatusCode);
        }
        using var subjectPut = await Put(client, root, subjectDigest, subject);
        Assert.Equal(HttpStatusCode.Created, subjectPut.StatusCode);
        using var resolved = await client.GetAsync(root + "/manifests/" + subjectDigest);
        Assert.Equal(subject, await resolved.Content.ReadAsByteArrayAsync());
        // Retries deduplicate, and compatibility-index discovery remains scoped.
        using var retry = await Put(client, root, artifactDigest, artifact);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        using var after = await client.GetAsync(root + "/referrers/" + subjectDigest);
        using var afterIndex = JsonDocument.Parse(await after.Content.ReadAsStringAsync());
        Assert.Single(afterIndex.RootElement.GetProperty("manifests").EnumerateArray());
        // A valid weak subject does not exempt config/layer dependencies from validation.
        var missingBlob = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(artifact).Replace(blobDigest, "sha256:" + new string('f', 64)));
        using var rejectedBlob = await Put(client, root, Digest(missingBlob), missingBlob);
        Assert.Equal(HttpStatusCode.BadRequest, rejectedBlob.StatusCode);
    }

    private static Task<HttpResponseMessage> Put(HttpClient client, string root, string reference, byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(MediaType);
        return client.PutAsync(root + "/manifests/" + reference, content);
    }
}
