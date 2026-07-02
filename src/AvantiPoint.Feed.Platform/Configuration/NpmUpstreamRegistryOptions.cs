namespace AvantiPoint.Feed.Platform.Configuration;

/// <summary>
/// An upstream npm registry used for pull-through mirroring. Supports authenticated
/// registries (for example FontAwesome Pro or Telerik npm) via a bearer token
/// (npm's <c>_authToken</c>) or basic credentials.
/// </summary>
public class NpmUpstreamRegistryOptions
{
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Bearer token sent as <c>Authorization: Bearer {Token}</c> (equivalent to npm's
    /// <c>//registry/:_authToken</c>). Takes precedence over <see cref="Username"/>/<see cref="Password"/>.
    /// </summary>
    public string? Token { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>Registries are tried in ascending priority order; the first hit wins.</summary>
    public int Priority { get; set; }
}
