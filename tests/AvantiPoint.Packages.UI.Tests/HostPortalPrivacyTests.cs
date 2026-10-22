using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TestContext = Xunit.TestContext;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class HostPortalPrivacyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnonymousAccountPages_RenderOnlyBrandingAndSignIn(bool allowAnonymousPull)
    {
        await using var factory = new PortalFactory(allowAnonymousPull);
        using var client = factory.Client();
        foreach (var path in new[] { "/Account/Login", "/Account/AccessDenied" })
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);
            foreach (var script in document.QuerySelectorAll("script, style")) script.Remove();
            var text = document.Body!.TextContent;
            Assert.Contains("Private Portal Test", html);
            Assert.Contains("Sign in", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Feed ecosystem", html);
            Assert.DoesNotContain("NuGet", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("npm", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Docker", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("private-package-sentinel", html);
            Assert.DoesNotContain("href=\"/browse", html);
            Assert.DoesNotContain("<!--Blazor:", html);
        }
        Assert.Equal(0, factory.Search.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnonymousPagesAndInteractiveData_AreChallengedBeforeReadingTheCatalog(bool allowAnonymousPull)
    {
        await using var factory = new PortalFactory(allowAnonymousPull);
        using var client = factory.Client();
        var paths = new[]
        {
            "/", "/?q=private-package-sentinel", "/browse", "/feed",
            "/packages/private-package-sentinel/1.0.0", "/browse/npm", "/browse/npm/connect",
            "/browse/npm/packages/@private/sentinel", "/oci", "/oci/docker", "/oci/connect/docker",
            "/oci/repos/private-repository-sentinel?reference=latest", "/oci/docker/repos/private-repository-sentinel",
        };
        foreach (var path in paths)
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("https://localhost/Account/Login", response.Headers.Location?.ToString());
            Assert.DoesNotContain("private-package-sentinel", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        using var negotiate = await client.PostAsync("/_blazor/negotiate?negotiateVersion=1", null, TestContext.Current.CancellationToken);
        Assert.Contains(negotiate.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Redirect });
        Assert.Equal(0, factory.Search.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthenticatedConsumer_CanBrowse_AndSubsequentAnonymousRequestsAreChallenged(bool allowAnonymousPull)
    {
        await using var factory = new PortalFactory(allowAnonymousPull);
        using var authenticated = factory.Client();
        authenticated.DefaultRequestHeaders.Add("X-Portal-Test-Role", FeedRoles.Consumer);
        using var response = await authenticated.GetAsync("/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("private-package-sentinel", html);
        Assert.Contains("NuGet", html);
        Assert.Contains("npm", html);
        Assert.Contains("Docker", html);
        Assert.Equal(1, factory.Search.Calls);

        using var anonymous = factory.Client();
        using var second = await anonymous.GetAsync("/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.DoesNotContain("private-package-sentinel", await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, factory.Search.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtocolBootstrapAndAnonymousPullConfiguration_ArePreserved(bool allowAnonymousPull)
    {
        await using var factory = new PortalFactory(allowAnonymousPull);
        using var client = factory.Client();
        using var index = await client.GetAsync("/v3/index.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        using var search = await client.GetAsync("/v3/search?q=sentinel", TestContext.Current.CancellationToken);
        Assert.Equal(allowAnonymousPull ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, search.StatusCode);
        if (!allowAnonymousPull) Assert.NotEmpty(search.Headers.WwwAuthenticate);
        using var npm = await client.GetAsync("/npm/-/v1/search?text=sentinel", TestContext.Current.CancellationToken);
        Assert.Equal(allowAnonymousPull ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, npm.StatusCode);
        using var oci = await client.GetAsync("/v2/", TestContext.Current.CancellationToken);
        Assert.Equal(allowAnonymousPull ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, oci.StatusCode);
        if (!allowAnonymousPull) Assert.Contains("Bearer", oci.Headers.WwwAuthenticate.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MountedPortal_KeepsAnonymousLoginInsideThePublicPrefix(bool allowAnonymousPull)
    {
        await using var factory = new PortalFactory(allowAnonymousPull, "/portal");
        using var client = factory.Client();
        using var response = await client.GetAsync("/portal/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://localhost/portal/Account/Login", response.Headers.Location?.ToString());
        using var login = await client.GetAsync("/portal/Account/Login", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var html = await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Private Portal Test", html);
        Assert.DoesNotContain("<!--Blazor:", html);
        Assert.DoesNotContain("href=\"/portal/browse", html);
        Assert.Equal(0, factory.Search.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnonymousMonitoring_PreservesGenericReadinessWithoutFeedDetails(bool allowAnonymousPull)
    {
        await using var factory = new PortalFactory(allowAnonymousPull);
        using var client = factory.Client();
        foreach (var path in new[] { "/health/feeds", "/health/feeds/default", "/health/feeds/not-a-feed" })
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Single(json.RootElement.EnumerateObject());
            Assert.Equal("healthy", json.RootElement.GetProperty("status").GetString());
        }
        using var metrics = await client.GetAsync("/metrics", TestContext.Current.CancellationToken);
        Assert.Contains(metrics.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Redirect });
        Assert.DoesNotContain("feed_", await metrics.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        client.DefaultRequestHeaders.Add("X-Portal-Test-Role", FeedRoles.Consumer);
        using var detailed = await client.GetAsync("/health/feeds", TestContext.Current.CancellationToken);
        var body = await detailed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("feedId", body);
        Assert.Contains("NuGet", body);
        using var known = await client.GetAsync("/health/feeds/default", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        using var unknown = await client.GetAsync("/health/feeds/not-a-feed", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var authorizedMetrics = await client.GetAsync("/metrics", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, authorizedMetrics.StatusCode);
        Assert.Contains("feed_push_total", await authorizedMetrics.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnonymousUnhealthyFeed_ReturnsOnlyGenericFailureWithReadinessStatus()
    {
        await using var factory = new PortalFactory(false, unhealthy: true);
        using var client = factory.Client();
        using var response = await client.GetAsync("/health/feeds/not-a-feed", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Single(json.RootElement.EnumerateObject());
        Assert.Equal("unhealthy", json.RootElement.GetProperty("status").GetString());
    }

    private sealed class PortalFactory(bool allowAnonymousPull, string pathBase = "", bool unhealthy = false) : WebApplicationFactory<InProcessNuGetSearchService>
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), $"portal-privacy-{Guid.NewGuid():N}");
        public RecordingSearchService Search { get; } = new();
        public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(directory);
            builder.UseEnvironment("Development");
            var settings = new Dictionary<string, string?>
            {
                ["Host:ServerName"] = "Private Portal Test",
                ["Host:PathBase"] = pathBase,
                ["Host:DemoData:Enabled"] = "false",
                ["Host:HttpsRedirection:Enabled"] = "false",
                ["Host:TokenHashPepper"] = "synthetic-portal-test-pepper-only-123456789",
                ["Host:DataProtection:KeyPath"] = Path.Combine(directory, "keys"),
                ["Database:Type"] = "Sqlite",
                ["Database:ConnectionString"] = $"Data Source={Path.Combine(directory, "packages.db")}",
                ["Storage:Type"] = "FileSystem",
                ["Storage:Path"] = Path.Combine(directory, "storage"),
                ["Feed:Authentication:ApiKey"] = "synthetic-portal-test-package-key-only-123456789",
                ["Feed:Authentication:AllowAnonymousPull"] = allowAnonymousPull.ToString(),
                ["Feed:Npm:Enabled"] = "true",
                ["Feed:Oci:Default:Enabled"] = "true",
                ["Feed:Oci:Docker:Enabled"] = "true",
                ["Feed:Oci:Docker:DisplayName"] = "Docker",
            };
            foreach (var setting in settings) builder.UseSetting(setting.Key, setting.Value);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
            builder.ConfigureTestServices(services =>
            {
                if (unhealthy) services.AddHealthChecks().AddCheck("private-health-sentinel", () =>
                    Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("private-check-description"));
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddSingleton<INuGetSearchService>(Search);
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "PortalTest";
                    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("PortalTest", _ => { });
            });
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Portal-Test-Role", out var role)) return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Synthetic Consumer"), new Claim(ClaimTypes.Role, role.ToString())], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed class RecordingSearchService : INuGetSearchService
    {
        public int Calls { get; private set; }
        public Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new SearchResponse { TotalHits = 1, Data = [new SearchResult { PackageId = "private-package-sentinel", Version = "1.0.0", Description = "Private catalog sentinel", Authors = ["Synthetic"] }] });
        }
        public Task<SearchResponse> SearchAsync(string? query = null, int skip = 0, int take = 20, bool includePrerelease = false,
            string? packageType = null, string? framework = null, CancellationToken cancellationToken = default) => SearchAsync(new SearchRequest { Query = query ?? "" }, cancellationToken);
        public Task<IReadOnlyList<string>> AutocompleteAsync(string? query = null, int skip = 0, int take = 10,
            bool includePrerelease = false, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
