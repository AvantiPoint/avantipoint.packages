#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core.Signing;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// Implements the NuGet Package Content resource. Supports read-through caching.
    /// Tracks state in a database (<see cref="IPackageService"/>) and stores packages
    /// using <see cref="IPackageStorageService"/>.
    /// </summary>
    public class DefaultPackageContentService(
        IMirrorService mirror,
        IPackageService packages,
        IPackageStorageService storage,
        IRepositorySigningKeyProvider signingKeyProvider,
        IPackageSigningService signingService,
        PackageSignatureStripper signatureStripper,
        Microsoft.Extensions.Options.IOptions<Signing.SigningOptions> signingOptions,
        IPackageSourceService packageSources,
        Signing.RepositorySigningCertificateService certificateService,
        ILogger<DefaultPackageContentService> logger) : IPackageContentService
    {

        public async Task<PackageVersionsResponse?> GetPackageVersionsOrNullAsync(
            string id,
            CancellationToken cancellationToken = default)
        {
            // First, attempt to find all package versions using the upstream source.
            var versions = await mirror.FindPackageVersionsOrNullAsync(id, cancellationToken);

            versions ??= await packages.FindVersionsAsync(id, true, cancellationToken);

            if (versions.Count == 0)
            {
                return null;
            }

            return new PackageVersionsResponse
            {
                Versions = versions
                    .Select(v => v.ToNormalizedString())
                    .Select(v => v.ToLowerInvariant())
                    .ToList()
            };
        }

        public async Task<Stream?> GetPackageContentStreamOrNullAsync(
            string id,
            NuGetVersion version,
            CancellationToken cancellationToken = default)
        {
            // Allow read-through caching if it is configured.
            var mirrorResult = await mirror.MirrorAsync(id, version, cancellationToken);
            if (mirrorResult.IsProxied)
            {
                if (mirrorResult.ProxiedStream == null)
                {
                    return null;
                }

                mirrorResult.ProxiedStream.Position = 0;
                return mirrorResult.ProxiedStream;
            }

            if (!await packages.AddDownloadAsync(id, version, cancellationToken))
            {
                return null;
            }

            var packageEntity = await packages.FindOrNullAsync(id, version, includeUnlisted: true, cancellationToken);
            if (packageEntity == null)
            {
                return null;
            }

            var mirrorPolicy = MirrorRepositorySignaturePolicy.Resign;
            if (packageEntity.PackageSourceId.HasValue)
            {
                var source = await packageSources.GetRequiredAsync(packageEntity.PackageSourceId.Value, cancellationToken);
                mirrorPolicy = source.MirrorSignaturePolicy;
            }

            // Check if repository signing is enabled
            var signingEnabled = signingKeyProvider is not INullSigningKeyProvider;

            // If signing is enabled, try to serve the signed version
            if (signingEnabled)
            {
                // First, check if a signed version already exists
                var signedStream = await storage.GetSignedPackageStreamOrNullAsync(id, version, cancellationToken);
                if (signedStream != null)
                {
                    logger.LogDebug(
                        "Serving signed package {PackageId} {PackageVersion} from storage",
                        id,
                        version.ToNormalizedString());
                    return signedStream;
                }

                // No signed version exists, sign on-demand
                logger.LogInformation(
                    "Signed package {PackageId} {PackageVersion} not found, signing on-demand",
                    id,
                    version.ToNormalizedString());

                var unsignedStream = await storage.GetPackageStreamAsync(id, version, cancellationToken);
                if (unsignedStream == null)
                {
                    return null;
                }

                var applyPublishPolicy = packageEntity.Origin == PackageOrigin.Published || packageEntity.PackageSourceId == null;

                try
                {
                    // Get the signing certificate
                    var certificate = await signingKeyProvider.GetSigningCertificateAsync(cancellationToken);
                    if (certificate == null)
                    {
                        logger.LogWarning(
                            "Repository signing is enabled but no certificate is available for {PackageId} {PackageVersion}",
                            id,
                            version.ToNormalizedString());
                        // Fall back to unsigned
                        return unsignedStream;
                    }

                    // Check if package already has a repository signature (e.g., from nuget.org)
                    unsignedStream.Position = 0;
                    var hasExistingRepositorySignature = await signingService.IsPackageSignedAsync(unsignedStream, cancellationToken);
                    
                    Stream streamToSign = unsignedStream;
                    if (hasExistingRepositorySignature)
                    {
                        if (applyPublishPolicy)
                        {
                            switch (signingOptions.Value.PublishSignaturePolicy)
                            {
                                case UpstreamSignatureBehavior.Reject:
                                    logger.LogWarning(
                                        "Package {PackageId} {PackageVersion} already has a repository signature and PublishSignaturePolicy is Reject. " +
                                        "Serving unsigned package.",
                                        id,
                                        version.ToNormalizedString());
                                    unsignedStream.Position = 0;
                                    return unsignedStream;

                                case UpstreamSignatureBehavior.ReSign:
                                default:
                                    logger.LogInformation(
                                        "Package {PackageId} {PackageVersion} already has a repository signature, stripping it before adding our own",
                                        id,
                                        version.ToNormalizedString());
                                    
                                    unsignedStream.Position = 0;
                                    streamToSign = await signatureStripper.StripRepositorySignaturesAsync(unsignedStream, cancellationToken);
                                    break;
                            }
                        }
                        else
                        {
                            switch (mirrorPolicy)
                            {
                                case MirrorRepositorySignaturePolicy.Merge:
                                    unsignedStream.Position = 0;
                                    var mergeCertificate = await RepositorySignatureInspector.TryGetRepositoryCertificateAsync(unsignedStream, logger, cancellationToken);
                                    if (mergeCertificate != null)
                                    {
                                        await certificateService.RecordCertificateUsageAsync(mergeCertificate, cancellationToken);
                                    }
                                    return unsignedStream;

                                case MirrorRepositorySignaturePolicy.TrustedCerts:
                                    unsignedStream.Position = 0;
                                    var trustedCertificate = await RepositorySignatureInspector.TryGetRepositoryCertificateAsync(unsignedStream, logger, cancellationToken);
                                    if (trustedCertificate != null &&
                                        await certificateService.IsCertificateTrustedAsync(trustedCertificate, CertificateHashAlgorithm.Sha256, cancellationToken))
                                    {
                                        return unsignedStream;
                                    }

                                    logger.LogInformation(
                                        "Repository signature for package {PackageId} {PackageVersion} is not trusted, re-signing",
                                        id,
                                        version.ToNormalizedString());
                                    unsignedStream.Position = 0;
                                    streamToSign = await signatureStripper.StripRepositorySignaturesAsync(unsignedStream, cancellationToken);
                                    break;

                                case MirrorRepositorySignaturePolicy.Resign:
                                default:
                                    unsignedStream.Position = 0;
                                    streamToSign = await signatureStripper.StripRepositorySignaturesAsync(unsignedStream, cancellationToken);
                                    break;
                            }
                        }
                    }

                    // Sign the package with our repository signature
                    streamToSign.Position = 0;
                    var signedPackageStream = await signingService.SignPackageAsync(
                        id,
                        version,
                        streamToSign,
                        certificate,
                        cancellationToken);

                    // Save the signed copy for future downloads
                    signedPackageStream.Position = 0;
                    var copyStream = new MemoryStream();
                    await signedPackageStream.CopyToAsync(copyStream, cancellationToken);
                    copyStream.Position = 0;
                    signedPackageStream.Position = 0;

                    // Save asynchronously without blocking the response
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await storage.SaveSignedPackageAsync(id, version, copyStream, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex,
                                "Failed to save signed package {PackageId} {PackageVersion} to storage",
                                id,
                                version.ToNormalizedString());
                        }
                        finally
                        {
                            await copyStream.DisposeAsync();
                        }
                    }, cancellationToken);

                    logger.LogInformation(
                        "Successfully signed package {PackageId} {PackageVersion} on-demand",
                        id,
                        version.ToNormalizedString());

                    return signedPackageStream;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to sign package {PackageId} {PackageVersion}, falling back to unsigned",
                        id,
                        version.ToNormalizedString());

                    // Ensure the unsigned stream is at the beginning
                    unsignedStream.Position = 0;
                    return unsignedStream;
                }
            }

            // Signing is disabled or not configured, serve unsigned
            return await storage.GetPackageStreamAsync(id, version, cancellationToken);
        }

        public async Task<Stream?> GetPackageManifestStreamOrNullAsync(string id, NuGetVersion version, CancellationToken cancellationToken = default)
        {
            // Allow read-through caching if it is configured.
            await mirror.MirrorAsync(id, version, cancellationToken);

            if (!await packages.ExistsAsync(id, version, cancellationToken))
            {
                return null;
            }

            return await storage.GetNuspecStreamAsync(id, version, cancellationToken);
        }

        public async Task<Stream?> GetPackageReadmeStreamOrNullAsync(string id, NuGetVersion version, CancellationToken cancellationToken = default)
        {
            // Allow read-through caching if it is configured.
            await mirror.MirrorAsync(id, version, cancellationToken);

            var package = await packages.FindOrNullAsync(id, version, includeUnlisted: true, cancellationToken);
            if (package == null || !package.HasReadme)
            {
                return null;
            }

            return await storage.GetReadmeStreamAsync(id, version, cancellationToken);
        }

        public async Task<Stream?> GetPackageIconStreamOrNullAsync(string id, NuGetVersion version, CancellationToken cancellationToken = default)
        {
            // Allow read-through caching if it is configured.
            await mirror.MirrorAsync(id, version, cancellationToken);

            var package = await packages.FindOrNullAsync(id, version, includeUnlisted: true, cancellationToken);
            if (package == null || !package.HasEmbeddedIcon)
            {
                return null;
            }

            return await storage.GetIconStreamAsync(id, version, cancellationToken);
        }

        public async Task<Stream?> GetPackageLicenseStreamOrNullAsync(string id, NuGetVersion version, CancellationToken cancellationToken = default)
        {
            // Allow read-through caching if it is configured.
            await mirror.MirrorAsync(id, version, cancellationToken);

            var package = await packages.FindOrNullAsync(id, version, includeUnlisted: true, cancellationToken);
            if (package == null || !package.HasEmbeddedLicense)
            {
                return null;
            }

            return await storage.GetLicenseStreamAsync(id, version, cancellationToken);
        }
    }
}
