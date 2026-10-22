using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Authorization;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Entities;

namespace AvantiPoint.Packages.Host.Admin.Tests.Authorization;

public sealed class HostRoleAssignmentPolicyTests
{
    [Fact]
    public void DedicatedPublisherGroup_IsExclusiveEvenForAdministrators()
    {
        var options = Options(adminGroups: ["admins"], publisherGroups: ["publishers"]);
        var user = new HostUser { IsAdmin = true, CanPublish = true };

        HostRoleAssignmentPolicy.ApplyMicrosoftRoles(user, Principal("admins"), options);

        Assert.True(user.IsAdmin);
        Assert.False(user.CanPublish);
        Assert.True(user.CanConsume);
    }

    [Fact]
    public void DedicatedPublisherGroup_CanGrantPublishWithoutAdmin()
    {
        var options = Options(adminGroups: ["admins"], publisherGroups: ["publishers"]);
        var user = new HostUser();

        HostRoleAssignmentPolicy.ApplyMicrosoftRoles(user, Principal("publishers"), options);

        Assert.False(user.IsAdmin);
        Assert.True(user.CanPublish);
        Assert.True(user.CanConsume);
    }

    [Fact]
    public void AdminGroup_ControlsPublishingWhenNoPublisherGroupExists()
    {
        var options = Options(adminGroups: ["admins"]);
        var formerAdmin = new HostUser { IsAdmin = true, CanPublish = true };
        var currentAdmin = new HostUser();

        HostRoleAssignmentPolicy.ApplyMicrosoftRoles(formerAdmin, Principal(), options);
        HostRoleAssignmentPolicy.ApplyMicrosoftRoles(currentAdmin, Principal("admins"), options);

        Assert.False(formerAdmin.IsAdmin);
        Assert.False(formerAdmin.CanPublish);
        Assert.True(currentAdmin.IsAdmin);
        Assert.True(currentAdmin.CanPublish);
    }

    [Fact]
    public void NoRoleGroups_PreservesLocallyAssignedRoles()
    {
        var user = new HostUser { CanPublish = true, CanConsume = false };

        HostRoleAssignmentPolicy.ApplyMicrosoftRoles(user, Principal(), Options());

        Assert.False(user.IsAdmin);
        Assert.True(user.CanPublish);
        Assert.True(user.CanConsume);
    }

    [Fact]
    public void ConsumerGroup_IsAuthoritativeForUsersWithoutHigherRoles()
    {
        var options = Options(consumerGroups: ["consumers"]);
        var formerConsumer = new HostUser { CanConsume = true };
        var currentConsumer = new HostUser();

        HostRoleAssignmentPolicy.ApplyMicrosoftRoles(formerConsumer, Principal(), options);
        HostRoleAssignmentPolicy.ApplyMicrosoftRoles(currentConsumer, Principal("consumers"), options);

        Assert.False(formerConsumer.CanConsume);
        Assert.True(currentConsumer.CanConsume);
    }

    private static HostAuthenticationOptions Options(
        string[]? adminGroups = null,
        string[]? publisherGroups = null,
        string[]? consumerGroups = null) =>
        new()
        {
            Microsoft = new HostMicrosoftOptions
            {
                AdminRoleGroupIds = adminGroups?.ToList() ?? [],
                PublisherRoleGroupIds = publisherGroups?.ToList() ?? [],
                ConsumerRoleGroupIds = consumerGroups?.ToList() ?? [],
            },
        };

    private static ClaimsPrincipal Principal(params string[] groups)
    {
        var claims = groups.Select(group => new Claim("groups", group));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
