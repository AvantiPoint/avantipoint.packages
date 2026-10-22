namespace AvantiPoint.Packages.Registry.Npm;

public sealed class NpmTarballSizeLimitExceededException(long limit)
    : Exception($"The upstream package exceeds this feed's tarball limit of {limit} bytes. Ask an administrator to review Feed:Npm:MaxTarballBytes.");
