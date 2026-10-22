namespace AvantiPoint.Packages.UI.Services;

/// <summary>
/// Retrieves original source text referenced by package-controlled SourceLink metadata.
/// </summary>
public interface ISourceLinkContentFetcher
{
    Task<string?> FetchAsync(Uri uri, CancellationToken cancellationToken);
}
