namespace AvantiPoint.Packages.Core;

/// <summary>
/// Resolves the active logical feed identifier for the current deployment scope.
/// </summary>
public interface IFeedScope
{
    string FeedId { get; }
}
