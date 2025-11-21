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
    public class DefaultPackageContentService : IPackageContentService
    {
        private readonly IMirrorService _mirror;
        private readonly IPackageService _packages;
        private readonly IPackageStorageService _storage;
        private readonly IRepositorySigningKeyProvider _signingKeyProvider;
        private readonly IPackageSigningService _signingService;
        private readonly PackageSignatureStripper _signatureStripper;
        private readonly Microsoft.Extensions.Options.IOptions<Signing.SigningOptions> _signingOptions;
        private readonly ILogger<DefaultPackageContentService> _logger;

        public DefaultPackageContentService(
            IMirrorService mirror,
            IPackageService packages,
            IPackageStorageService storage,
            IRepositorySigningKeyProvider signingKeyProvider,
            IPackageSigningService signingService,
            PackageSignatureStripper signatureStripper,
            Microsoft.Extensions.Options.IOptions<Signing.SigningOptions> signingOptions,
            ILogger<DefaultPackageContentService> logger)
        {
            _mirror = mirror ?? throw new ArgumentNullException(nameof(mirror));
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _signingKeyProvider = signingKeyProvider ?? throw new ArgumentNullException(nameof(signingKeyProvider));
            _signingService = signingService ?? throw new ArgumentNullException(nameof(signingService));
            _signatureStripper = signatureStripper ?? throw new ArgumentNullException(nameof(signatureStripper));
            _signingOptions = signingOptions ?? throw new ArgumentNullException(nameof(signingOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PackageVersionsResponse> GetPackageVersionsOrNullAsync(
            string id,
            CancellationToken cancellationToken = default)
        {
            // First, attempt to find all package versions using the upstream source.
            var versions = await _mirror.FindPackageVersionsOrNullAsync(id, cancellationToken);

            versions ??= await _packages.FindVersionsAsync(id, true, cancellationToken);

            return new PackageVersionsResponse
            {
                Versions = versions
                    .Select(v => v.ToNormalizedString())
                    .Select(v => v.ToLowerInvariant())
                    .ToList()
            };
        }

        public async Task<Stream> GetPackageContentStreamOrNullAsync(
            string id,
            NuGetVersion version,
            CancellationToken cancellationToken = default)
        {
            // Allow read-through caching if it is configured.
            await _mirror.MirrorAsync(id, version, cancellationToken);

            if (!await _packages.AddDownloadAsync(id, version, cancellationToken))
            {
                return null;
            }

            // Check if repository signing is enabled
            var signingEnabled = _signingKeyProvider is not INullSigningKeyProvider;

            // If signing is enabled, try to serve the signed version
            if (signingEnabled)
            {
                // First, check if a signed version already exists
                var signedStream = await _storage.GetSignedPackageStreamOrNullAsync(id, version, cancellationToken);
                if (signedStream != null)
                {
                    _logger.LogDebug(
                        "Serving signed package {PackageId} {PackageVersion} from storage",
                        id,
                        version.ToNormalizedString());
                    return signedStream;
                }

                // No signed version exists, sign on-demand
                _logger.LogInformation(
                    "Signed package {PackageId} {PackageVersion} not found, signing on-demand",
                    id,
                    version.ToNormalizedString());

                var unsignedStream = await _storage.GetPackageStreamAsync(id, version, cancellationToken);
                if (unsignedStream == null)
                {
                    return null;
                }

                try
                {
                    // Get the signing certificate
                    var certificate = await _signingKeyProvider.GetSigningCertificateAsync(cancellationToken);
                    if (certificate == null)
                    {
                        _logger.LogWarning(
                            "Repository signing is enabled but no certificate is available for {PackageId} {PackageVersion}",
                            id,
                            version.ToNormalizedString());
                        // Fall back to unsigned
                        return unsignedStream;
                    }

                    // Check if package already has a repository signature (e.g., from nuget.org)
                    unsignedStream.Position = 0;
                    var hasExistingRepositorySignature = await _signingService.IsPackageSignedAsync(unsignedStream, cancellationToken);
                    
                    Stream streamToSign = unsignedStream;
                    if (hasExistingRepositorySignature)
                    {
                        switch (_signingOptions.Value.UpstreamSignature)
                        {
                            case UpstreamSignatureBehavior.Reject:
                                // Strict mode: cannot sign packages with existing repository signatures on-demand
                                // Fall back to unsigned package
                                _logger.LogWarning(
                                    "Package {PackageId} {PackageVersion} already has a repository signature and UpstreamSignature is Reject. " +
                                    "Serving unsigned package.",
                                    id,
                                    version.ToNormalizedString());
                                unsignedStream.Position = 0;
                                return unsignedStream;

                            case UpstreamSignatureBehavior.ReSign:
                            default:
                                _logger.LogInformation(
                                    "Package {PackageId} {PackageVersion} already has a repository signature, stripping it before adding our own",
                                    id,
                                    version.ToNormalizedString());
                                
                                // Strip existing repository signature (preserves author signatures)
                                unsignedStream.Position = 0;
                                streamToSign = await _signatureStripper.StripRepositorySignaturesAsync(unsignedStream, cancellationToken);
                                break;
                        }
                    }

                    // Sign the package with our repository signature
                    streamToSign.Position = 0;
                    var signedPackageStream = await _signingService.SignPackageAsync(
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
                            await _storage.SaveSignedPackageAsync(id, version, copyStream, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Failed to save signed package {PackageId} {PackageVersion} to storage",
                                id,
                                version.ToNormalizedString());
                        }
                        finally
                        {
                            await copyStream.DisposeAsync();
                        }
                    }, cancellationToken);

                    _logger.LogInformation(
                        "Successfully signed package {PackageId} {PackageVersion} on-demand",
                        id,
                        version.ToNormalizedString());

                    return signedPackageStream;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to sign package {PackageId} {PackageVersion}, falling back to unsigned",
                        id,
                        version.ToNormalizedString());

                    // Ensure the unsigned stream is at the beginning
                    unsignedStream.Position = 0;
                    return unsignedStream;
                }
            }

            // Signing is disabled or not configured, serve unsigned
            return await _storage.GetPackageStreamAsync(id, version, cancellationToken);
        }

        public async Task<Stream> GetPackageManifestStreamOrNullAsync(string id, NuGetVersion version, CancellationToken cancellationToken = default)
        {
            // Allow read-through caching if it is configured.
            await _mirror.MirrorAsync(id, version, cancellationToken);

            if (!await _packages.ExistsAsync(id, version, cancellationToken))
            {
                return null;
            }

            return await _storage.GetNuspecStreamAsync(id, version, cancellationToken);
        }

        public async Task<Stream> GetPackageReadmeStreamOrNullAsync(string id, NuGetVersion version, CancellationToken cancellationToken = default)
        {
            // Allow read-through caching if it is configured.
            await _mirror.MirrorAsync(id, version, cancellationToken);

            var package = await _packages.FindOrNullAsync(id, version, includeUnlisted: true, cancellationToken);
            if (!package.HasReadme)
            {
                return null;
            }

            return await _storage.GetReadmeStreamAsync(id, version, cancellationToken);
        }

        public async Task<Stream> GetPackageIconStreamOrNullAsync(string id, NuGetVersion version, CancellationToken cancellationToken = default)
        {
            // Allow read-through caching if it is configured.
            await _mirror.MirrorAsync(id, version, cancellationToken);

            var package = await _packages.FindOrNullAsync(id, version, includeUnlisted: true, cancellationToken);
            if (!package.HasEmbeddedIcon)
            {
                return null;
            }

            return await _storage.GetIconStreamAsync(id, version, cancellationToken);
        }

        public async Task<Stream> GetPackageLicenseStreamOrNullAsync(string id, NuGetVersion version, CancellationToken cancellationToken = default)
        {
            // Allow read-through caching if it is configured.
            await _mirror.MirrorAsync(id, version, cancellationToken);

            var package = await _packages.FindOrNullAsync(id, version, includeUnlisted: true, cancellationToken);
            if (!package.HasEmbeddedLicense)
            {
                return null;
            }

            return await _storage.GetLicenseStreamAsync(id, version, cancellationToken);
        }
    }
}
