namespace AvantiPoint.Packages.Registry.Oci;

internal sealed record OciAccessGrant(string Repository, string[] Actions, string ResourceType = "repository");
