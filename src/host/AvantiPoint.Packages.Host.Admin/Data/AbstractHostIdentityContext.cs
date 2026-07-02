using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Admin.Data;

public abstract class AbstractHostIdentityContext : DbContext, IHostIdentityContext
{
    protected AbstractHostIdentityContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<HostUser> HostUsers { get; set; } = null!;

    public DbSet<HostApiToken> HostApiTokens { get; set; } = null!;

    public DbSet<HostTokenNotification> HostTokenNotifications { get; set; } = null!;

    public DbSet<HostAccessSettings> HostAccessSettings { get; set; } = null!;

    public DbSet<HostPublishTarget> HostPublishTargets { get; set; } = null!;

    public DbSet<HostPackageGroup> HostPackageGroups { get; set; } = null!;

    public DbSet<HostPackageGroupMember> HostPackageGroupMembers { get; set; } = null!;

    public DbSet<HostPackageGroupSyndication> HostPackageGroupSyndications { get; set; } = null!;

    public DbSet<HostAuditEvent> HostAuditEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HostUser>(e =>
        {
            e.ToTable("HostUsers");
            e.HasKey(x => x.Email);
        });

        modelBuilder.Entity<HostApiToken>(e =>
        {
            e.ToTable("HostApiTokens");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.TokenPrefix);
            e.Property(x => x.Scopes).HasConversion<int>();
            e.HasOne(x => x.User)
                .WithMany(x => x.Tokens)
                .HasForeignKey(x => x.UserEmail)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HostTokenNotification>(e =>
        {
            e.ToTable("HostTokenNotifications");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Token)
                .WithMany()
                .HasForeignKey(x => x.HostApiTokenId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HostAccessSettings>(e =>
        {
            e.ToTable("HostAccessSettings");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<HostPublishTarget>(e =>
        {
            e.ToTable("HostPublishTargets");
            e.HasKey(x => x.Name);
            e.Property(x => x.Protocol)
                .HasConversion<string>()
                .HasDefaultValue(Entities.PublishTargetProtocol.NuGet);
        });

        modelBuilder.Entity<HostAuditEvent>(e =>
        {
            e.ToTable("HostAuditEvents");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.EventType);
            e.Property(x => x.Actor).HasMaxLength(256);
            e.Property(x => x.EventType).HasMaxLength(128);
            e.Property(x => x.Subject).HasMaxLength(512);
        });

        modelBuilder.Entity<HostPackageGroup>(e =>
        {
            e.ToTable("HostPackageGroups");
            e.HasKey(x => x.Name);
        });

        modelBuilder.Entity<HostPackageGroupMember>(e =>
        {
            e.ToTable("HostPackageGroupMembers");
            e.HasKey(x => new { x.PackageGroupName, x.PackageId });
            e.HasOne(x => x.PackageGroup)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.PackageGroupName);
        });

        modelBuilder.Entity<HostPackageGroupSyndication>(e =>
        {
            e.ToTable("HostPackageGroupSyndications");
            e.HasKey(x => new { x.PackageGroupName, x.PublishTargetName });
            e.HasOne(x => x.PackageGroup)
                .WithMany(x => x.Syndications)
                .HasForeignKey(x => x.PackageGroupName);
            e.HasOne(x => x.PublishTarget)
                .WithMany(x => x.Syndications)
                .HasForeignKey(x => x.PublishTargetName);
        });
    }
}
