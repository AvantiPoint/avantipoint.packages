namespace AvantiPoint.Packages.Registry.Npm;

public sealed class NpmPublishSizeLimitExceededException : Exception
{
    public NpmPublishSizeLimitExceededException(string message)
        : base(message)
    {
    }
}
