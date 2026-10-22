namespace AvantiPoint.Packages.Core.Entities.Oci;

/// <summary>
/// A persistent lock target shared by OCI publication and garbage collection.
/// The row's database write lock is held until its transaction finishes, so a
/// process crash releases it without an expiring lease or stale lock owner.
/// </summary>
public class OciMutationGate
{
    public string FeedId { get; set; } = FeedConstants.DefaultFeedId;
    public string Segment { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
}
