using System;

namespace AvantiPoint.Packages.Core;

public class SearchOptions
{
    /// <summary>
    /// Search provider type: Database, Null, AzureSearch, OpenSearch, or Elasticsearch.
    /// </summary>
    public string Type { get; set; } = "Database";

    /// <summary>
    /// Batch size for background search index reconciliation.
    /// </summary>
    public int ReconcileBatchSize { get; set; } = 100;

    /// <summary>
    /// When false, search and registration discovery include only <see cref="PackageOrigin.Published"/> packages.
    /// When true (default), include published and mirrored packages (never cached-only rows).
    /// </summary>
    public bool IncludeMirroredPackages { get; set; } = true;

    /// <summary>
    /// When true, NuGet search results are merged with live results from enabled upstream sources.
    /// </summary>
    public bool EnableUpstreamSearch { get; set; }

    /// <summary>
    /// Maximum time allowed for the complete upstream search operation. A timeout returns local
    /// results without failing the client request.
    /// </summary>
    public TimeSpan UpstreamSearchTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Controls how local and upstream search results are combined.
    /// </summary>
    public FederatedSearchMergeStrategy MergeStrategy { get; set; } = FederatedSearchMergeStrategy.LocalPreferred;
}
