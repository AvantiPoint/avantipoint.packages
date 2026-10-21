using System.Reflection;
using System.Text;
using System.Text.Json;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Packages.Registry.Oci;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ComponentRegression.TestInfrastructure;

internal sealed class ScopedTokenTestHost : IDisposable
{
    internal static SurfaceContext DefaultSurface { get; } = new("default", FeedProtocol.Oci, "oci-docker", "docker", "/docker", new Uri("https://registry.invalid/docker"));
    private readonly ServiceProvider _services = new ServiceCollection().AddLogging()
        .AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()).BuildServiceProvider();
    internal SyntheticPackageAuthentication Authentication { get; } = new();
    internal DefaultHttpContext Context(SurfaceContext? surface = null)
    {
        Authentication.Surface = surface ?? DefaultSurface;
        var http = new DefaultHttpContext { RequestServices = _services };
        http.Response.Body = new MemoryStream();
        return http;
    }
    internal async Task<IssuedOciToken> Issue(string query, string credential = "consumer", SurfaceContext? surface = null, bool bearer = false, string username = "user@example.invalid")
    {
        var http = Context(surface);
        http.Request.QueryString = new QueryString(query);
        if (credential.Length > 0) http.Request.Headers.Authorization = bearer ? "Bearer " + credential
            : "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + credential));
        var accessor = new TestSurfaceAccessor { Current = surface ?? DefaultSurface };
        var method = typeof(OciTokenEndpoints).GetMethod("IssueTokenAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var result = await (Task<IResult>)method.Invoke(null, [http, accessor, Authentication, TestContext.Current.CancellationToken])!;
        await result.ExecuteAsync(http);
        if (http.Response.Body.Length == 0) return new(http.Response.StatusCode, "", "");
        http.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(http.Response.Body);
        return new(http.Response.StatusCode, json.RootElement.TryGetProperty("token", out var token) ? token.GetString()! : "",
            json.RootElement.TryGetProperty("scope", out var scope) ? scope.GetString()! : "");
    }
    internal async Task<bool> Authorize(string token, string path, FeedOperation operation, SurfaceContext? surface = null)
    {
        var http = Context(surface);
        http.Request.Path = path;
        http.Request.Method = "HEAD";
        var header = "Bearer " + token;
        http.Request.Headers.Authorization = header;
        var result = await OciAccessTokens.AuthenticateAsync(Authentication,
            new FeedAuthenticationRequest(surface ?? DefaultSurface, http, operation), TestContext.Current.CancellationToken);
        Assert.Equal(header, http.Request.Headers.Authorization.ToString());
        return result.Succeeded;
    }
    public void Dispose() => _services.Dispose();
}
