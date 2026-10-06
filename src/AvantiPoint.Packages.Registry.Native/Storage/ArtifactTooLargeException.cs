namespace AvantiPoint.Packages.Registry.Native.Storage;

public sealed class ArtifactTooLargeException : IOException
{
    public ArtifactTooLargeException() : base("Artifact exceeds the configured size limit.") { }
}
