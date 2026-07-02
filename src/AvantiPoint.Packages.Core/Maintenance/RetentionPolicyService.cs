#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core.Maintenance
{
    public interface IRetentionPolicyService
    {
        /// <summary>Computes the versions the current policy would remove, without deleting anything.</summary>
        Task<IReadOnlyList<RetentionCandidate>> GetCandidatesAsync(CancellationToken cancellationToken = default);

        /// <summary>Applies the policy. Honors <see cref="RetentionOptions.DryRun"/>. Returns the number of versions removed.</summary>
        Task<int> ApplyAsync(CancellationToken cancellationToken = default);
    }

    public sealed record RetentionCandidate(string PackageId, NuGetVersion Version, string Reason);

    public sealed class RetentionPolicyService(
        IContext context,
        IPackageDeletionService deletionService,
        IOptionsSnapshot<RetentionOptions> options,
        TimeProvider timeProvider,
        ILogger<RetentionPolicyService> logger) : IRetentionPolicyService
    {
        public async Task<IReadOnlyList<RetentionCandidate>> GetCandidatesAsync(CancellationToken cancellationToken = default)
        {
            var policy = options.Value;
            if (!policy.Enabled
                || (policy.MaxPrereleaseVersionsPerPackage is null && policy.MaxPrereleaseAgeDays is null))
            {
                return [];
            }

            var excluded = new HashSet<string>(policy.ExcludedPackageIds, StringComparer.OrdinalIgnoreCase);

            // Only published (non-mirrored) prerelease versions are ever pruned.
            var prereleases = await context.Packages
                .AsNoTracking()
                .Where(p => p.IsPrerelease && p.Origin == PackageOrigin.Published)
                .Select(p => new { p.Id, p.NormalizedVersionString, p.Published })
                .ToListAsync(cancellationToken);

            var candidates = new Dictionary<(string Id, string Version), RetentionCandidate>();
            var now = timeProvider.GetUtcNow().UtcDateTime;

            if (policy.MaxPrereleaseAgeDays is { } maxAgeDays)
            {
                var cutoff = now.AddDays(-maxAgeDays);
                foreach (var package in prereleases.Where(p => p.Published < cutoff && !excluded.Contains(p.Id)))
                {
                    candidates[(package.Id, package.NormalizedVersionString)] = new RetentionCandidate(
                        package.Id,
                        NuGetVersion.Parse(package.NormalizedVersionString),
                        $"older than {maxAgeDays} days");
                }
            }

            if (policy.MaxPrereleaseVersionsPerPackage is { } keep)
            {
                foreach (var group in prereleases.GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase))
                {
                    if (excluded.Contains(group.Key))
                    {
                        continue;
                    }

                    foreach (var package in group
                        .OrderByDescending(p => NuGetVersion.Parse(p.NormalizedVersionString))
                        .Skip(keep))
                    {
                        candidates[(package.Id, package.NormalizedVersionString)] = new RetentionCandidate(
                            package.Id,
                            NuGetVersion.Parse(package.NormalizedVersionString),
                            $"exceeds newest {keep} prerelease version(s)");
                    }
                }
            }

            return candidates.Values
                .OrderBy(c => c.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Version)
                .ToList();
        }

        public async Task<int> ApplyAsync(CancellationToken cancellationToken = default)
        {
            var candidates = await GetCandidatesAsync(cancellationToken);
            if (candidates.Count == 0)
            {
                return 0;
            }

            if (options.Value.DryRun)
            {
                foreach (var candidate in candidates)
                {
                    logger.LogInformation(
                        "Retention dry run: would remove {PackageId} {Version} ({Reason})",
                        candidate.PackageId,
                        candidate.Version,
                        candidate.Reason);
                }

                return 0;
            }

            var removed = 0;
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await deletionService.TryDeletePackageAsync(candidate.PackageId, candidate.Version, cancellationToken))
                {
                    logger.LogInformation(
                        "Retention removed {PackageId} {Version} ({Reason})",
                        candidate.PackageId,
                        candidate.Version,
                        candidate.Reason);
                    removed++;
                }
            }

            return removed;
        }
    }
}
