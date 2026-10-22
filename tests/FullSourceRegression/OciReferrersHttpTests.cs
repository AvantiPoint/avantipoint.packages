using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

// Run in the complete preserved source tree, with this branch's OCI component and adapter.
// The component-only recovery branch deliberately does not contain that complete application.
public sealed class OciReferrersHttpTests : IClassFixture<OciTestWebApplicationFactory>
{
    private readonly OciTestWebApplicationFactory _factory;
    public OciReferrersHttpTests(OciTestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/v2/", "docker/appportal-devspace")]
    [InlineData("/docker/v2/", "team/product/appportal-devspace")]
    [InlineData("/v2/", "team/referrers/app")]
    [InlineData("/v2/", "team/manifests/app")]
    [InlineData("/v2/", "team/blobs/uploads/app")]
    public async Task PrivateReferrers_ChallengeRetryAndEmptyFilteredIndex(string prefix, string repository)
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<FeedOptions>(options => options.Authentication.AllowAnonymousPull = false)));
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IContext>().RunMigrationsAsync(TestContext.Current.CancellationToken);
        using var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        repository += "/" + Guid.NewGuid().ToString("N");
        var basePath = prefix + repository;
        // Create an existing repository, without adding any subject/referrer manifests.
        using var seed = new HttpRequestMessage(HttpMethod.Post, basePath + "/blobs/uploads/");
        seed.Headers.Authorization = new("Bearer", "integration-test-key");
        using var seeded = await client.SendAsync(seed, ct);
        Assert.Equal(HttpStatusCode.Accepted, seeded.StatusCode);

        var path = basePath + "/referrers/sha256:" + new string('a', 64) + "?artifactType=application%2Fspdx%2Bjson";
        using var denied = await client.GetAsync(path, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var challenge = denied.Headers.WwwAuthenticate.ToString();
        Assert.Contains($"scope=\"repository:{repository}:pull\"", challenge);
        var realm = Regex.Match(challenge, "realm=\"([^\"]+)\"").Groups[1].Value;
        var service = Regex.Match(challenge, "service=\"([^\"]+)\"").Groups[1].Value;
        var grant = Regex.Match(challenge, "scope=\"([^\"]+)\"").Groups[1].Value;
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Get,
            realm + "?service=" + Uri.EscapeDataString(service) + "&scope=" + Uri.EscapeDataString(grant));
        tokenRequest.Headers.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("user:integration-test-key")));
        using var tokenResponse = await client.SendAsync(tokenRequest, ct);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        using var json = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(ct));
        using var retry = new HttpRequestMessage(HttpMethod.Get, path);
        retry.Headers.Authorization = new("Bearer", json.RootElement.GetProperty("token").GetString());
        using var authorized = await client.SendAsync(retry, ct);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        Assert.Equal(OciReferrersIndex.MediaType, authorized.Content.Headers.ContentType?.MediaType);
        Assert.Equal("artifactType", Assert.Single(authorized.Headers.GetValues("OCI-Filters-Applied")));
        using var index = JsonDocument.Parse(await authorized.Content.ReadAsStringAsync(ct));
        Assert.Equal(2, index.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Empty(index.RootElement.GetProperty("manifests").EnumerateArray());

        using var invalid = new HttpRequestMessage(HttpMethod.Get, path);
        invalid.Headers.Authorization = new("Bearer", "invalid-synthetic-key");
        using var invalidResponse = await client.SendAsync(invalid, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
        using var wrongRepository = new HttpRequestMessage(HttpMethod.Get, prefix + "other/referrers/sha256:" + new string('a',64));
        wrongRepository.Headers.Authorization = retry.Headers.Authorization;
        using var wrongResponse = await client.SendAsync(wrongRepository, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongResponse.StatusCode);
    }
}
