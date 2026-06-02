namespace AvantiPoint.Packages.Registry.Oci;

public sealed record OciUpstreamManifest(string Digest, string MediaType, byte[] Content);
