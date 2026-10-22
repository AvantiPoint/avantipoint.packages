using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core.Entities.Oci;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.UI.Services;

/// <summary>
/// Searches every configured human-facing package surface while keeping the
/// protocol-specific browse services as the source of truth for visibility policy.
/// Each search uses its own scope because the browse services share a scoped EF context.
/// </summary>
public sealed class ArtifactDiscoveryService(
    IFeedRegistry feedRegistry,
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<OciFeedOptions> ociOptions,
    ILogger<ArtifactDiscoveryService> logger) : IArtifactDiscoveryService
{
    public async Task<ArtifactDiscoveryResult> SearchAsync(
        string? query,
        int takePerSurface = 6,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        takePerSurface = Math.Clamp(takePerSurface, 1, 12);

        var searches = new List<Task<ArtifactDiscoverySection>>();
        if (feedRegistry.TryGetNuGetSurface() is { } nuget)
        {
            searches.Add(SearchNuGetAsync(nuget, normalizedQuery, takePerSurface, cancellationToken));
        }

        if (feedRegistry.TryGetNpmSurface() is { } npm)
        {
            searches.Add(SearchNpmAsync(npm, normalizedQuery, takePerSurface, cancellationToken));
        }

        foreach (var oci in feedRegistry.Surfaces.Where(static surface => surface.Protocol == FeedProtocol.Oci))
        {
            searches.Add(SearchOciAsync(oci, normalizedQuery, takePerSurface, cancellationToken));
        }

        var sections = searches.Count == 0
            ? []
            : await Task.WhenAll(searches);

        return new ArtifactDiscoveryResult(normalizedQuery, sections);
    }

    private async Task<ArtifactDiscoverySection> SearchNuGetAsync(
        SurfaceRegistration surface,
        string query,
        int take,
        CancellationToken cancellationToken)
    {
        const string name = "NuGet";
        const string description = ".NET libraries, tools, and templates";
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<INuGetSearchService>();
            var response = await service.SearchAsync(
                query,
                skip: 0,
                take: take,
                includePrerelease: false,
                cancellationToken: cancellationToken);
            var items = (response.Data ?? [])
                .Select(package => new ArtifactDiscoveryItem(
                    package.PackageId,
                    package.Version,
                    FirstNonEmpty(package.Description, package.Summary),
                    BuildNuGetPackageHref(package.PackageId, package.Version),
                    package.Published,
                    package.Authors is { Count: > 0 }
                        ? string.Join(", ", package.Authors.Take(3))
                        : null))
                .ToList();

            return new ArtifactDiscoverySection(
                surface.SurfaceId,
                name,
                ArtifactDiscoveryKind.NuGet,
                description,
                "/",
                "/feed",
                response.TotalHits,
                response.TotalHits > items.Count,
                items);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unified discovery could not search the NuGet surface {SurfaceId}", surface.SurfaceId);
            return Unavailable(surface.SurfaceId, name, ArtifactDiscoveryKind.NuGet, description, "/", "/feed");
        }
    }

    private async Task<ArtifactDiscoverySection> SearchNpmAsync(
        SurfaceRegistration surface,
        string query,
        int take,
        CancellationToken cancellationToken)
    {
        const string name = "npm";
        const string description = "JavaScript and TypeScript packages";
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<INpmPackageBrowseService>();
            var matches = await service.SearchAsync(
                query,
                skip: 0,
                take: take + 1,
                cancellationToken: cancellationToken);
            var hasMore = matches.Count > take;
            var items = matches.Take(take)
                .Select(package => new ArtifactDiscoveryItem(
                    package.Name,
                    package.LatestVersion,
                    package.Description,
                    BuildNpmPackageHref(package.Name),
                    package.Published is { } published
                        ? new DateTimeOffset(DateTime.SpecifyKind(published, DateTimeKind.Utc))
                        : null,
                    package.Keywords is { Count: > 0 }
                        ? string.Join(" · ", package.Keywords.Take(4))
                        : null))
                .ToList();

            return new ArtifactDiscoverySection(
                surface.SurfaceId,
                name,
                ArtifactDiscoveryKind.Npm,
                description,
                "/browse/npm",
                "/browse/npm/connect",
                TotalCount: null,
                HasMore: hasMore,
                Items: items);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unified discovery could not search the npm surface {SurfaceId}", surface.SurfaceId);
            return Unavailable(surface.SurfaceId, name, ArtifactDiscoveryKind.Npm, description, "/browse/npm", "/browse/npm/connect");
        }
    }

    private async Task<ArtifactDiscoverySection> SearchOciAsync(
        SurfaceRegistration surface,
        string query,
        int take,
        CancellationToken cancellationToken)
    {
        var browseHref = BuildOciBrowseHref(surface.OciSegment);
        var connectHref = BuildOciConnectHref(surface.OciSegment);
        var name = string.IsNullOrEmpty(surface.OciSegment) ? "OCI" : $"{surface.OciSegment} (OCI)";
        var kind = ArtifactDiscoveryKind.Oci;
        var description = "OCI repositories and immutable artifacts";

        try
        {
            var options = ociOptions.Get(GetOciOptionsName(surface));
            name = OciSurfacePresentation.GetDisplayName(options, surface.OciSegment);
            kind = GetDiscoveryKind(options.Profile);
            description = options.Profile switch
            {
                OciProfile.ContainerImages => "Container image repositories and immutable tags",
                OciProfile.HelmCharts => "Kubernetes charts and deployable versions",
                _ => "OCI repositories and immutable artifacts",
            };
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IOciRepositoryBrowseService>();
            var response = await service.SearchRepositoriesAsync(
                surface.OciSegment,
                query,
                skip: 0,
                take: take,
                cancellationToken: cancellationToken);
            var items = response.Items
                .Select(repository => new ArtifactDiscoveryItem(
                    repository.Name,
                    repository.LatestTag,
                    DescribeOciRepository(repository),
                    BuildOciRepositoryHref(surface.OciSegment, repository.Name),
                    repository.UpdatedAt is { } updated
                        ? new DateTimeOffset(DateTime.SpecifyKind(updated, DateTimeKind.Utc))
                        : null,
                    repository.TagCount == 1
                        ? options.Profile == OciProfile.HelmCharts ? "1 version" : "1 tag"
                        : options.Profile == OciProfile.HelmCharts
                            ? $"{repository.TagCount} versions"
                            : $"{repository.TagCount} tags"))
                .ToList();

            return new ArtifactDiscoverySection(
                surface.SurfaceId,
                name,
                kind,
                description,
                browseHref,
                connectHref,
                response.TotalCount,
                response.HasNextPage,
                items);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unified discovery could not search the OCI surface {SurfaceId}", surface.SurfaceId);
            return Unavailable(surface.SurfaceId, name, kind, description, browseHref, connectHref);
        }
    }

    private static ArtifactDiscoverySection Unavailable(
        string id,
        string name,
        ArtifactDiscoveryKind kind,
        string description,
        string browseHref,
        string connectHref) =>
        new(
            id,
            name,
            kind,
            description,
            browseHref,
            connectHref,
            TotalCount: null,
            HasMore: false,
            Items: [],
            ErrorMessage: $"{name} results are temporarily unavailable.");

    private static string? FirstNonEmpty(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) ? first : !string.IsNullOrWhiteSpace(second) ? second : null;

    private static string BuildNuGetPackageHref(string packageId, string? version)
    {
        var href = $"/packages/{Uri.EscapeDataString(packageId)}";
        return string.IsNullOrWhiteSpace(version) ? href : $"{href}/{Uri.EscapeDataString(version)}";
    }

    private static string BuildNpmPackageHref(string packageName) =>
        "/browse/npm/packages/" + EncodePath(packageName);

    private static string BuildOciBrowseHref(string? segment) =>
        string.IsNullOrEmpty(segment) ? "/oci" : $"/oci/{Uri.EscapeDataString(segment)}";

    private static string BuildOciConnectHref(string? segment) =>
        string.IsNullOrEmpty(segment) ? "/oci/connect" : $"/oci/connect/{Uri.EscapeDataString(segment)}";

    private static string BuildOciRepositoryHref(string? segment, string repositoryName)
    {
        var prefix = string.IsNullOrEmpty(segment) ? "/oci" : $"/oci/{Uri.EscapeDataString(segment)}";
        return $"{prefix}/repos/{EncodePath(repositoryName)}";
    }

    private static string EncodePath(string value) => string.Join(
        '/',
        value.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

    private static string GetOciOptionsName(SurfaceRegistration surface) =>
        string.IsNullOrEmpty(surface.OciSegment)
            ? "OciFeed:default"
            : OciSurfaceOptionsBuilder.GetOptionsName(surface.OciSegment);

    private static ArtifactDiscoveryKind GetDiscoveryKind(OciProfile profile) => profile switch
    {
        OciProfile.ContainerImages => ArtifactDiscoveryKind.Docker,
        OciProfile.HelmCharts => ArtifactDiscoveryKind.Helm,
        _ => ArtifactDiscoveryKind.Oci,
    };

    private static string DescribeOciRepository(OciRepositoryListItem repository)
    {
        var kind = repository.ArtifactKind switch
        {
            OciArtifactKind.Image => "Container image",
            OciArtifactKind.Index => "Multi-platform container image",
            OciArtifactKind.Helm => "Helm chart",
            _ => "OCI artifact",
        };
        var platform = !string.IsNullOrEmpty(repository.PlatformOs) && !string.IsNullOrEmpty(repository.PlatformArch)
            ? $" · {repository.PlatformOs}/{repository.PlatformArch}"
            : string.Empty;
        return kind + platform;
    }
}
