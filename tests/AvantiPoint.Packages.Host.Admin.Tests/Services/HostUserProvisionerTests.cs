using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using AvantiPoint.Packages.Host.Admin.Services.Tokens;
using AvantiPoint.Packages.Host.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Tests.Services;

public sealed class HostUserProvisionerTests
{
    [Fact]
    public async Task RevokedOnlyAdministrator_DoesNotReopenFirstUserBootstrap()
    {
        await using var fixture = await ProvisionerFixture.CreateAsync();
        fixture.Context.HostUsers.Add(new HostUser
        {
            Email = "original@example.test",
            IsAdmin = true,
            IsRevoked = true,
        });
        await fixture.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var user = await fixture.Provisioner.ProvisionUserAsync(Principal("new@example.test"),
            HostExternalAuthProvider.MicrosoftAccount, TestContext.Current.CancellationToken);

        Assert.False(user.IsAdmin);
        Assert.False(user.CanPublish);
    }

    [Fact]
    public async Task ConcurrentFirstSignIns_CreateOnlyOneAdministrator()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"host-bootstrap-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var options = new DbContextOptionsBuilder<HostSqliteContext>()
                .UseSqlite($"Data Source={Path.Combine(directory, "identity.db")};Pooling=False")
                .Options;
            await using (var schema = new HostSqliteContext(options))
            {
                await schema.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            }
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var started = 0;
            var attempts = Enumerable.Range(0, 2).Select(i => Task.Run(async () =>
            {
                await using var context = new HostSqliteContext(options);
                var provisioner = new HostUserProvisioner(context,
                    new HostTokenHasher(Options.Create(new HostSettings { TokenHashPepper = "test-pepper" })),
                    Options.Create(new HostAccessOptions()), Options.Create(new HostAuthenticationOptions()));
                if (Interlocked.Increment(ref started) == 2) ready.TrySetResult();
                await ready.Task;
                return await provisioner.ProvisionUserAsync(Principal($"user-{i}@example.test"),
                    HostExternalAuthProvider.MicrosoftAccount, TestContext.Current.CancellationToken);
            })).ToArray();

            var users = await Task.WhenAll(attempts);
            Assert.Single(users, user => user.IsAdmin);
            await using var verification = new HostSqliteContext(options);
            Assert.Equal(1, await verification.HostUsers.CountAsync(user => user.IsAdmin, TestContext.Current.CancellationToken));
            Assert.Equal(2, await verification.HostUsers.CountAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FirstUser_BecomesLocalAdminWhenNoAdminGroupIsConfigured()
    {
        await using var fixture = await ProvisionerFixture.CreateAsync();

        var user = await fixture.Provisioner.ProvisionUserAsync(
            Principal("first@example.test"),
            HostExternalAuthProvider.MicrosoftAccount,
            TestContext.Current.CancellationToken);

        Assert.True(user.IsAdmin);
        Assert.True(user.CanPublish);
        Assert.True(user.CanConsume);
        Assert.Equal(HostUserApprovalStatus.Approved, user.ApprovalStatus);
    }

    [Fact]
    public async Task FirstUser_IsNotBootstrappedWhenAdminGroupIsConfigured()
    {
        await using var fixture = await ProvisionerFixture.CreateAsync(
            requireApproval: false,
            adminGroups: ["admins"]);

        var user = await fixture.Provisioner.ProvisionUserAsync(
            Principal("member@example.test"),
            HostExternalAuthProvider.MicrosoftAccount,
            TestContext.Current.CancellationToken);

        Assert.False(user.IsAdmin);
        Assert.False(user.CanPublish);
        Assert.True(user.CanConsume);
        Assert.Equal(HostUserApprovalStatus.Approved, user.ApprovalStatus);
    }

    [Fact]
    public async Task EntraAdmin_IsApprovedEvenWhenNewUsersNormallyRequireApproval()
    {
        await using var fixture = await ProvisionerFixture.CreateAsync(
            requireApproval: true,
            adminGroups: ["admins"]);

        var user = await fixture.Provisioner.ProvisionUserAsync(
            Principal("admin@example.test", "admins"),
            HostExternalAuthProvider.MicrosoftAccount,
            TestContext.Current.CancellationToken);

        Assert.True(user.IsAdmin);
        Assert.True(user.CanPublish);
        Assert.True(user.CanConsume);
        Assert.Equal(HostUserApprovalStatus.Approved, user.ApprovalStatus);
    }

    [Fact]
    public async Task DedicatedPublisherGroup_DoesNotLetBootstrapAdminPublish()
    {
        await using var fixture = await ProvisionerFixture.CreateAsync(
            publisherGroups: ["publishers"]);

        var user = await fixture.Provisioner.ProvisionUserAsync(
            Principal("admin@example.test"),
            HostExternalAuthProvider.MicrosoftAccount,
            TestContext.Current.CancellationToken);

        Assert.True(user.IsAdmin);
        Assert.False(user.CanPublish);
        Assert.True(user.CanConsume);
    }

    [Fact]
    public async Task ExistingMicrosoftUser_RolesAreReconciledAtSignIn()
    {
        await using var fixture = await ProvisionerFixture.CreateAsync(adminGroups: ["admins"]);
        fixture.Context.HostUsers.Add(new HostUser
        {
            Email = "former-admin@example.test",
            Name = "Former Admin",
            IsAdmin = true,
            CanPublish = true,
            CanConsume = true,
            ApprovalStatus = HostUserApprovalStatus.Approved,
            ExternalProvider = HostExternalAuthProvider.MicrosoftAccount,
        });
        await fixture.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var user = await fixture.Provisioner.ProvisionUserAsync(
            Principal("former-admin@example.test"),
            HostExternalAuthProvider.MicrosoftAccount,
            TestContext.Current.CancellationToken);

        Assert.False(user.IsAdmin);
        Assert.False(user.CanPublish);
        Assert.True(user.CanConsume);
    }

    private static ClaimsPrincipal Principal(string email, params string[] groups)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, email),
            new(ClaimTypes.NameIdentifier, $"subject-{email}"),
        };
        claims.AddRange(groups.Select(group => new Claim("groups", group)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private sealed class ProvisionerFixture : IAsyncDisposable
    {
        private ProvisionerFixture(
            SqliteConnection connection,
            HostSqliteContext context,
            HostUserProvisioner provisioner)
        {
            Connection = connection;
            Context = context;
            Provisioner = provisioner;
        }

        public SqliteConnection Connection { get; }

        public HostSqliteContext Context { get; }

        public HostUserProvisioner Provisioner { get; }

        public static async Task<ProvisionerFixture> CreateAsync(
            bool requireApproval = false,
            string[]? adminGroups = null,
            string[]? publisherGroups = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var context = new HostSqliteContext(
                new DbContextOptionsBuilder<HostSqliteContext>()
                    .UseSqlite(connection)
                    .Options);
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

            var hostSettings = Options.Create(new HostSettings
            {
                ServerName = "Test Packages",
                TokenHashPepper = "test-pepper",
            });
            var authentication = Options.Create(new HostAuthenticationOptions
            {
                Microsoft = new HostMicrosoftOptions
                {
                    AdminRoleGroupIds = adminGroups?.ToList() ?? [],
                    PublisherRoleGroupIds = publisherGroups?.ToList() ?? [],
                },
            });
            var provisioner = new HostUserProvisioner(
                context,
                new HostTokenHasher(hostSettings),
                Options.Create(new HostAccessOptions { RequireNewUserApproval = requireApproval }),
                authentication);
            return new ProvisionerFixture(connection, context, provisioner);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
