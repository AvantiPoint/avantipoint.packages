namespace AvantiPoint.Packages.Registry.Oci;

internal sealed record OciAccessTicket(
    string FeedId,
    string SurfaceId,
    string Credential,
    OciAccessGrant[] Grants);
