using System.Reflection;
using System.Security.Claims;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Tokens;
using AvantiPoint.Packages.Host.Database.Sqlite;
using AvantiPoint.Packages.Host.Pages.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace AvantiPoint.Packages.Host.Admin.Tests.Pages;

public sealed class ApiKeysModelTests
{
    [Fact]
    public async Task SuccessfulCreationRedirects_AndSubsequentGetsCannotMintAnotherToken()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new HostSqliteContext(new DbContextOptionsBuilder<HostSqliteContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        const string email = "api-key-refresh@example.test";
        db.HostUsers.Add(new HostUser { Email = email, CanConsume = true, CanPublish = true });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var registry = new FeedRegistry(new FeedContext("test", "Test", ""));
        registry.Register(new SurfaceRegistration("nuget", FeedProtocol.NuGet, null, "", ""));
        var hasher = new HostTokenHasher(Options.Create(new HostSettings { TokenHashPepper = "isolated-unit-test" }));
        var service = new HostApiTokenService(db, hasher, registry);
        var urls = new Mock<IPublicBaseUrlProvider>();
        urls.Setup(provider => provider.GetSurfacePublicBaseUrl(It.IsAny<HttpContext>(), It.IsAny<string>()))
            .Returns(new Uri("https://local.example.test"));
        ApiKeysModel Model() => new(db, service, registry, urls.Object)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, FeedRoles.Consumer)], "test")),
                },
            },
            NewDescription = "Refresh regression", NewFeedIds = ["nuget"], NewScopeRead = true,
        };
        var post = Model();
        Assert.IsType<RedirectToPageResult>(await post.OnPostCreateAsync());
        Assert.False(string.IsNullOrEmpty(post.LastCreatedToken));
        for (var refresh = 0; refresh < 3; refresh++)
        {
            var get = Model();
            await get.OnGetAsync();
            Assert.Null(get.LastCreatedToken);
            Assert.Single(get.Tokens);
        }
        Assert.Single(await db.HostApiTokens.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void OneTimeSecretUsesProtectedTempData_AndResponsesAreNotCacheable()
    {
        Assert.NotNull(typeof(ApiKeysModel).GetProperty(nameof(ApiKeysModel.LastCreatedToken))!.GetCustomAttribute<TempDataAttribute>());
        var cache = typeof(ApiKeysModel).GetCustomAttribute<ResponseCacheAttribute>();
        Assert.NotNull(cache);
        Assert.True(cache.NoStore);
        Assert.Equal(ResponseCacheLocation.None, cache.Location);
    }
}
