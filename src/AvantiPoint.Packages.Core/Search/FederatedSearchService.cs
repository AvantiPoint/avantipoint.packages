using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Decorates the configured local search provider with live searches across enabled upstream
/// NuGet sources. Only package search is federated; autocomplete, versions, and dependents retain
/// the configured local provider's behavior.
/// </summary>
public sealed class FederatedSearchService(
    ISearchService localSearch,
    IMirrorService mirror,
    IOptions<SearchOptions> options,
    ILogger<FederatedSearchService> logger) : ISearchService
{
    private readonly SearchOptions _options = options.Value;

    public async Task<SearchResponse> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var windowRequest = CreateWindowRequest(request);
        var local = await localSearch.SearchAsync(windowRequest, cancellationToken);

        IReadOnlyList<SearchResponse> upstream;
        using var timeout = new CancellationTokenSource(_options.UpstreamSearchTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);

        try
        {
            upstream = await mirror.SearchAsync(windowRequest, linked.Token);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            logger.LogWarning(
                "Upstream package search exceeded the configured timeout of {Timeout}; returning local results",
                _options.UpstreamSearchTimeout);
            upstream = [];
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Upstream package search failed; returning local results");
            upstream = [];
        }

        return Merge(local, upstream, request, _options.MergeStrategy);
    }

    public Task<AutocompleteResponse> AutocompleteAsync(
        AutocompleteRequest request,
        CancellationToken cancellationToken) =>
        localSearch.AutocompleteAsync(request, cancellationToken);

    public Task<AutocompleteResponse> ListPackageVersionsAsync(
        VersionsRequest request,
        CancellationToken cancellationToken) =>
        localSearch.ListPackageVersionsAsync(request, cancellationToken);

    public Task<DependentsResponse> FindDependentsAsync(
        string packageId,
        CancellationToken cancellationToken) =>
        localSearch.FindDependentsAsync(packageId, cancellationToken);

    private static SearchRequest CreateWindowRequest(SearchRequest request)
    {
        var requestedEnd = Math.Min(
            (long)Math.Max(0, request.Skip) + Math.Max(0, request.Take),
            int.MaxValue);

        return new SearchRequest
        {
            Skip = 0,
            Take = (int)requestedEnd,
            IncludePrerelease = request.IncludePrerelease,
            IncludeSemVer2 = request.IncludeSemVer2,
            PackageType = request.PackageType,
            Framework = request.Framework,
            Query = request.Query,
        };
    }

    private static SearchResponse Merge(
        SearchResponse local,
        IReadOnlyList<SearchResponse> upstream,
        SearchRequest originalRequest,
        FederatedSearchMergeStrategy strategy)
    {
        var responses = new[] { local }.Concat(upstream).ToList();
        var candidates = responses
            .SelectMany(response => response.Data ?? [])
            .OfType<SearchResult>()
            .ToList();
        var merged = strategy switch
        {
            FederatedSearchMergeStrategy.Union => candidates,
            FederatedSearchMergeStrategy.Deduplicate => Deduplicate(candidates, preferFirst: false),
            FederatedSearchMergeStrategy.LocalPreferred => Deduplicate(candidates, preferFirst: true),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown merge strategy."),
        };
        var page = merged
            .Skip(Math.Max(0, originalRequest.Skip))
            .Take(Math.Max(0, originalRequest.Take))
            .ToList();
        var totalHits = SumTotalHits(responses);
        if (strategy != FederatedSearchMergeStrategy.Union)
        {
            totalHits = Math.Max(merged.Count, totalHits - (candidates.Count - merged.Count));
        }

        return new SearchResponse
        {
            Context = local.Context
                ?? upstream.Select(response => response.Context).FirstOrDefault(context => context is not null),
            TotalHits = totalHits,
            Data = page,
        };
    }

    private static List<SearchResult> Deduplicate(
        IReadOnlyList<SearchResult> candidates,
        bool preferFirst)
    {
        var merged = new List<SearchResult>(candidates.Count);
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.PackageId)
                || !indexes.TryGetValue(candidate.PackageId, out var index))
            {
                if (!string.IsNullOrWhiteSpace(candidate.PackageId))
                {
                    indexes[candidate.PackageId] = merged.Count;
                }

                merged.Add(candidate);
                continue;
            }

            if (!preferFirst && IsNewer(candidate, merged[index]))
            {
                merged[index] = candidate;
            }
        }

        return merged;
    }

    private static bool IsNewer(SearchResult candidate, SearchResult current)
    {
        var candidateValid = NuGetVersion.TryParse(candidate.Version, out var candidateVersion);
        var currentValid = NuGetVersion.TryParse(current.Version, out var currentVersion);

        if (candidateValid && currentValid)
        {
            return candidateVersion > currentVersion;
        }

        return candidateValid && !currentValid;
    }

    private static long SumTotalHits(IEnumerable<SearchResponse> responses)
    {
        long total = 0;
        foreach (var response in responses)
        {
            if (response.TotalHits > long.MaxValue - total)
            {
                return long.MaxValue;
            }

            total += Math.Max(0, response.TotalHits);
        }

        return total;
    }
}
