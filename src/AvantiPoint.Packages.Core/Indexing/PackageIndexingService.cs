#nullable enable

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using CoreSigningOptions = AvantiPoint.Packages.Core.Signing.SigningOptions;

namespace AvantiPoint.Packages.Core
{
    public class PackageIndexingService : IPackageIndexingService
    {
        private readonly IPackageService _packages;
        private readonly IPackageStorageService _storage;
        private readonly ISearchIndexingService _search;
        private readonly TimeProvider _time;
        private readonly IOptionsSnapshot<PackageFeedOptions> _options;
        private readonly IRepositorySigningKeyProvider _signingKeyProvider;
        private readonly IPackageSigningService _signingService;
        private readonly PackageSignatureStripper _signatureStripper;
        private readonly IOptions<CoreSigningOptions> _signingOptions;
        private readonly Signing.RepositorySigningCertificateService _certificateService;
        private readonly ILogger<PackageIndexingService> _logger;
        private readonly IFeedScope _feedScope;

        public PackageIndexingService(
            IPackageService packages,
            IPackageStorageService storage,
            ISearchIndexingService search,
            TimeProvider time,
            IOptionsSnapshot<PackageFeedOptions> options,
            IRepositorySigningKeyProvider signingKeyProvider,
            IPackageSigningService signingService,
            PackageSignatureStripper signatureStripper,
            IOptions<CoreSigningOptions> signingOptions,
            Signing.RepositorySigningCertificateService certificateService,
            ILogger<PackageIndexingService> logger,
            IFeedScope feedScope)
        {
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _feedScope = feedScope ?? throw new ArgumentNullException(nameof(feedScope));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _signingKeyProvider = signingKeyProvider ?? throw new ArgumentNullException(nameof(signingKeyProvider));
            _signingService = signingService ?? throw new ArgumentNullException(nameof(signingService));
            _signatureStripper = signatureStripper ?? throw new ArgumentNullException(nameof(signatureStripper));
            _signingOptions = signingOptions ?? throw new ArgumentNullException(nameof(signingOptions));
            _certificateService = certificateService ?? throw new ArgumentNullException(nameof(certificateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PackageIndexingResult> IndexAsync(
            Stream packageStream,
            PackageIngestionContext? ingestionContext,
            CancellationToken cancellationToken)
        {
            ingestionContext ??= new PackageIngestionContext();

            // Try to extract all the necessary information from the package.
            Package package;
            Stream nuspecStream;
            Stream? readmeStream;
            Stream? iconStream;
            Stream? licenseStream;

            try
            {
                using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
                package = await packageReader.GetPackageMetadata();
                package.FeedId = _feedScope.FeedId;
                package.Origin = ingestionContext.Origin;
                package.PackageSourceId = ingestionContext.PackageSourceId;

                // For unsigned packages, use the current time as the published date.
                // For signed packages, GetPackageMetadata already set the Published date to the signature timestamp.
                if (!package.IsSigned)
                {
                    package.Published = _time.GetUtcNow().DateTime;
                }

                nuspecStream = await packageReader.GetNuspecAsync(cancellationToken);
                nuspecStream = await nuspecStream.AsTemporaryFileStreamAsync();

                if (package.HasReadme)
                {
                    readmeStream = await packageReader.GetReadmeAsync(cancellationToken);
                    readmeStream = await readmeStream.AsTemporaryFileStreamAsync();
                }
                else
                {
                    readmeStream = null;
                }

                if (package.HasEmbeddedIcon)
                {
                    iconStream = await packageReader.GetIconAsync(cancellationToken);
                    iconStream = await iconStream.AsTemporaryFileStreamAsync();
                }
                else
                {
                    iconStream = null;
                }

                if (package.HasEmbeddedLicense)
                {
                    licenseStream = await packageReader.GetLicenseAsync(cancellationToken);
                    licenseStream = await licenseStream.AsTemporaryFileStreamAsync();
                }
                else
                {
                    licenseStream = null;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Uploaded package is invalid");
                return new() { Status = PackageIndexingStatus.InvalidPackage };
            }

            // The package is well-formed. Ensure this is a new package.
            if (await _packages.ExistsAsync(package.Id, package.Version, cancellationToken))
            {
                if (!_options.Value.AllowPackageOverwrites)
                {
                    return new() { Status = PackageIndexingStatus.PackageAlreadyExists };
                }

                await _packages.HardDeletePackageAsync(package.Id, package.Version, cancellationToken);
                await _storage.DeleteAsync(package.Id, package.Version, cancellationToken);
            }

            // TODO: Add more package validations
            // TODO: Call PackageArchiveReader.ValidatePackageEntriesAsync
            _logger.LogInformation(
                "Validated package {PackageId} {PackageVersion}, persisting content to storage...",
                package.Id,
                package.NormalizedVersionString);

            // For stored certificates, validate certificate BEFORE saving to ensure upload fails if expired
            var signingEnabled = _signingKeyProvider is not INullSigningKeyProvider;
            var isStoredCertificateMode = signingEnabled &&
                string.Equals(
                    _signingOptions.Value.Provider,
                    SigningProviderNames.StoredCertificate,
                    StringComparison.OrdinalIgnoreCase);
            
            if (isStoredCertificateMode)
            {
                try
                {
                    var certificate = await _signingKeyProvider.GetSigningCertificateAsync(cancellationToken);
                    if (certificate == null)
                    {
                        var errorMessage = "Repository signing is enabled with StoredCertificate mode but no certificate is available. " +
                            "Package upload cannot proceed.";
                        _logger.LogError(errorMessage);
                        throw new InvalidOperationException(errorMessage);
                    }
                    
                    _logger.LogInformation(
                        "Stored certificate validated successfully before package upload. Thumbprint: {Thumbprint}",
                        certificate.Thumbprint);
                }
                catch (InvalidOperationException)
                {
                    // Re-throw InvalidOperationException (e.g., expired certificate) to fail upload
                    throw;
                }
                catch (Exception ex)
                {
                    var errorMessage = "Failed to validate stored certificate before package upload. " +
                        "Package upload cannot proceed.";
                    _logger.LogError(ex, errorMessage);
                    throw new InvalidOperationException(errorMessage, ex);
                }
            }

            try
            {
                packageStream.Position = 0;

                await _storage.SavePackageContentAsync(
                    package,
                    packageStream,
                    nuspecStream,
                    readmeStream,
                    iconStream,
                    licenseStream,
                    cancellationToken);
            }
            catch (Exception e)
            {
                // This may happen due to concurrent pushes.
                // TODO: Make IPackageStorageService.SavePackageContentAsync return a result enum so this
                // can be properly handled.
                _logger.LogError(
                    e,
                    "Failed to persist package {PackageId} {PackageVersion} content to storage",
                    package.Id,
                    package.NormalizedVersionString);

                throw;
            }

            if (ingestionContext.SkipDatabasePersistence)
            {
                _logger.LogInformation(
                    "Persisted package {Id} {Version} content to storage; skipping database persistence due to caching strategy {Strategy}",
                    package.Id,
                    package.NormalizedVersionString,
                    ingestionContext.CachingStrategy);

                return new PackageIndexingResult
                {
                    PackageId = package.Id,
                    PackageVersion = package.OriginalVersionString,
                    Status = PackageIndexingStatus.Success,
                };
            }

            _logger.LogInformation(
                "Persisted package {Id} {Version} content to storage, saving metadata to database...",
                package.Id,
                package.NormalizedVersionString);

            var result = await _packages.AddAsync(package, cancellationToken);
            if (result == PackageAddResult.PackageAlreadyExists)
            {
                _logger.LogWarning(
                    "Package {Id} {Version} metadata already exists in database",
                    package.Id,
                    package.NormalizedVersionString);

                return new() { Status = PackageIndexingStatus.PackageAlreadyExists };
            }

            if (result != PackageAddResult.Success)
            {
                _logger.LogError($"Unknown {nameof(PackageAddResult)} value: {{PackageAddResult}}", result);

                throw new InvalidOperationException($"Unknown {nameof(PackageAddResult)} value: {result}");
            }

            _logger.LogInformation(
                "Successfully persisted package {Id} {Version} metadata to database. Indexing in search...",
                package.Id,
                package.NormalizedVersionString);

            if (!ingestionContext.SkipSearchIndexing)
            {
                await _search.IndexAsync(package, cancellationToken);

                _logger.LogInformation(
                    "Successfully indexed package {Id} {Version} in search",
                    package.Id,
                    package.NormalizedVersionString);
            }
            else
            {
                _logger.LogInformation(
                    "Skipping search indexing for package {Id} {Version} due to caching strategy {Strategy}",
                    package.Id,
                    package.NormalizedVersionString,
                    ingestionContext.CachingStrategy);
            }

            // If repository signing is enabled, sign the package and save the signed copy
            if (signingEnabled)
            {
                try
                {
                    _logger.LogInformation(
                        "Repository signing enabled, signing package {Id} {Version}",
                        package.Id,
                        package.NormalizedVersionString);

                    var certificate = await _signingKeyProvider.GetSigningCertificateAsync(cancellationToken);
                    if (certificate == null)
                    {
                        if (isStoredCertificateMode)
                        {
                            var errorMessage = "Repository signing is enabled with StoredCertificate mode but no certificate is available. " +
                                "Package upload cannot proceed.";
                            _logger.LogError(errorMessage);
                            throw new InvalidOperationException(errorMessage);
                        }

                        _logger.LogWarning(
                            "Repository signing is enabled but no certificate is available for package {Id} {Version}",
                            package.Id,
                            package.NormalizedVersionString);
                    }
                    else
                    {
                        packageStream.Position = 0;
                        var packageCopy = new MemoryStream();
                        await packageStream.CopyToAsync(packageCopy, cancellationToken);
                        packageCopy.Position = 0;

                        var hasExistingRepositorySignature = await _signingService.IsPackageSignedAsync(packageCopy, cancellationToken);
                        var publishPolicy = _signingOptions.Value.PublishSignaturePolicy;
                        var mirrorPolicy = ingestionContext.MirrorSignaturePolicy;
                        var applyPublishPolicy = ingestionContext.ApplyPublishSignaturePolicy;

                        Stream streamToSign = packageCopy;
                        var skipSigning = false;

                        if (hasExistingRepositorySignature)
                        {
                            if (applyPublishPolicy)
                            {
                                switch (publishPolicy)
                                {
                                    case UpstreamSignatureBehavior.Reject:
                                        var errorMessage = $"Package {package.Id} {package.NormalizedVersionString} already has a repository signature. " +
                                            "Package upload is rejected because PublishSignaturePolicy is set to Reject.";
                                        _logger.LogError(errorMessage);
                                        await packageCopy.DisposeAsync();
                                        throw new InvalidOperationException(errorMessage);

                                    case UpstreamSignatureBehavior.ReSign:
                                    default:
                                        _logger.LogInformation(
                                            "Package {Id} {Version} already has a repository signature, stripping it before adding our own",
                                            package.Id,
                                            package.NormalizedVersionString);

                                        streamToSign = await _signatureStripper.StripRepositorySignaturesAsync(packageCopy, cancellationToken);
                                        if (streamToSign != packageCopy)
                                        {
                                            await packageCopy.DisposeAsync();
                                        }

                                        break;
                                }
                            }
                            else
                            {
                                var upstreamCertificate = await RepositorySignatureInspector.TryGetRepositoryCertificateAsync(packageCopy, _logger, cancellationToken);
                                switch (mirrorPolicy)
                                {
                                    case MirrorRepositorySignaturePolicy.Merge:
                                        if (upstreamCertificate != null)
                                        {
                                            await _certificateService.RecordCertificateUsageAsync(upstreamCertificate, cancellationToken);
                                            skipSigning = true;
                                            _logger.LogInformation(
                                                "Keeping upstream repository signature for package {Id} {Version} due to Merge policy",
                                                package.Id,
                                                package.NormalizedVersionString);
                                        }
                                        else
                                        {
                                            _logger.LogWarning(
                                                "Unable to inspect upstream repository signature for package {Id} {Version}. Re-signing with local certificate.",
                                                package.Id,
                                                package.NormalizedVersionString);
                                            streamToSign = await _signatureStripper.StripRepositorySignaturesAsync(packageCopy, cancellationToken);
                                            if (streamToSign != packageCopy)
                                            {
                                                await packageCopy.DisposeAsync();
                                            }
                                        }
                                        break;

                                    case MirrorRepositorySignaturePolicy.TrustedCerts:
                                        if (upstreamCertificate != null &&
                                            await _certificateService.IsCertificateTrustedAsync(
                                                upstreamCertificate,
                                                CertificateHashAlgorithm.Sha256,
                                                cancellationToken))
                                        {
                                            skipSigning = true;
                                            _logger.LogInformation(
                                                "Upstream repository signature already trusted for package {Id} {Version}, keeping existing signature",
                                                package.Id,
                                                package.NormalizedVersionString);
                                        }
                                        else
                                        {
                                            _logger.LogInformation(
                                                "Repository signature for package {Id} {Version} is not trusted, re-signing with local certificate",
                                                package.Id,
                                                package.NormalizedVersionString);
                                            streamToSign = await _signatureStripper.StripRepositorySignaturesAsync(packageCopy, cancellationToken);
                                            if (streamToSign != packageCopy)
                                            {
                                                await packageCopy.DisposeAsync();
                                            }
                                        }
                                        break;

                                    case MirrorRepositorySignaturePolicy.Resign:
                                    default:
                                        streamToSign = await _signatureStripper.StripRepositorySignaturesAsync(packageCopy, cancellationToken);
                                        if (streamToSign != packageCopy)
                                        {
                                            await packageCopy.DisposeAsync();
                                        }
                                        break;
                                }
                            }
                        }

                        if (skipSigning)
                        {
                            if (streamToSign != packageCopy)
                            {
                                await streamToSign.DisposeAsync();
                            }

                            await packageCopy.DisposeAsync();
                        }
                        else
                        {
                            try
                            {
                                var signedStream = await _signingService.SignPackageAsync(
                                    package.Id,
                                    package.Version,
                                    streamToSign,
                                    certificate,
                                    cancellationToken);

                                signedStream.Position = 0;
                                await _storage.SaveSignedPackageAsync(package.Id, package.Version, signedStream, cancellationToken);

                                _logger.LogInformation(
                                    "Successfully signed and saved package {Id} {Version}",
                                    package.Id,
                                    package.NormalizedVersionString);

                                await signedStream.DisposeAsync();
                            }
                            finally
                            {
                                if (streamToSign != packageCopy)
                                {
                                    await streamToSign.DisposeAsync();
                                }

                                await packageCopy.DisposeAsync();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (isStoredCertificateMode)
                    {
                        var errorMessage = $"Failed to sign package {package.Id} {package.NormalizedVersionString} with stored certificate. " +
                            "Package upload cannot proceed.";
                        _logger.LogError(ex, errorMessage);
                        throw new InvalidOperationException(errorMessage, ex);
                    }

                    _logger.LogError(ex,
                        "Failed to sign package {Id} {Version} during upload, package will be available unsigned",
                        package.Id,
                        package.NormalizedVersionString);
                }
            }

            return new PackageIndexingResult()
            {
                PackageId = package.Id,
                PackageVersion = package.OriginalVersionString,
                Status = PackageIndexingStatus.Success
            };
        }
    }
}
