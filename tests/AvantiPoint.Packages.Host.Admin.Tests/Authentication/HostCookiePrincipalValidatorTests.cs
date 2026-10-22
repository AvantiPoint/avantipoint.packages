using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Database.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Host.Admin.Tests.Authentication;

public sealed class HostCookiePrincipalValidatorTests
{
    [Fact]
    public async Task RestoredAccount_RejectsCookieIssuedBeforeRevocation()
    {
        await using var fixture = await CookieFixture.CreateAsync(new HostUser
        {
            Email = "restored@example.test", SessionVersion = 2, CanConsume = true,
            ApprovalStatus = HostUserApprovalStatus.Approved,
        });
        var validation = fixture.CreateValidationContext(Principal("restored@example.test", FeedRoles.Consumer));
        await HostCookiePrincipalValidator.ValidateAsync(validation);
        Assert.Null(validation.Principal);
        Assert.True(fixture.AuthenticationService.SignedOut);
    }

    [Fact]
    public async Task ValidateAsync_ReplacesStaleHostRolesFromDatabase()
    {
        await using var fixture = await CookieFixture.CreateAsync(new HostUser
        {
            Email = "publisher@example.test",
            Name = "Publisher",
            IsAdmin = false,
            CanPublish = true,
            CanConsume = true,
            ApprovalStatus = HostUserApprovalStatus.Approved,
        });
        var principal = Principal(
            "publisher@example.test",
            FeedRoles.Admin,
            FeedRoles.Consumer);
        var validationContext = fixture.CreateValidationContext(principal);

        await HostCookiePrincipalValidator.ValidateAsync(validationContext);

        Assert.True(validationContext.ShouldRenew);
        Assert.False(principal.IsInRole(FeedRoles.Admin));
        Assert.True(principal.IsInRole(FeedRoles.Publisher));
        Assert.True(principal.IsInRole(FeedRoles.Consumer));
    }

    [Fact]
    public async Task ValidateAsync_RejectsRevokedUserAndSignsOutCookie()
    {
        await using var fixture = await CookieFixture.CreateAsync(new HostUser
        {
            Email = "revoked@example.test",
            Name = "Revoked",
            IsAdmin = true,
            CanPublish = true,
            CanConsume = true,
            IsRevoked = true,
            ApprovalStatus = HostUserApprovalStatus.Approved,
        });
        var principal = Principal("revoked@example.test", FeedRoles.Admin);
        var validationContext = fixture.CreateValidationContext(principal);

        await HostCookiePrincipalValidator.ValidateAsync(validationContext);

        Assert.Null(validationContext.Principal);
        Assert.True(fixture.AuthenticationService.SignedOut);
    }

    private static ClaimsPrincipal Principal(string email, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, email) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private sealed class CookieFixture : IAsyncDisposable
    {
        private CookieFixture(
            SqliteConnection connection,
            HostSqliteContext context,
            ServiceProvider services,
            RecordingAuthenticationService authenticationService)
        {
            Connection = connection;
            Context = context;
            Services = services;
            AuthenticationService = authenticationService;
        }

        public SqliteConnection Connection { get; }

        public HostSqliteContext Context { get; }

        public ServiceProvider Services { get; }

        public RecordingAuthenticationService AuthenticationService { get; }

        public static async Task<CookieFixture> CreateAsync(HostUser user)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var context = new HostSqliteContext(
                new DbContextOptionsBuilder<HostSqliteContext>()
                    .UseSqlite(connection)
                    .Options);
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            context.HostUsers.Add(user);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var authenticationService = new RecordingAuthenticationService();
            var services = new ServiceCollection()
                .AddSingleton<IHostIdentityContext>(context)
                .AddSingleton<IAuthenticationService>(authenticationService)
                .BuildServiceProvider();
            return new CookieFixture(connection, context, services, authenticationService);
        }

        public CookieValidatePrincipalContext CreateValidationContext(ClaimsPrincipal principal)
        {
            var scheme = new AuthenticationScheme(
                CookieAuthenticationDefaults.AuthenticationScheme,
                displayName: null,
                typeof(CookieAuthenticationHandler));
            var httpContext = new DefaultHttpContext { RequestServices = Services };
            var ticket = new AuthenticationTicket(principal, scheme.Name);
            return new CookieValidatePrincipalContext(
                httpContext,
                scheme,
                new CookieAuthenticationOptions(),
                ticket);
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public bool SignedOut { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            SignedOut = true;
            return Task.CompletedTask;
        }
    }
}
