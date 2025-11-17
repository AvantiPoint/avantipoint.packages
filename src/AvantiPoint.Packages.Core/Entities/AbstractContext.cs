using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core.Entities.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvantiPoint.Packages.Core
{
    public abstract class AbstractContext : DbContext, IContext
    {
        public const int DefaultMaxStringLength = 4000;

        public const int MaxPackageIdLength = 128;
        public const int MaxPackageVersionLength = 64;
        public const int MaxPackageMinClientVersionLength = 44;
        public const int MaxPackageLanguageLength = 20;
        public const int MaxPackageTitleLength = 256;
        public const int MaxPackageTypeNameLength = 512;
        public const int MaxPackageTypeVersionLength = 64;
        public const int MaxRepositoryTypeLength = 100;
        public const int MaxTargetFrameworkLength = 256;
        public const int MaxClientNameLength = 64;
        public const int MaxClientPlatformLength = 64;
        public const int MaxClientVersionLength = 64;
        public const int MaxUserAgentLength = 256;

        public const int MaxPackageDependencyVersionRangeLength = 256;
        public const int MaxRepositoryCommitLength = 64;
        public const int MaxDeprecationMessageLength = 4000;

        protected AbstractContext(DbContextOptions options)
            : base(options)
        { }

        public DbSet<Package> Packages { get; set; }
        public DbSet<PackageDependency> PackageDependencies { get; set; }
        public DbSet<PackageDownload> PackageDownloads { get; set; }
        public DbSet<PackageType> PackageTypes { get; set; }
        public DbSet<TargetFramework> TargetFrameworks { get; set; }
        public DbSet<VulnerabilityRecord> VulnerabilityRecords { get; set; }
        public DbSet<PackageVulnerability> PackageVulnerabilities { get; set; }

        public Task<int> SaveChangesAsync() => SaveChangesAsync(default);

        public virtual async Task RunMigrationsAsync(CancellationToken cancellationToken)
            => await Database.MigrateAsync(cancellationToken);

        /// <summary>
        /// Finds packages by ID using optimized view with JSON aggregation.
        /// This implementation is for relational databases that support views.
        /// </summary>
        public virtual async Task<IReadOnlyList<Package>> FindPackagesAsync(
            string id,
            bool includeUnlisted,
            CancellationToken cancellationToken)
        {
            // Use the optimized view - single query with JSON aggregation done by database
            var viewQuery = Set<PackageWithJsonData>()
                .AsNoTracking()
                .Where(p => p.Id == id);

            if (!includeUnlisted)
            {
                viewQuery = viewQuery.Where(p => p.Listed);
            }

            var viewPackages = await viewQuery.ToListAsync(cancellationToken);
            
            // Convert view entities to Package entities with deserialized relationships
            return viewPackages.Select(ConvertFromView).ToList().AsReadOnly();
        }

        private static Package ConvertFromView(PackageWithJsonData view)
        {
            var package = new Package
            {
                Key = view.Key,
                Id = view.Id,
                NormalizedVersionString = view.NormalizedVersionString,
                OriginalVersionString = view.OriginalVersionString,
                Authors = view.Authors ?? [],
                Description = view.Description ?? string.Empty,
                Downloads = view.Downloads,
                HasReadme = view.HasReadme,
                HasEmbeddedIcon = view.HasEmbeddedIcon,
                HasEmbeddedLicense = view.HasEmbeddedLicense,
                IsPrerelease = view.IsPrerelease,
                ReleaseNotes = view.ReleaseNotes,
                Language = view.Language,
                Listed = view.Listed,
                LicenseExpression = view.LicenseExpression,
                IsSigned = view.IsSigned,
                IsTool = view.IsTool,
                IsDevelopmentDependency = view.IsDevelopmentDependency,
                MinClientVersion = view.MinClientVersion,
                Published = view.Published,
                RequireLicenseAcceptance = view.RequireLicenseAcceptance,
                SemVerLevel = view.SemVerLevel,
                Summary = view.Summary,
                Title = view.Title,
                IconUrl = view.IconUrl,
                LicenseUrl = view.LicenseUrl,
                ProjectUrl = view.ProjectUrl,
                RepositoryUrl = view.RepositoryUrl,
                RepositoryType = view.RepositoryType,
                RepositoryCommit = view.RepositoryCommit,
                RepositoryCommitDate = view.RepositoryCommitDate,
                Tags = view.Tags ?? [],
                IsDeprecated = view.IsDeprecated,
                DeprecationReasons = view.DeprecationReasons ?? [],
                DeprecationMessage = view.DeprecationMessage,
                DeprecatedAlternatePackageId = view.DeprecatedAlternatePackageId,
                DeprecatedAlternatePackageVersionRange = view.DeprecatedAlternatePackageVersionRange,
                RowVersion = view.RowVersion ?? [],
                TargetFrameworks = [],
                Dependencies = [],
                PackageTypes = []
            };

            // Deserialize JSON columns into entity collections
            if (!string.IsNullOrEmpty(view.DependenciesJson))
            {
                try
                {
                    var dependencies = JsonSerializer.Deserialize<List<PackageDependencyData>>(view.DependenciesJson);
                    if (dependencies != null)
                    {
                        foreach (var dep in dependencies)
                        {
                            package.Dependencies.Add(new PackageDependency
                            {
                                Id = dep.Id ?? string.Empty,
                                VersionRange = dep.VersionRange ?? string.Empty,
                                TargetFramework = dep.TargetFramework ?? string.Empty,
                                Package = package
                            });
                        }
                    }
                }
                catch (JsonException)
                {
                    // Ignore deserialization errors - leave dependencies empty
                }
            }

            if (!string.IsNullOrEmpty(view.PackageTypesJson))
            {
                try
                {
                    var packageTypes = JsonSerializer.Deserialize<List<PackageTypeData>>(view.PackageTypesJson);
                    if (packageTypes != null)
                    {
                        foreach (var pt in packageTypes)
                        {
                            package.PackageTypes.Add(new PackageType
                            {
                                Name = pt.Name ?? string.Empty,
                                Version = pt.Version ?? string.Empty,
                                Package = package
                            });
                        }
                    }
                }
                catch (JsonException)
                {
                    // Ignore deserialization errors - leave package types empty
                }
            }

            if (!string.IsNullOrEmpty(view.TargetFrameworksJson))
            {
                try
                {
                    var frameworks = JsonSerializer.Deserialize<List<TargetFrameworkData>>(view.TargetFrameworksJson);
                    if (frameworks != null)
                    {
                        foreach (var fw in frameworks)
                        {
                            package.TargetFrameworks.Add(new TargetFramework
                            {
                                Moniker = fw.Moniker ?? string.Empty,
                                Package = package
                            });
                        }
                    }
                }
                catch (JsonException)
                {
                    // Ignore deserialization errors - leave target frameworks empty
                }
            }

            return package;
        }

        public abstract bool IsUniqueConstraintViolationException(DbUpdateException exception);

        public virtual bool SupportsLimitInSubqueries => true;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Package>(BuildPackageEntity);
            builder.Entity<PackageDependency>(BuildPackageDependencyEntity);
            builder.Entity<PackageDownload>(BuildPackageDownloadEntity);
            builder.Entity<PackageType>(BuildPackageTypeEntity);
            builder.Entity<TargetFramework>(BuildTargetFrameworkEntity);
            builder.Entity<PackageWithJsonData>(BuildPackageWithJsonDataEntity);
            builder.Entity<VulnerabilityRecord>(BuildVulnerabilityRecordEntity);
            builder.Entity<PackageVulnerability>(BuildPackageVulnerabilityEntity);
        }

        private void BuildPackageEntity(EntityTypeBuilder<Package> package)
        {
            package.HasKey(p => p.Key);
            package.HasIndex(p => p.Id);
            package.HasIndex(p => new { p.Id, p.NormalizedVersionString })
                .IsUnique();

            package.Property(p => p.Id)
                .HasMaxLength(MaxPackageIdLength)
                .IsRequired();

            package.Property(p => p.NormalizedVersionString)
                .HasColumnName("Version")
                .HasMaxLength(MaxPackageVersionLength)
                .IsRequired();

            package.Property(p => p.OriginalVersionString)
                .HasColumnName("OriginalVersion")
                .HasMaxLength(MaxPackageVersionLength);

            package.Property(p => p.ReleaseNotes)
                .HasColumnName("ReleaseNotes")
                .HasMaxLength(DefaultMaxStringLength);

            package.Property(p => p.Authors)
                .HasMaxLength(DefaultMaxStringLength)
                .HasConversion(StringArrayToJsonConverter.Instance)
                .Metadata.SetValueComparer(StringArrayComparer.Instance);

            package.Property(p => p.IconUrl)
                .HasConversion(UriToStringConverter.Instance)
                .HasMaxLength(DefaultMaxStringLength);

            package.Property(p => p.LicenseUrl)
                .HasConversion(UriToStringConverter.Instance)
                .HasMaxLength(DefaultMaxStringLength);

            package.Property(p => p.ProjectUrl)
                .HasConversion(UriToStringConverter.Instance)
                .HasMaxLength(DefaultMaxStringLength);

            package.Property(p => p.RepositoryUrl)
                .HasConversion(UriToStringConverter.Instance)
                .HasMaxLength(DefaultMaxStringLength);

            package.Property(p => p.Tags)
                .HasMaxLength(DefaultMaxStringLength)
                .HasConversion(StringArrayToJsonConverter.Instance)
                .Metadata.SetValueComparer(StringArrayComparer.Instance);

            package.Property(p => p.DeprecationReasons)
                .HasMaxLength(DefaultMaxStringLength)
                .HasConversion(StringArrayToJsonConverter.Instance)
                .Metadata.SetValueComparer(StringArrayComparer.Instance);

            package.Property(p => p.Description).HasMaxLength(DefaultMaxStringLength);
            package.Property(p => p.Language).HasMaxLength(MaxPackageLanguageLength);
            package.Property(p => p.MinClientVersion).HasMaxLength(MaxPackageMinClientVersionLength);
            package.Property(p => p.Summary).HasMaxLength(DefaultMaxStringLength);
            package.Property(p => p.Title).HasMaxLength(MaxPackageTitleLength);
            package.Property(p => p.RepositoryType).HasMaxLength(MaxRepositoryTypeLength);
            package.Property(p => p.RepositoryCommit).HasMaxLength(MaxRepositoryCommitLength);
            package.Property(p => p.DeprecationMessage).HasMaxLength(MaxDeprecationMessageLength);
            package.Property(p => p.DeprecatedAlternatePackageId).HasMaxLength(MaxPackageIdLength);
            package.Property(p => p.DeprecatedAlternatePackageVersionRange).HasMaxLength(MaxPackageDependencyVersionRangeLength);

            package.Ignore(p => p.Version);
            package.Ignore(p => p.IconUrlString);
            package.Ignore(p => p.LicenseUrlString);
            package.Ignore(p => p.ProjectUrlString);
            package.Ignore(p => p.RepositoryUrlString);
            package.HasMany(p => p.Dependencies)
                .WithOne(d => d.Package)
                .IsRequired();

            package.HasMany(p => p.PackageTypes)
                .WithOne(d => d.Package)
                .IsRequired();

            package.HasMany(p => p.TargetFrameworks)
                .WithOne(d => d.Package)
                .IsRequired();

            package.Property(p => p.RowVersion).IsRowVersion();
        }

        private void BuildPackageDependencyEntity(EntityTypeBuilder<PackageDependency> dependency)
        {
            dependency.HasKey(d => d.Key);
            dependency.HasIndex(d => d.Id);

            dependency.Property(d => d.Id).HasMaxLength(MaxPackageIdLength);
            dependency.Property(d => d.VersionRange).HasMaxLength(MaxPackageDependencyVersionRangeLength);
            dependency.Property(d => d.TargetFramework).HasMaxLength(MaxTargetFrameworkLength);
        }

        private void BuildPackageDownloadEntity(EntityTypeBuilder<PackageDownload> download)
        {
            download.Property(x => x.Timestamp)
                .HasField("_timestamp");

            download.Property(x => x.ClientPlatform)
                .HasMaxLength(MaxClientPlatformLength);
            download.Property(x => x.ClientPlatformVersion)
                .HasMaxLength(MaxClientVersionLength);
            download.Property(x => x.NuGetClient)
                .HasMaxLength(MaxClientNameLength);
            download.Property(x => x.NuGetClientVersion)
                .HasMaxLength(MaxClientVersionLength);
            download.Property(x => x.UserAgentString)
                .HasMaxLength(MaxUserAgentLength);
        }

        private void BuildPackageTypeEntity(EntityTypeBuilder<PackageType> type)
        {
            type.HasKey(d => d.Key);
            type.HasIndex(d => d.Name);

            type.Property(d => d.Name).HasMaxLength(MaxPackageTypeNameLength);
            type.Property(d => d.Version).HasMaxLength(MaxPackageTypeVersionLength);
        }

        private void BuildTargetFrameworkEntity(EntityTypeBuilder<TargetFramework> targetFramework)
        {
            targetFramework.HasKey(f => f.Key);
            targetFramework.HasIndex(f => f.Moniker);

            targetFramework.Property(f => f.Moniker).HasMaxLength(MaxTargetFrameworkLength);
        }

        private void BuildPackageWithJsonDataEntity(EntityTypeBuilder<PackageWithJsonData> entity)
        {
            // This is a read-only view, mark it as such
            entity.ToView("vw_PackageWithJsonData");
            entity.HasKey(e => e.Key);

            // Configure the same mappings as Package for consistent behavior
            entity.Property(p => p.NormalizedVersionString)
                .HasColumnName("Version")
                .HasMaxLength(MaxPackageVersionLength)
                .IsRequired();

            entity.Property(p => p.OriginalVersionString)
                .HasColumnName("OriginalVersion")
                .HasMaxLength(MaxPackageVersionLength);

            entity.Property(p => p.Authors)
                .HasMaxLength(DefaultMaxStringLength)
                .HasConversion(StringArrayToJsonConverter.Instance)
                .Metadata.SetValueComparer(StringArrayComparer.Instance);

            entity.Property(p => p.IconUrl)
                .HasConversion(UriToStringConverter.Instance)
                .HasMaxLength(DefaultMaxStringLength);

            entity.Property(p => p.LicenseUrl)
                .HasConversion(UriToStringConverter.Instance)
                .HasMaxLength(DefaultMaxStringLength);

            entity.Property(p => p.ProjectUrl)
                .HasConversion(UriToStringConverter.Instance)
                .HasMaxLength(DefaultMaxStringLength);

            entity.Property(p => p.RepositoryUrl)
                .HasConversion(UriToStringConverter.Instance)
                .HasMaxLength(DefaultMaxStringLength);

            entity.Property(p => p.Tags)
                .HasMaxLength(DefaultMaxStringLength)
                .HasConversion(StringArrayToJsonConverter.Instance)
                .Metadata.SetValueComparer(StringArrayComparer.Instance);

            entity.Property(p => p.DeprecationReasons)
                .HasMaxLength(DefaultMaxStringLength)
                .HasConversion(StringArrayToJsonConverter.Instance)
                .Metadata.SetValueComparer(StringArrayComparer.Instance);

            // Ignore computed properties from Package
            entity.Ignore(p => p.Version);
            entity.Ignore(p => p.IconUrlString);
            entity.Ignore(p => p.LicenseUrlString);
            entity.Ignore(p => p.ProjectUrlString);
            entity.Ignore(p => p.RepositoryUrlString);
        }

        private void BuildVulnerabilityRecordEntity(EntityTypeBuilder<VulnerabilityRecord> vulnerability)
        {
            vulnerability.HasKey(v => v.Key);
            vulnerability.HasIndex(v => v.AdvisoryUrl);
            vulnerability.HasIndex(v => v.UpdatedUtc);

            vulnerability.Property(v => v.AdvisoryUrl)
                .HasMaxLength(DefaultMaxStringLength)
                .IsRequired();

            vulnerability.Property(v => v.Severity)
                .HasMaxLength(50)
                .IsRequired();

            vulnerability.Property(v => v.Description)
                .HasMaxLength(DefaultMaxStringLength);

            vulnerability.HasMany(v => v.AffectedPackages)
                .WithOne(p => p.Vulnerability)
                .HasForeignKey(p => p.VulnerabilityKey)
                .IsRequired();
        }

        private void BuildPackageVulnerabilityEntity(EntityTypeBuilder<PackageVulnerability> packageVulnerability)
        {
            packageVulnerability.HasKey(pv => pv.Key);
            packageVulnerability.HasIndex(pv => pv.PackageId);
            packageVulnerability.HasIndex(pv => new { pv.PackageId, pv.VersionRange });

            packageVulnerability.Property(pv => pv.PackageId)
                .HasMaxLength(MaxPackageIdLength)
                .IsRequired();

            packageVulnerability.Property(pv => pv.VersionRange)
                .HasMaxLength(MaxPackageDependencyVersionRangeLength)
                .IsRequired();
        }
    }
}
