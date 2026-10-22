namespace AvantiPoint.Packages.Core;

/// <summary>
/// Resolves the active logical feed identifier for the current deployment scope.
/// </summary>
public interface IFeedScope
{
    string FeedId { get; }

    /// <summary>
    /// Storage namespace assigned to this logical feed. An empty value preserves the legacy
    /// single-feed storage layout.
    /// </summary>
    string StoragePrefix => string.Empty;
}
