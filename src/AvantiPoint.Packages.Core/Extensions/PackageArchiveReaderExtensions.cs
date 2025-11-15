using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Licenses;
using NuGet.Packaging.Signing;

namespace AvantiPoint.Packages.Core
{
    using NuGetPackageType = NuGet.Packaging.Core.PackageType;

    public static class PackageArchiveReaderExtensions
    {
        private static readonly string[] OrderedReadmeFileNames = new[]
        {
            "readme.md",
            "readme.txt",
        };

        private static readonly HashSet<string> ReadmeFileNames = new HashSet<string>(
            OrderedReadmeFileNames,
            StringComparer.OrdinalIgnoreCase);

        public static bool HasReadme(this PackageArchiveReader package)
            => package.GetFiles().Any(ReadmeFileNames.Contains);

        public static bool HasEmbeddedIcon(this PackageArchiveReader package)
            => !string.IsNullOrEmpty(package.NuspecReader.GetIcon());

        public static bool HasEmbeddedLicense(this PackageArchiveReader package)
        {
            var licenseMetadata = package.NuspecReader.GetLicenseMetadata();
            return licenseMetadata?.Type == NuGet.Packaging.LicenseType.File;
        }

        public async static Task<Stream> GetReadmeAsync(
            this PackageArchiveReader package,
            CancellationToken cancellationToken)
        {
            var packageFiles = package.GetFiles();

            foreach (var readmeFileName in OrderedReadmeFileNames)
            {
                var readmePath = packageFiles.FirstOrDefault(f => f.Equals(readmeFileName, StringComparison.OrdinalIgnoreCase));

                if (readmePath != null)
                {
                    return await package.GetStreamAsync(readmePath, cancellationToken);
                }
            }

            throw new InvalidOperationException("Package does not have a readme!");
        }

        public async static Task<Stream> GetIconAsync(
            this PackageArchiveReader package,
            CancellationToken cancellationToken)
        {
            return await package.GetStreamAsync(
                PathUtility.StripLeadingDirectorySeparators(package.NuspecReader.GetIcon()),
                cancellationToken);
        }

        public async static Task<Stream> GetLicenseAsync(
            this PackageArchiveReader package,
            CancellationToken cancellationToken)
        {
            var licenseMetadata = package.NuspecReader.GetLicenseMetadata();
            if (licenseMetadata?.Type != NuGet.Packaging.LicenseType.File)
            {
                throw new InvalidOperationException("Package does not have an embedded license!");
            }

            return await package.GetStreamAsync(
                PathUtility.StripLeadingDirectorySeparators(licenseMetadata.License),
                cancellationToken);
        }

        /// <summary>
        /// Attempts to get the signature timestamp from a signed package.
        /// Returns null if the package is not signed or has no timestamp.
        /// </summary>
        private static async Task<DateTime?> GetSignatureTimestampAsync(
            this PackageArchiveReader packageReader,
            CancellationToken cancellationToken)
        {
            try
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(cancellationToken);
                if (primarySignature != null)
                {
                    // Try to get the timestamp from the signature
                    var timestamp = primarySignature.Timestamps?.FirstOrDefault();
                    if (timestamp != null)
                    {
                        return timestamp.GeneralizedTime.UtcDateTime;
                    }
                }
            }
            catch
            {
                // If we can't get the signature or timestamp, return null
                // This will fall back to using the current time
            }

            return null;
        }

        public static async Task<Package> GetPackageMetadata(this PackageArchiveReader packageReader)
        {
            var nuspec = packageReader.NuspecReader;

            (var repositoryUri, var repositoryType) = GetRepositoryMetadata(nuspec);
            (var repositoryCommit, var repositoryCommitDate) = GetRepositoryCommitMetadata(nuspec);

            // Try to get the signature timestamp, fallback to current time if not available
            var signatureTimestamp = await packageReader.GetSignatureTimestampAsync(default);
            var publishedDate = signatureTimestamp ?? DateTime.UtcNow;

            return new Package
            {
                Id = nuspec.GetId(),
                Version = nuspec.GetVersion(),
                Authors = ParseAuthors(nuspec.GetAuthors()),
                Description = nuspec.GetDescription(),
                HasReadme = packageReader.HasReadme(),
                HasEmbeddedIcon = packageReader.HasEmbeddedIcon(),
                HasEmbeddedLicense = packageReader.HasEmbeddedLicense(),
                IsPrerelease = nuspec.GetVersion().IsPrerelease,
                Language = nuspec.GetLanguage() ?? string.Empty,
                ReleaseNotes = nuspec.GetReleaseNotes() ?? string.Empty,
                Listed = true,
                IsTool = nuspec.GetPackageTypes()?.Any(x => x.Name == "DotnetTool") ?? false,
                IsSigned = await packageReader.IsSignedAsync(default),
                IsDevelopmentDependency = nuspec.GetDevelopmentDependency(),
                LicenseExpression = nuspec.GetLicenseMetadata()?.License ?? string.Empty,
                MinClientVersion = nuspec.GetMinClientVersion()?.ToNormalizedString() ?? string.Empty,
                Published = publishedDate,
                RequireLicenseAcceptance = nuspec.GetRequireLicenseAcceptance(),
                SemVerLevel = GetSemVerLevel(nuspec),
                Summary = nuspec.GetSummary(),
                Title = nuspec.GetTitle(),
                IconUrl = ParseUri(nuspec.GetIconUrl()),
                LicenseUrl = ParseUri(nuspec.GetLicenseUrl()),
                ProjectUrl = ParseUri(nuspec.GetProjectUrl()),
                RepositoryUrl = repositoryUri,
                RepositoryType = repositoryType,
                RepositoryCommit = repositoryCommit,
                RepositoryCommitDate = repositoryCommitDate,
                Dependencies = GetDependencies(nuspec),
                Tags = ParseTags(nuspec.GetTags()),
                PackageTypes = GetPackageTypes(nuspec),
                TargetFrameworks = GetTargetFrameworks(packageReader),
                // Deprecation info is not set during package upload
                // It would need to be set through a separate administrative action
                IsDeprecated = false,
                DeprecationReasons = new string[0],
                DeprecationMessage = null,
                DeprecatedAlternatePackageId = null,
                DeprecatedAlternatePackageVersionRange = null,
            };
        }

        // Based off https://github.com/NuGet/NuGetGallery/blob/master/src/NuGetGallery.Core/SemVerLevelKey.cs
        private static SemVerLevel GetSemVerLevel(NuspecReader nuspec)
        {
            if (nuspec.GetVersion().IsSemVer2)
            {
                return SemVerLevel.SemVer2;
            }

            foreach (var dependencyGroup in nuspec.GetDependencyGroups())
            {
                foreach (var dependency in dependencyGroup.Packages)
                {
                    if ((dependency.VersionRange.MinVersion != null && dependency.VersionRange.MinVersion.IsSemVer2)
                        || (dependency.VersionRange.MaxVersion != null && dependency.VersionRange.MaxVersion.IsSemVer2))
                    {
                        return SemVerLevel.SemVer2;
                    }
                }
            }

            return SemVerLevel.Unknown;
        }

        private static Uri ParseUri(string uriString)
        {
            if (string.IsNullOrEmpty(uriString)) return null;

            return new Uri(uriString);
        }

        private static string[] ParseAuthors(string authors)
        {
            if (string.IsNullOrEmpty(authors)) return new string[0];

            return authors.Split(new[] { ',', ';', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string[] ParseTags(string tags)
        {
            if (string.IsNullOrEmpty(tags)) return new string[0];

            return tags.Split(new[] { ',', ';', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static (Uri repositoryUrl, string repositoryType) GetRepositoryMetadata(NuspecReader nuspec)
        {
            var repository = nuspec.GetRepositoryMetadata();

            if (string.IsNullOrEmpty(repository?.Url) ||
                !Uri.TryCreate(repository.Url, UriKind.Absolute, out var repositoryUri))
            {
                return (null, null);
            }

            if (repositoryUri.Scheme != Uri.UriSchemeHttps)
            {
                return (null, null);
            }

            if (repository.Type.Length > 100)
            {
                throw new InvalidOperationException("Repository type must be less than or equal 100 characters");
            }

            return (repositoryUri, repository.Type);
        }

        private static (string commit, DateTime? commitDate) GetRepositoryCommitMetadata(NuspecReader nuspec)
        {
            var repository = nuspec.GetRepositoryMetadata();

            if (repository == null)
            {
                return (null, null);
            }

            // The commit SHA is stored in the Commit property
            var commit = repository.Commit;
            if (string.IsNullOrWhiteSpace(commit))
            {
                return (null, null);
            }

            // Ensure the commit SHA is a reasonable length (Git SHA-1 is 40 chars, SHA-256 is 64 chars)
            if (commit.Length > 64)
            {
                return (null, null);
            }

            // The commit date is not directly available in RepositoryMetadata
            // It would need to be set through other means if available
            return (commit, null);
        }

        private static List<PackageDependency> GetDependencies(NuspecReader nuspec)
        {
            var dependencies = new List<PackageDependency>();

            foreach (var group in nuspec.GetDependencyGroups())
            {
                var targetFramework = group.TargetFramework.GetShortFolderName();

                if (!group.Packages.Any())
                {
                    dependencies.Add(new PackageDependency
                    {
                        Id = null,
                        VersionRange = null,
                        TargetFramework = targetFramework,
                    });
                }

                foreach (var dependency in group.Packages)
                {
                    dependencies.Add(new PackageDependency
                    {
                        Id = dependency.Id,
                        VersionRange = dependency.VersionRange?.ToString(),
                        TargetFramework = targetFramework,
                    });
                }
            }

            return dependencies;
        }

        private static List<PackageType> GetPackageTypes(NuspecReader nuspec)
        {
            var packageTypes = nuspec
                .GetPackageTypes()
                .Select(t => new PackageType
                {
                    Name = t.Name,
                    Version = t.Version.ToString()
                })
                .ToList();

            // Default to the standard "dependency" package type if no types were found.
            if (packageTypes.Count == 0)
            {
                packageTypes.Add(new PackageType
                {
                    Name = NuGetPackageType.Dependency.Name,
                    Version = NuGetPackageType.Dependency.Version.ToString(),
                });
            }

            return packageTypes;
        }

        private static List<TargetFramework> GetTargetFrameworks(PackageArchiveReader packageReader)
        {
            var targetFrameworks = packageReader
                .GetSupportedFrameworks()
                .Select(f => new TargetFramework
                {
                    Moniker = f.GetShortFolderName()
                })
                .ToList();

            // Default to the "any" framework if no frameworks were found.
            if (targetFrameworks.Count == 0)
            {
                targetFrameworks.Add(new TargetFramework { Moniker = "any" });
            }

            return targetFrameworks;
        }
    }
}
