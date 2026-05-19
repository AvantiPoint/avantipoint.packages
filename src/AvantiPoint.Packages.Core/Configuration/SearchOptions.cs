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
}
