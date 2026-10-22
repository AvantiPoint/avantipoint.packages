using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using AvantiPoint.Packages.Host.Database.Sqlite;
using AvantiPoint.Packages.Host.Pages.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AvantiPoint.Packages.Host.Admin.Tests.Pages;

public class PackageGroupPickerTests
{
    [Theory]
    [InlineData("NuGet")]
    [InlineData("Npm")]
    [InlineData("Docker")]
    [InlineData("Helm")]
    [InlineData("Invalid")]
    public async Task CannotAddUnhostedPackage(string ecosystem)
    {
        await using var identityConnection = new SqliteConnection("DataSource=:memory:");
        await identityConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var packageConnection = new SqliteConnection("DataSource=:memory:");
        await packageConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var identity = new HostSqliteContext(new DbContextOptionsBuilder<HostSqliteContext>().UseSqlite(identityConnection).Options);
        await using var packages = new SqliteContext(new DbContextOptionsBuilder<SqliteContext>().UseSqlite(packageConnection).Options);
        await identity.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await packages.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        identity.HostPackageGroups.Add(new HostPackageGroup { Name = "Release" });
        await identity.SaveChangesAsync(TestContext.Current.CancellationToken);
        var page = new PackageGroupsModel(identity, Mock.Of<ISyndicationService>(), NullLogger<PackageGroupsModel>.Instance, packages);

        Assert.IsType<RedirectToPageResult>(await page.OnPostAddMemberAsync("Release", "missing", ecosystem));
        Assert.True(page.StatusIsError);
        Assert.Equal("Release", page.ActiveGroup);
        Assert.Empty(await identity.HostPackageGroupMembers.ToListAsync(TestContext.Current.CancellationToken));
    }
}
