using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using AvantiPoint.Packages.Registry.Oci;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.UI.Services;

public sealed class OciRepositoryBrowseService : IOciRepositoryBrowseService
{
    private readonly IFeedRegistry _feedRegistry;
    private readonly IOciRegistryService _registryService;
    private readonly IContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPublicBaseUrlProvider _publicBaseUrlProvider;

    public OciRepositoryBrowseService(
        IFeedRegistry feedRegistry,
        IOciRegistryService registryService,
        IContext context,
        IHttpContextAccessor httpContextAccessor,
        IPublicBaseUrlProvider publicBaseUrlProvider)
    {
        _feedRegistry = feedRegistry;
        _registryService = registryService;
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _publicBaseUrlProvider = publicBaseUrlProvider;
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

        var context = CreateSurfaceContext(surface);
        var catalog = await _registryService.ListCatalogAsync(context, max, last, cancellationToken);
        return catalog.Repositories
            .Select(name => new OciRepositoryListItem(name))
            .ToList();
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

        var context = CreateSurfaceContext(surface);
        var tags = await _registryService.ListTagsAsync(context, repositoryName, max, last, cancellationToken);
        if (tags is null)
        {
            return null;
        }

        return new OciRepositoryTagsModel(tags.Name, tags.Tags);
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

        var context = CreateSurfaceContext(surface);
        var manifest = await _registryService.GetManifestAsync(context, repositoryName, reference, cancellationToken);
        if (manifest is null)
        {
            return null;
        }

        var feedId = _feedRegistry.Feed.FeedId;
        var record = await _context.OciManifests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.FeedId == feedId
                     && m.OciSegment == surface.OciSegment
                     && m.Digest == manifest.Digest,
                cancellationToken);

        var tags = await _context.OciTags
            .AsNoTracking()
            .Where(t => t.FeedId == feedId
                        && t.OciSegment == surface.OciSegment
                        && t.ManifestDigest == manifest.Digest)
            .Join(
                _context.OciRepositories.AsNoTracking(),
                tag => tag.RepositoryKey,
                repo => repo.Key,
                (tag, repo) => new { tag.Tag, repo.Name })
            .Where(x => x.Name == repositoryName)
            .Select(x => x.Tag)
            .OrderBy(t => t)
            .ToListAsync(cancellationToken);

        var registryRoot = GetRegistryRootUrl(segment);

        return new OciArtifactDetailModel(
            repositoryName,
            reference,
            manifest.Digest,
            manifest.MediaType,
            record?.ArtifactKind ?? OciArtifactKind.Unknown,
            record?.PlatformOs,
            record?.PlatformArch,
            record?.Size ?? manifest.Content.LongLength,
            tags,
            registryRoot);
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
}
