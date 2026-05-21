using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AvantiPoint.Packages.Host.Admin.Data;

public interface IHostIdentityContext : IDisposable
{
    DatabaseFacade Database { get; }

    DbSet<HostUser> HostUsers { get; set; }

    DbSet<HostApiToken> HostApiTokens { get; set; }

    DbSet<HostTokenNotification> HostTokenNotifications { get; set; }

    DbSet<HostAccessSettings> HostAccessSettings { get; set; }

    DbSet<HostPublishTarget> HostPublishTargets { get; set; }

    DbSet<HostPackageGroup> HostPackageGroups { get; set; }

    DbSet<HostPackageGroupMember> HostPackageGroupMembers { get; set; }

    DbSet<HostPackageGroupSyndication> HostPackageGroupSyndications { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
