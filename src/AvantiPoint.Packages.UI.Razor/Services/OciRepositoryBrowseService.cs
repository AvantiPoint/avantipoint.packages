using System.Text.Json;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using AvantiPoint.Packages.Registry.Oci;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.UI.Services;

public sealed class OciRepositoryBrowseService : IOciRepositoryBrowseService
{
    private const int MaximumPageSize = 100;
    private const string DockerManifestMediaType = "application/vnd.docker.distribution.manifest.v2+json";
    private const string OciImageManifestMediaType = "application/vnd.oci.image.manifest.v1+json";

    private readonly IFeedRegistry _feedRegistry;
    private readonly IOciRegistryService _registryService;
    private readonly IContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPublicBaseUrlProvider _publicBaseUrlProvider;
    private readonly OciFeedOptionsAccessor _optionsAccessor;

    public OciRepositoryBrowseService(
        IFeedRegistry feedRegistry,
        IOciRegistryService registryService,
        IContext context,
        IHttpContextAccessor httpContextAccessor,
        IPublicBaseUrlProvider publicBaseUrlProvider,
        OciFeedOptionsAccessor optionsAccessor)
    {
        _feedRegistry = feedRegistry;
        _registryService = registryService;
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _publicBaseUrlProvider = publicBaseUrlProvider;
        _optionsAccessor = optionsAccessor;
    }

    public async Task<OciRepositorySearchResult> SearchRepositoriesAsync(
        string? segment,
        string? query = null,
        string? kind = null,
        int skip = 0,
        int take = 24,
        CancellationToken cancellationToken = default)
    {
        var surface = ResolveSurface(segment);
        if (surface is null)
        {
            return new OciRepositorySearchResult([], 0, 0, Math.Clamp(take, 1, MaximumPageSize));
        }

        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, MaximumPageSize);

        var options = _optionsAccessor.GetOptions(CreateSurfaceContext(surface));
        var effectiveKind = GetEffectiveKindFilter(options.Profile, kind);
        var visibleOrigins = OciDiscoveryPolicy.GetVisibleOrigins(options).ToArray();
        var repositoryQuery = BuildVisibleRepositoryQuery(surface, visibleOrigins, effectiveKind);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalizedQuery = query.Trim().ToLower();
            repositoryQuery = repositoryQuery.Where(r => r.Name.ToLower().Contains(normalizedQuery));
        }

        var totalCount = await repositoryQuery.CountAsync(cancellationToken);
        if (skip >= totalCount && totalCount > 0)
        {
            skip = Math.Max(0, ((totalCount - 1) / take) * take);
        }

        var repositoryIds = await repositoryQuery
            .OrderBy(r => r.Name)
            .Skip(skip)
            .Take(take)
            .Select(r => r.Key)
            .ToListAsync(cancellationToken);

        var items = await LoadRepositoryItemsAsync(
            surface,
            repositoryIds,
            visibleOrigins,
            effectiveKind,
            cancellationToken);

        return new OciRepositorySearchResult(items, totalCount, skip, take);
    }

    public async Task<IReadOnlyList<OciRepositoryListItem>> ListRepositoriesAsync(
        string? segment,
        int? max = null,
        string? last = null,
        CancellationToken cancellationToken = default)
    {
        var surface = ResolveSurface(segment);
        if (surface is null)
        {
            return [];
        }

        var surfaceContext = CreateSurfaceContext(surface);
        var options = _optionsAccessor.GetOptions(surfaceContext);
        var catalog = await _registryService.ListCatalogAsync(surfaceContext, max, last, cancellationToken);
        if (catalog.Repositories.Count == 0)
        {
            return [];
        }

        var repositoryIds = await _context.OciRepositories
            .AsNoTracking()
            .Where(r => r.FeedId == _feedRegistry.Feed.FeedId
                        && r.OciSegment == surface.OciSegment
                        && catalog.Repositories.Contains(r.Name))
            .OrderBy(r => r.Name)
            .Select(r => r.Key)
            .ToListAsync(cancellationToken);

        return await LoadRepositoryItemsAsync(
            surface,
            repositoryIds,
            OciDiscoveryPolicy.GetVisibleOrigins(options).ToArray(),
            GetEffectiveKindFilter(options.Profile, null),
            cancellationToken);
    }

    public async Task<OciRepositoryTagsModel?> ListTagsAsync(
        string repositoryName,
        string? segment,
        int? max = null,
        string? last = null,
        CancellationToken cancellationToken = default)
    {
        var surface = ResolveSurface(segment);
        if (surface is null)
        {
            return null;
        }

        var feedId = _feedRegistry.Feed.FeedId;
        var repository = await _context.OciRepositories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.FeedId == feedId
                     && r.OciSegment == surface.OciSegment
                     && r.Name == repositoryName,
                cancellationToken);
        if (repository is null)
        {
            return null;
        }

        var surfaceContext = CreateSurfaceContext(surface);
        // Native and human repository views share the same tracked-only refresh path. This
        // may update Helm manifest metadata for this repository, but it never downloads
        // chart layers and never enumerates the upstream registry catalog.
        await _registryService.ListTagsAsync(
            surfaceContext,
            repositoryName,
            max: null,
            last: null,
            cancellationToken);

        var options = _optionsAccessor.GetOptions(surfaceContext);
        var visibleOrigins = OciDiscoveryPolicy.GetVisibleOrigins(options).ToArray();
        var effectiveKind = GetEffectiveKindFilter(options.Profile, null);
        var tags = await _context.OciTags
            .AsNoTracking()
            .Where(t => t.FeedId == feedId
                        && t.OciSegment == surface.OciSegment
                        && t.RepositoryKey == repository.Key
                        && visibleOrigins.Contains(t.Origin)
                        && !t.Tag.Contains(":"))
            .ToListAsync(cancellationToken);

        var manifests = await LoadManifestsAsync(surface, tags.Select(t => t.ManifestDigest), cancellationToken);
        var items = tags
            .Where(tag => !TryParseLegacySupplyChainReference(tag.Tag, out _)
                          && !OciReferrersIndex.IsInternalTag(tag.Tag))
            .Select(tag => ToTagListItem(tag, manifests))
            .Where(tag => MatchesKind(tag.ArtifactKind, effectiveKind))
            .ToList();

        if (items.Count == 0)
        {
            return null;
        }

        var artifactKind = ResolveRepositoryKind(items.Select(item => item.ArtifactKind));
        items = items
            .OrderBy(item => item.Tag, GetTagComparer(artifactKind))
            .ToList();

        if (!string.IsNullOrEmpty(last))
        {
            var lastIndex = items.FindIndex(item => item.Tag.Equals(last, StringComparison.Ordinal));
            if (lastIndex >= 0)
            {
                items = items.Skip(lastIndex + 1).ToList();
            }
        }

        if (max is > 0)
        {
            items = items.Take(max.Value).ToList();
        }

        return new OciRepositoryTagsModel(repositoryName, artifactKind, items);
    }

    public async Task<OciArtifactDetailModel?> GetArtifactAsync(
        string repositoryName,
        string reference,
        string? segment,
        CancellationToken cancellationToken = default)
    {
        var surface = ResolveSurface(segment);
        if (surface is null)
        {
            return null;
        }

        var feedId = _feedRegistry.Feed.FeedId;
        var options = _optionsAccessor.GetOptions(CreateSurfaceContext(surface));
        var visibleOrigins = OciDiscoveryPolicy.GetVisibleOrigins(options).ToArray();
        var tagQuery = _context.OciTags
            .AsNoTracking()
            .Where(t => t.FeedId == feedId
                        && t.OciSegment == surface.OciSegment
                        && t.Repository.Name == repositoryName
                        && visibleOrigins.Contains(t.Origin));

        tagQuery = IsDigestReference(reference)
            ? tagQuery.Where(t => t.ManifestDigest == reference)
            : tagQuery.Where(t => t.Tag == reference);

        var selectedTag = await tagQuery.FirstOrDefaultAsync(cancellationToken);
        if (selectedTag is null)
        {
            return null;
        }

        var record = await _context.OciManifests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.FeedId == feedId
                     && m.OciSegment == surface.OciSegment
                     && m.Digest == selectedTag.ManifestDigest,
                cancellationToken);
        if (record is null)
        {
            // Browse pages describe content already stored by this feed. Do not turn a human
            // detail-page visit into an upstream registry request.
            return null;
        }

        var context = CreateSurfaceContext(surface);
        var manifest = await _registryService.GetManifestAsync(
            context,
            repositoryName,
            record.Digest,
            cancellationToken);
        if (manifest is null)
        {
            return null;
        }

        var tags = await _context.OciTags
            .AsNoTracking()
            .Where(t => t.FeedId == feedId
                        && t.OciSegment == surface.OciSegment
                        && t.Repository.Name == repositoryName
                        && t.ManifestDigest == manifest.Digest
                        && visibleOrigins.Contains(t.Origin)
                        && !t.Tag.Contains(":"))
            .Select(t => t.Tag)
            .ToListAsync(cancellationToken);

        var registryRoot = GetRegistryRootUrl(segment);
        var parsed = OciManifestParser.Parse(manifest.MediaType, manifest.Content);
        var artifactKind = record.ArtifactKind != OciArtifactKind.Unknown
            ? record.ArtifactKind
            : parsed.ArtifactKind;
        tags.Sort(GetTagComparer(artifactKind));

        if (!MatchesKind(artifactKind, GetEffectiveKindFilter(options.Profile, null)))
        {
            return null;
        }

        var dockerDetails = artifactKind is OciArtifactKind.Image or OciArtifactKind.Index
            ? await LoadDockerDetailsAsync(context, artifactKind, manifest.Content, cancellationToken)
            : null;
        if (dockerDetails is not null)
        {
            var supplyChainArtifacts = await LoadSupplyChainArtifactsAsync(
                surface,
                context,
                selectedTag.RepositoryKey,
                repositoryName,
                manifest.Digest,
                visibleOrigins,
                cancellationToken);
            dockerDetails = dockerDetails with
            {
                ManifestDigest = manifest.Digest,
                SupplyChainArtifacts = supplyChainArtifacts,
                VulnerabilityPolicyAssessment = OciVulnerabilityPolicyEvaluator.Evaluate(
                    options.VulnerabilityPolicy,
                    supplyChainArtifacts),
            };
        }
        var dockerComparison = dockerDetails is not null
            ? await LoadPreviousDockerComparisonAsync(
                surface,
                context,
                selectedTag.RepositoryKey,
                repositoryName,
                reference,
                manifest.Digest,
                dockerDetails,
                visibleOrigins,
                cancellationToken)
            : null;
        var helmDetails = artifactKind == OciArtifactKind.Helm
            ? await HelmChartArtifactReader.ReadAsync(
                manifest.Content,
                (digest, token) => _registryService.GetBlobAsync(
                    context,
                    digest,
                    repositoryName: null,
                    token),
                cancellationToken)
            : null;
        var helmComparison = helmDetails is not null
            ? await LoadPreviousHelmComparisonAsync(
                surface,
                context,
                selectedTag.RepositoryKey,
                repositoryName,
                reference,
                manifest.Digest,
                helmDetails,
                visibleOrigins,
                cancellationToken)
            : null;

        return new OciArtifactDetailModel(
            repositoryName,
            reference,
            manifest.Digest,
            manifest.MediaType,
            artifactKind,
            dockerDetails?.OperatingSystem ?? record.PlatformOs ?? parsed.PlatformOs,
            dockerDetails?.Architecture ?? record.PlatformArch ?? parsed.PlatformArch,
            record.Size,
            tags,
            registryRoot,
            record.CreatedAt,
            record.Origin,
            CountReferencedArtifacts(artifactKind, manifest.Content, parsed.ReferencedDigests.Count),
            dockerDetails,
            helmDetails,
            helmComparison,
            dockerComparison);
    }

    public string GetRegistryRootUrl(string? segment)
    {
        var surface = ResolveSurface(segment);
        if (surface is null)
        {
            return "/v2/";
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return string.IsNullOrEmpty(surface.OciSegment)
                ? "/v2/"
                : $"{surface.RoutePrefix}/v2/";
        }

        var baseUrl = _publicBaseUrlProvider.GetSurfacePublicBaseUrl(httpContext, surface.RoutePrefix);
        return baseUrl.ToString().TrimEnd('/') + "/v2/";
    }

    public string GetRegistryApiUrl(string? segment) => GetRegistryRootUrl(segment);

    private IQueryable<OciRepository> BuildVisibleRepositoryQuery(
        SurfaceRegistration surface,
        PackageOrigin[] visibleOrigins,
        string effectiveKind)
    {
        var feedId = _feedRegistry.Feed.FeedId;
        var visibleTags = _context.OciTags
            .AsNoTracking()
            .Where(t => t.FeedId == feedId
                        && t.OciSegment == surface.OciSegment
                        && visibleOrigins.Contains(t.Origin)
                        && !t.Tag.Contains(":")
                        && !(t.Tag.StartsWith("sha256-") && t.Tag.Length == 71)
                        && !(t.Tag.StartsWith("sha512-") && t.Tag.Length == 71));

        var repositories = _context.OciRepositories
            .AsNoTracking()
            .Where(r => r.FeedId == feedId
                        && r.OciSegment == surface.OciSegment
                        && visibleTags.Any(t => t.RepositoryKey == r.Key));

        var kinds = OciCatalogKind.GetArtifactKinds(effectiveKind);
        if (kinds.Count == 0)
        {
            return repositories;
        }

        var matchingRepositoryIds =
            from tag in visibleTags
            join manifest in _context.OciManifests.AsNoTracking()
                on new { tag.FeedId, tag.OciSegment, Digest = tag.ManifestDigest }
                equals new { manifest.FeedId, manifest.OciSegment, Digest = manifest.Digest }
            where kinds.Contains(manifest.ArtifactKind)
                  || (effectiveKind == OciCatalogKind.Docker
                      && manifest.ArtifactKind == OciArtifactKind.Unknown
                      && (manifest.MediaType.StartsWith(DockerManifestMediaType)
                          || manifest.MediaType.StartsWith(OciImageManifestMediaType)))
            select tag.RepositoryKey;

        return repositories.Where(repository => matchingRepositoryIds.Contains(repository.Key));
    }

    private async Task<IReadOnlyList<OciRepositoryListItem>> LoadRepositoryItemsAsync(
        SurfaceRegistration surface,
        IReadOnlyCollection<int> repositoryIds,
        PackageOrigin[] visibleOrigins,
        string effectiveKind,
        CancellationToken cancellationToken)
    {
        if (repositoryIds.Count == 0)
        {
            return [];
        }

        var feedId = _feedRegistry.Feed.FeedId;
        var repositories = await _context.OciRepositories
            .AsNoTracking()
            .Where(r => r.FeedId == feedId
                        && r.OciSegment == surface.OciSegment
                        && repositoryIds.Contains(r.Key))
            .ToListAsync(cancellationToken);
        var tags = await _context.OciTags
            .AsNoTracking()
            .Where(t => t.FeedId == feedId
                        && t.OciSegment == surface.OciSegment
                        && repositoryIds.Contains(t.RepositoryKey)
                        && visibleOrigins.Contains(t.Origin)
                        && !t.Tag.Contains(":"))
            .ToListAsync(cancellationToken);
        tags.RemoveAll(static tag => OciReferrersIndex.IsInternalTag(tag.Tag));
        var manifests = await LoadManifestsAsync(surface, tags.Select(t => t.ManifestDigest), cancellationToken);
        var tagsByRepository = tags.ToLookup(tag => tag.RepositoryKey);
        var repositoriesById = repositories.ToDictionary(repository => repository.Key);

        return repositoryIds
            .Where(repositoriesById.ContainsKey)
            .Select(repositoryId => ToListItem(
                repositoriesById[repositoryId],
                tagsByRepository[repositoryId],
                manifests,
                effectiveKind))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<string, OciManifest>> LoadManifestsAsync(
        SurfaceRegistration surface,
        IEnumerable<string> digests,
        CancellationToken cancellationToken)
    {
        var distinctDigests = digests
            .Where(digest => !string.IsNullOrEmpty(digest))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (distinctDigests.Count == 0)
        {
            return new Dictionary<string, OciManifest>(StringComparer.OrdinalIgnoreCase);
        }

        var feedId = _feedRegistry.Feed.FeedId;
        var manifests = await _context.OciManifests
            .AsNoTracking()
            .Where(m => m.FeedId == feedId
                        && m.OciSegment == surface.OciSegment
                        && distinctDigests.Contains(m.Digest))
            .ToListAsync(cancellationToken);
        return manifests.ToDictionary(manifest => manifest.Digest, StringComparer.OrdinalIgnoreCase);
    }

    private SurfaceRegistration? ResolveSurface(string? segment) =>
        string.IsNullOrEmpty(segment)
            ? _feedRegistry.TryGetDefaultOciSurface()
            : _feedRegistry.TryGetOciSurfaceBySegment(segment);

    private SurfaceContext CreateSurfaceContext(SurfaceRegistration surface)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var publicBaseUrl = httpContext is null
            ? new Uri("http://localhost/", UriKind.Absolute)
            : _publicBaseUrlProvider.GetSurfacePublicBaseUrl(httpContext, surface.RoutePrefix);

        return new SurfaceContext(
            _feedRegistry.Feed.FeedId,
            surface.Protocol,
            surface.SurfaceId,
            surface.OciSegment,
            surface.RoutePrefix,
            publicBaseUrl);
    }

    private static OciRepositoryListItem ToListItem(
        OciRepository repository,
        IEnumerable<OciTag> tags,
        IReadOnlyDictionary<string, OciManifest> manifests,
        string effectiveKind)
    {
        var tagged = tags
            .Where(tag => !TryParseLegacySupplyChainReference(tag.Tag, out _))
            .Select(tag =>
            {
                manifests.TryGetValue(tag.ManifestDigest, out var manifest);
                return (Tag: tag, Manifest: manifest);
            })
            .Where(item => MatchesKind(GetEffectiveArtifactKind(item.Manifest), effectiveKind))
            .ToList();

        var kind = ResolveRepositoryKind(tagged.Select(item => GetEffectiveArtifactKind(item.Manifest)));
        var latest = tagged
            .OrderBy(item => item.Tag.Tag, GetTagComparer(kind))
            .ThenByDescending(item => item.Manifest?.CreatedAt ?? DateTime.MinValue)
            .FirstOrDefault();

        return new OciRepositoryListItem(
            repository.Name,
            kind,
            latest.Tag?.Tag,
            latest.Tag?.ManifestDigest,
            latest.Manifest?.PlatformOs,
            latest.Manifest?.PlatformArch,
            tagged.Count,
            latest.Manifest?.Size,
            latest.Manifest?.CreatedAt ?? repository.CreatedAt,
            latest.Tag?.Origin ?? PackageOrigin.Published);
    }

    private static OciTagListItem ToTagListItem(
        OciTag tag,
        IReadOnlyDictionary<string, OciManifest> manifests)
    {
        manifests.TryGetValue(tag.ManifestDigest, out var manifest);
        return new OciTagListItem(
            tag.Tag,
            tag.ManifestDigest,
            GetEffectiveArtifactKind(manifest),
            manifest?.Size,
            manifest?.CreatedAt,
            manifest?.PlatformOs,
            manifest?.PlatformArch,
            tag.Origin);
    }

    private static string GetEffectiveKindFilter(OciProfile profile, string? requestedKind) => profile switch
    {
        OciProfile.ContainerImages => OciCatalogKind.Docker,
        OciProfile.HelmCharts => OciCatalogKind.Helm,
        _ when requestedKind is OciCatalogKind.Docker or OciCatalogKind.Helm => requestedKind,
        _ => "All",
    };

    private static bool MatchesKind(OciArtifactKind kind, string effectiveKind) =>
        OciCatalogKind.MatchesFilter(kind, effectiveKind);

    private static IComparer<string> GetTagComparer(OciArtifactKind artifactKind) =>
        artifactKind == OciArtifactKind.Helm
            ? OciTagVersionComparer.HelmVersions
            : OciTagVersionComparer.Instance;

    private static OciArtifactKind GetEffectiveArtifactKind(OciManifest? manifest)
    {
        if (manifest is null || manifest.ArtifactKind != OciArtifactKind.Unknown)
        {
            return manifest?.ArtifactKind ?? OciArtifactKind.Unknown;
        }

        return manifest.MediaType.StartsWith(DockerManifestMediaType, StringComparison.OrdinalIgnoreCase)
               || manifest.MediaType.StartsWith(OciImageManifestMediaType, StringComparison.OrdinalIgnoreCase)
            ? OciArtifactKind.Image
            : OciArtifactKind.Unknown;
    }

    private static OciArtifactKind ResolveRepositoryKind(IEnumerable<OciArtifactKind> kinds)
    {
        var known = kinds.Where(kind => kind != OciArtifactKind.Unknown).Distinct().ToList();
        if (known.Count == 0)
        {
            return OciArtifactKind.Unknown;
        }

        if (known.All(kind => kind is OciArtifactKind.Image or OciArtifactKind.Index))
        {
            return known.Contains(OciArtifactKind.Index) ? OciArtifactKind.Index : OciArtifactKind.Image;
        }

        return known.Count == 1 ? known[0] : OciArtifactKind.Unknown;
    }

    private static bool IsDigestReference(string reference) =>
        reference.Contains(':', StringComparison.Ordinal);

    private async Task<OciDockerImageComparison?> LoadPreviousDockerComparisonAsync(
        SurfaceRegistration surface,
        SurfaceContext surfaceContext,
        int repositoryKey,
        string repositoryName,
        string currentReference,
        string currentDigest,
        OciDockerImageDetails currentDetails,
        PackageOrigin[] visibleOrigins,
        CancellationToken cancellationToken)
    {
        var storedVersions = await (
                from tag in _context.OciTags.AsNoTracking()
                join manifest in _context.OciManifests.AsNoTracking()
                    on new { tag.FeedId, tag.OciSegment, Digest = tag.ManifestDigest }
                    equals new { manifest.FeedId, manifest.OciSegment, Digest = manifest.Digest }
                where tag.FeedId == _feedRegistry.Feed.FeedId
                      && tag.OciSegment == surface.OciSegment
                      && tag.RepositoryKey == repositoryKey
                      && visibleOrigins.Contains(tag.Origin)
                      && !tag.Tag.Contains(":")
                      && (manifest.ArtifactKind == OciArtifactKind.Image
                          || manifest.ArtifactKind == OciArtifactKind.Index
                          || manifest.ArtifactKind == OciArtifactKind.Unknown)
                select new { tag.Tag, tag.ManifestDigest })
            .ToListAsync(cancellationToken);

        var versioned = new List<(string Tag, string Digest, NuGet.Versioning.NuGetVersion Version)>();
        foreach (var storedVersion in storedVersions)
        {
            if (TryParseDockerVersion(storedVersion.Tag, out var version))
            {
                versioned.Add((storedVersion.Tag, storedVersion.ManifestDigest, version!));
            }
        }

        var currentLabel = currentReference;
        NuGet.Versioning.NuGetVersion? currentVersion;
        if (!TryParseDockerVersion(currentReference, out currentVersion))
        {
            var currentCandidate = versioned
                .Where(candidate => candidate.Digest.Equals(currentDigest, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(
                    candidate => candidate.Version,
                    NuGet.Versioning.VersionComparer.VersionRelease)
                .FirstOrDefault();
            currentVersion = currentCandidate.Version;
            currentLabel = currentCandidate.Tag;
        }

        // Floating, channel, and commit tags have no reliable predecessor unless the same
        // manifest also carries a semantic version. Avoid manufacturing a misleading diff.
        if (currentVersion is null || string.IsNullOrWhiteSpace(currentLabel))
        {
            return null;
        }

        var previous = versioned
            .Where(candidate => !candidate.Digest.Equals(currentDigest, StringComparison.OrdinalIgnoreCase)
                                && NuGet.Versioning.VersionComparer.VersionRelease.Compare(
                                    candidate.Version,
                                    currentVersion) < 0)
            .OrderByDescending(
                candidate => candidate.Version,
                NuGet.Versioning.VersionComparer.VersionRelease)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(previous.Tag))
        {
            return null;
        }

        try
        {
            // Resolve a manifest already represented in the local database. Config lookup
            // remains local-only inside LoadDockerDetailsAsync, and image layers are never read.
            var previousManifest = await _registryService.GetManifestAsync(
                surfaceContext,
                repositoryName,
                previous.Digest,
                cancellationToken);
            if (previousManifest is null)
            {
                return null;
            }

            var parsed = OciManifestParser.Parse(previousManifest.MediaType, previousManifest.Content);
            if (parsed.ArtifactKind is not (OciArtifactKind.Image or OciArtifactKind.Index))
            {
                return null;
            }

            var previousDetails = await LoadDockerDetailsAsync(
                surfaceContext,
                parsed.ArtifactKind,
                previousManifest.Content,
                cancellationToken);
            return OciDockerImageComparisonBuilder.Compare(
                previous.Tag,
                previousDetails,
                currentLabel,
                currentDetails);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    private static bool TryParseDockerVersion(
        string? value,
        out NuGet.Versioning.NuGetVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value;
        if (candidate.Length > 1
            && candidate[0] is 'v' or 'V'
            && char.IsAsciiDigit(candidate[1]))
        {
            candidate = candidate[1..];
        }

        return NuGet.Versioning.NuGetVersion.TryParse(candidate, out version);
    }

    private async Task<HelmChartVersionComparison?> LoadPreviousHelmComparisonAsync(
        SurfaceRegistration surface,
        SurfaceContext surfaceContext,
        int repositoryKey,
        string repositoryName,
        string currentReference,
        string currentDigest,
        HelmChartDetails currentDetails,
        PackageOrigin[] visibleOrigins,
        CancellationToken cancellationToken)
    {
        // Chart.yaml is present only when the bounded archive inspection succeeded. Do not
        // mistake an unreadable archive for a chart that removed all of its configuration.
        if (string.IsNullOrWhiteSpace(currentDetails.ChartYaml))
        {
            return null;
        }

        var storedVersions = await (
                from tag in _context.OciTags.AsNoTracking()
                join manifest in _context.OciManifests.AsNoTracking()
                    on new { tag.FeedId, tag.OciSegment, Digest = tag.ManifestDigest }
                    equals new { manifest.FeedId, manifest.OciSegment, Digest = manifest.Digest }
                where tag.FeedId == _feedRegistry.Feed.FeedId
                      && tag.OciSegment == surface.OciSegment
                      && tag.RepositoryKey == repositoryKey
                      && visibleOrigins.Contains(tag.Origin)
                      && !tag.Tag.Contains(":")
                      && (manifest.ArtifactKind == OciArtifactKind.Helm
                          || manifest.ArtifactKind == OciArtifactKind.Unknown)
                select new { tag.Tag, tag.ManifestDigest })
            .ToListAsync(cancellationToken);

        var versioned = new List<(string Tag, string Digest, NuGet.Versioning.NuGetVersion Version)>();
        foreach (var storedVersion in storedVersions)
        {
            if (TryParseHelmVersion(storedVersion.Tag, out var version))
            {
                versioned.Add((storedVersion.Tag, storedVersion.ManifestDigest, version!));
            }
        }

        NuGet.Versioning.NuGetVersion? currentVersion = null;
        if (TryParseHelmVersion(currentDetails.Version, out var metadataVersion))
        {
            currentVersion = metadataVersion;
        }
        else if (TryParseHelmVersion(currentReference, out var referenceVersion))
        {
            currentVersion = referenceVersion;
        }
        else
        {
            currentVersion = versioned
                .Where(candidate => candidate.Digest.Equals(currentDigest, StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => candidate.Tag, OciTagVersionComparer.HelmVersions)
                .Select(candidate => candidate.Version)
                .FirstOrDefault();
        }

        var previous = versioned
            .Where(candidate => !candidate.Digest.Equals(currentDigest, StringComparison.OrdinalIgnoreCase)
                                && (currentVersion is null
                                    || NuGet.Versioning.VersionComparer.VersionRelease.Compare(
                                        candidate.Version,
                                        currentVersion) < 0))
            .OrderBy(candidate => candidate.Tag, OciTagVersionComparer.HelmVersions)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(previous.Tag))
        {
            return null;
        }

        try
        {
            // Resolve only a manifest already represented in the local database. Blob loads
            // omit repositoryName so the comparison can never trigger an upstream request.
            var previousManifest = await _registryService.GetManifestAsync(
                surfaceContext,
                repositoryName,
                previous.Digest,
                cancellationToken);
            if (previousManifest is null)
            {
                return null;
            }

            var parsed = OciManifestParser.Parse(previousManifest.MediaType, previousManifest.Content);
            if (parsed.ArtifactKind != OciArtifactKind.Helm)
            {
                return null;
            }

            var previousDetails = await HelmChartArtifactReader.ReadAsync(
                previousManifest.Content,
                (digest, token) => _registryService.GetBlobAsync(
                    surfaceContext,
                    digest,
                    repositoryName: null,
                    token),
                cancellationToken);
            if (string.IsNullOrWhiteSpace(previousDetails.ChartYaml))
            {
                return null;
            }

            return HelmChartComparisonBuilder.Compare(
                previousDetails.Version ?? previous.Tag,
                previousDetails,
                currentDetails.Version ?? currentReference,
                currentDetails);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    private static bool TryParseHelmVersion(
        string? value,
        out NuGet.Versioning.NuGetVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value;
        if (candidate.Length > 1
            && candidate[0] is 'v' or 'V'
            && char.IsAsciiDigit(candidate[1]))
        {
            candidate = candidate[1..];
        }

        return NuGet.Versioning.NuGetVersion.TryParse(candidate.Replace('_', '+'), out version);
    }

    private async Task<IReadOnlyList<OciDockerSupplyChainArtifact>> LoadSupplyChainArtifactsAsync(
        SurfaceRegistration surface,
        SurfaceContext surfaceContext,
        int repositoryKey,
        string repositoryName,
        string subjectDigest,
        PackageOrigin[] visibleOrigins,
        CancellationToken cancellationToken)
    {
        var legacyArtifacts = new List<OciDockerSupplyChainArtifact>();
        if (TryGetSha256Hex(subjectDigest, out var digestHex))
        {
            var referencePrefix = $"sha256-{digestHex}";
            var expectedReferences = new[]
            {
                $"{referencePrefix}.sig",
                $"{referencePrefix}.att",
                $"{referencePrefix}.sbom",
            };
            var storedTags = await _context.OciTags
                .AsNoTracking()
                .Where(tag => tag.FeedId == _feedRegistry.Feed.FeedId
                              && tag.OciSegment == surface.OciSegment
                              && tag.RepositoryKey == repositoryKey
                              && visibleOrigins.Contains(tag.Origin)
                              && expectedReferences.Contains(tag.Tag))
                .Select(tag => new { tag.Tag, tag.ManifestDigest })
                .ToListAsync(cancellationToken);

            legacyArtifacts.AddRange(storedTags
                .Select(tag => TryParseLegacySupplyChainReference(tag.Tag, out var kind)
                    ? new OciDockerSupplyChainArtifact(kind, tag.Tag, tag.ManifestDigest)
                    : null)
                .Where(artifact => artifact is not null)
                .Cast<OciDockerSupplyChainArtifact>());
        }

        var referrers = await _registryService.ListReferrersAsync(
            surfaceContext,
            repositoryName,
            subjectDigest,
            cancellationToken: cancellationToken);
        var modernArtifacts = new List<OciDockerSupplyChainArtifact>();
        foreach (var descriptor in referrers.Manifests)
        {
            var kind = ClassifyReferrer(descriptor.ArtifactType);
            var vulnerabilitySummary = kind == OciDockerSupplyChainArtifactKind.VulnerabilityReport
                ? await LoadVulnerabilitySummaryAsync(
                    surfaceContext,
                    repositoryName,
                    descriptor.Digest,
                    cancellationToken)
                : null;
            modernArtifacts.Add(new OciDockerSupplyChainArtifact(
                kind,
                descriptor.Digest,
                descriptor.Digest,
                descriptor.ArtifactType,
                IsOciReferrer: true,
                vulnerabilitySummary));
        }

        return modernArtifacts
            .Concat(legacyArtifacts)
            .GroupBy(static artifact => artifact.ManifestDigest, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static artifact => artifact.Kind)
            .ThenBy(static artifact => artifact.Reference, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<OciVulnerabilityReportSummary?> LoadVulnerabilitySummaryAsync(
        SurfaceContext surface,
        string repositoryName,
        string manifestDigest,
        CancellationToken cancellationToken)
    {
        var reportManifest = await _registryService.GetManifestAsync(
            surface,
            repositoryName,
            manifestDigest,
            cancellationToken);
        if (reportManifest is null)
        {
            return null;
        }

        var layers = OciDockerMetadataReader.ReadManifest(reportManifest.Content).Layers;
        foreach (var layer in layers.Where(static layer => IsJsonEvidenceLayer(layer.MediaType)))
        {
            if (layer.Size is < 0 or > OciVulnerabilityReportReader.MaximumReportBytes)
            {
                continue;
            }

            // Referrer indexes are local-only. Omitting repositoryName prevents human
            // browsing from triggering an upstream blob download for attached evidence.
            var blob = await _registryService.GetBlobAsync(
                surface,
                layer.Digest,
                repositoryName: null,
                cancellationToken);
            if (blob is null)
            {
                continue;
            }

            await using var content = blob.Content;
            var bytes = await ReadBoundedAsync(
                content,
                blob.Size ?? layer.Size,
                OciVulnerabilityReportReader.MaximumReportBytes,
                cancellationToken);
            if (bytes is not null && OciVulnerabilityReportReader.Read(bytes) is { } summary)
            {
                return summary;
            }
        }

        return null;
    }

    private static bool IsJsonEvidenceLayer(string? mediaType) =>
        mediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true;

    private static OciDockerSupplyChainArtifactKind ClassifyReferrer(string? artifactType)
    {
        var value = artifactType?.ToLowerInvariant() ?? string.Empty;
        if (value.Contains("vulnerab", StringComparison.Ordinal)
            || value.Contains("trivy", StringComparison.Ordinal)
            || value.Contains("grype", StringComparison.Ordinal)
            || value.Contains("scan", StringComparison.Ordinal))
        {
            return OciDockerSupplyChainArtifactKind.VulnerabilityReport;
        }

        if (value.Contains("spdx", StringComparison.Ordinal)
            || value.Contains("cyclonedx", StringComparison.Ordinal)
            || value.Contains("sbom", StringComparison.Ordinal))
        {
            return OciDockerSupplyChainArtifactKind.Sbom;
        }

        if (value.Contains("signature", StringComparison.Ordinal)
            || value.Contains("cosign.artifact.sig", StringComparison.Ordinal))
        {
            return OciDockerSupplyChainArtifactKind.Signature;
        }

        if (value.Contains("attestation", StringComparison.Ordinal)
            || value.Contains("in-toto", StringComparison.Ordinal)
            || value.Contains("dsse", StringComparison.Ordinal))
        {
            return OciDockerSupplyChainArtifactKind.Attestation;
        }

        return OciDockerSupplyChainArtifactKind.Other;
    }

    private static bool TryParseLegacySupplyChainReference(
        string reference,
        out OciDockerSupplyChainArtifactKind kind)
    {
        kind = default;
        if (reference.Length <= "sha256-".Length + 64
            || !reference.StartsWith("sha256-", StringComparison.OrdinalIgnoreCase)
            || !ContainsOnlyHexDigits(reference.AsSpan("sha256-".Length, 64)))
        {
            return false;
        }

        var suffix = reference[("sha256-".Length + 64)..];
        if (suffix.Equals(".sig", StringComparison.OrdinalIgnoreCase))
        {
            kind = OciDockerSupplyChainArtifactKind.Signature;
            return true;
        }

        if (suffix.Equals(".att", StringComparison.OrdinalIgnoreCase))
        {
            kind = OciDockerSupplyChainArtifactKind.Attestation;
            return true;
        }

        if (suffix.Equals(".sbom", StringComparison.OrdinalIgnoreCase))
        {
            kind = OciDockerSupplyChainArtifactKind.Sbom;
            return true;
        }

        return false;
    }

    private static bool TryGetSha256Hex(string digest, out string hex)
    {
        const string prefix = "sha256:";
        if (digest.Length == prefix.Length + 64
            && digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && ContainsOnlyHexDigits(digest.AsSpan(prefix.Length)))
        {
            hex = digest[prefix.Length..].ToLowerInvariant();
            return true;
        }

        hex = string.Empty;
        return false;
    }

    private static bool ContainsOnlyHexDigits(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static int CountReferencedArtifacts(
        OciArtifactKind artifactKind,
        byte[] content,
        int fallback)
    {
        var propertyName = artifactKind == OciArtifactKind.Index ? "manifests" : "layers";
        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.TryGetProperty(propertyName, out var references)
                   && references.ValueKind == JsonValueKind.Array
                ? references.GetArrayLength()
                : 0;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private async Task<OciDockerImageDetails> LoadDockerDetailsAsync(
        SurfaceContext surface,
        OciArtifactKind artifactKind,
        byte[] manifestContent,
        CancellationToken cancellationToken)
    {
        var manifest = OciDockerMetadataReader.ReadManifest(manifestContent);
        OciDockerImageConfiguration? configuration = null;
        if (artifactKind == OciArtifactKind.Image && !string.IsNullOrEmpty(manifest.ConfigDigest))
        {
            // Omitting repositoryName is intentional: IOciRegistryService only consults its
            // upstream mirror when a repository is supplied. Human browsing must remain local.
            var blob = await _registryService.GetBlobAsync(
                surface,
                manifest.ConfigDigest,
                repositoryName: null,
                cancellationToken);
            if (blob is not null)
            {
                await using var content = blob.Content;
                var bytes = await ReadBoundedAsync(
                    content,
                    blob.Size,
                    OciDockerMetadataReader.MaximumConfigBytes,
                    cancellationToken);
                if (bytes is not null)
                {
                    configuration = OciDockerMetadataReader.ReadConfiguration(bytes);
                }
            }
        }

        return OciDockerMetadataReader.ToDetails(manifest, configuration);
    }

    private static async Task<byte[]?> ReadBoundedAsync(
        Stream content,
        long? declaredSize,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (declaredSize is < 0 || declaredSize > maximumBytes)
        {
            return null;
        }

        var initialCapacity = declaredSize.HasValue
            ? (int)Math.Min(declaredSize.Value, maximumBytes)
            : 16 * 1024;
        using var buffer = new MemoryStream(initialCapacity);
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await content.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maximumBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
    }
}
