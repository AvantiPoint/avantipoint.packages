using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Protocol.Tests;

public sealed class NuGetAnonymousProviderCompatibilityTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnonymousPull_UsesTheExistingProviderDecision(bool allowAnonymous)
    {
        var provider = new ConsumerProvider(allowAnonymous);
        var adapter = new NuGetFeedAuthenticationAdapter(provider, Options.Create(new FeedOptions()));
        var request = Request(FeedOperation.Pull);

        var result = await adapter.AuthenticateAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(allowAnonymous, result.Succeeded);
        Assert.Equal(1, provider.ConsumerCalls);
    }

    [Theory]
    [InlineData("Basic invalid")]
    [InlineData("Bearer invalid")]
    public async Task MalformedCredentials_DoNotFallBackToAnonymous(string authorization)
    {
        var provider = new ConsumerProvider(true);
        var adapter = new NuGetFeedAuthenticationAdapter(provider, Options.Create(new FeedOptions()));
        var request = Request(FeedOperation.Pull);
        request.HttpContext.Request.Headers.Authorization = authorization;

        Assert.False((await adapter.AuthenticateAsync(request, TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(0, provider.ConsumerCalls);
    }

    [Fact]
    public async Task AnonymousWrite_IsNotGrantedByAnAnonymousConsumerProvider()
    {
        var provider = new ConsumerProvider(true);
        var adapter = new NuGetFeedAuthenticationAdapter(provider, Options.Create(new FeedOptions()));

        Assert.False((await adapter.AuthenticateAsync(Request(FeedOperation.Push), TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(0, provider.ConsumerCalls);
    }

    private static FeedAuthenticationRequest Request(FeedOperation operation) => new(
        new SurfaceContext("default", FeedProtocol.NuGet, "nuget", null, "/", new Uri("https://example.test/")),
        new DefaultHttpContext(), operation);

    private sealed class ConsumerProvider(bool allowAnonymous) : IPackageAuthenticationService
    {
        public int ConsumerCalls { get; private set; }
        public Task<NuGetAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken cancellationToken) =>
            Task.FromResult(NuGetAuthenticationResult.Fail("No publishing key", "test"));
        public Task<NuGetAuthenticationResult> AuthenticateAsync(string username, string token, CancellationToken cancellationToken)
        {
            ConsumerCalls++;
            Assert.Equal(string.Empty, username);
            Assert.Equal(string.Empty, token);
            return Task.FromResult(allowAnonymous ? NuGetAuthenticationResult.Success() : NuGetAuthenticationResult.Fail("Private feed", "test"));
        }
    }
}
