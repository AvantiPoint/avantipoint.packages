using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Packaging;

namespace AvantiPoint.Packages.Core
{
    public class PackageIndexingService : IPackageIndexingService
    {
        private readonly IPackageService _packages;
        private readonly IPackageStorageService _storage;
        private readonly ISearchIndexer _search;
        private readonly SystemTime _time;
        private readonly IOptionsSnapshot<PackageFeedOptions> _options;
        private readonly IRepositorySigningKeyProvider _signingKeyProvider;
        private readonly IPackageSigningService _signingService;
        private readonly PackageSignatureStripper _signatureStripper;
        private readonly IOptions<SigningOptions> _signingOptions;
        private readonly ILogger<PackageIndexingService> _logger;

        public PackageIndexingService(
            IPackageService packages,
            IPackageStorageService storage,
            ISearchIndexer search,
            SystemTime time,
            IOptionsSnapshot<PackageFeedOptions> options,
            IRepositorySigningKeyProvider signingKeyProvider,
            IPackageSigningService signingService,
            PackageSignatureStripper signatureStripper,
            IOptions<SigningOptions> signingOptions,
            ILogger<PackageIndexingService> logger)
        {
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _signingKeyProvider = signingKeyProvider ?? throw new ArgumentNullException(nameof(signingKeyProvider));
            _signingService = signingService ?? throw new ArgumentNullException(nameof(signingService));
            _signatureStripper = signatureStripper ?? throw new ArgumentNullException(nameof(signatureStripper));
            _signingOptions = signingOptions ?? throw new ArgumentNullException(nameof(signingOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PackageIndexingResult> IndexAsync(Stream packageStream, CancellationToken cancellationToken)
        {
            // Try to extract all the necessary information from the package.
            Package package;
            Stream nuspecStream;
            Stream readmeStream;
            Stream iconStream;
            Stream licenseStream;

            try
            {
                using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
                package = await packageReader.GetPackageMetadata();

                // For unsigned packages, use the current time as the published date.
                // For signed packages, GetPackageMetadata already set the Published date to the signature timestamp.
                if (!package.IsSigned)
                {
                    package.Published = _time.UtcNow;
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
            var isStoredCertificateMode = signingEnabled && _signingOptions.Value.Mode == SigningMode.StoredCertificate;
            
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

            await _search.IndexAsync(package, cancellationToken);

            _logger.LogInformation(
                "Successfully indexed package {Id} {Version} in search",
                package.Id,
                package.NormalizedVersionString);

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
                        // Get the package stream and check if it already has a repository signature
                        packageStream.Position = 0;
                        var packageCopy = new MemoryStream();
                        await packageStream.CopyToAsync(packageCopy, cancellationToken);
                        packageCopy.Position = 0;

                        // Check if package already has a repository signature (e.g., from nuget.org)
                        var hasExistingRepositorySignature = await _signingService.IsPackageSignedAsync(packageCopy, cancellationToken);
                        
                        Stream streamToSign = packageCopy;
                        if (hasExistingRepositorySignature)
                        {
                            switch (_signingOptions.Value.UpstreamSignature)
                            {
                                case UpstreamSignatureBehavior.Reject:
                                    // Strict mode: reject packages with existing repository signatures
                                    var errorMessage = $"Package {package.Id} {package.NormalizedVersionString} already has a repository signature. " +
                                        "Package upload is rejected because UpstreamSignature is set to Reject.";
                                    _logger.LogError(errorMessage);
                                    await packageCopy.DisposeAsync();
                                    throw new InvalidOperationException(errorMessage);

                                case UpstreamSignatureBehavior.ReSign:
                                default:
                                    _logger.LogInformation(
                                        "Package {Id} {Version} already has a repository signature, stripping it before adding our own",
                                        package.Id,
                                        package.NormalizedVersionString);
                                    
                                    // Strip existing repository signature (preserves author signatures)
                                    streamToSign = await _signatureStripper.StripRepositorySignaturesAsync(packageCopy, cancellationToken);
                                    
                                    // If the stripper returned a new stream, dispose the old one
                                    if (streamToSign != packageCopy)
                                    {
                                        await packageCopy.DisposeAsync();
                                    }
                                    break;
                            }
                        }

                        try
                        {
                            // Sign the package with our repository signature
                            var signedStream = await _signingService.SignPackageAsync(
                                package.Id,
                                package.Version,
                                streamToSign,
                                certificate,
                                cancellationToken);

                            // Save the signed copy
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
                            // Dispose the stream we used for signing
                            if (streamToSign != packageCopy)
                            {
                                await streamToSign.DisposeAsync();
                            }
                            await packageCopy.DisposeAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (isStoredCertificateMode)
                    {
                        // For stored certificates, fail the upload if signing fails
                        var errorMessage = $"Failed to sign package {package.Id} {package.NormalizedVersionString} with stored certificate. " +
                            "Package upload cannot proceed.";
                        _logger.LogError(ex, errorMessage);
                        throw new InvalidOperationException(errorMessage, ex);
                    }
                    
                    // For self-signed certificates, allow graceful fallback
                    _logger.LogError(ex,
                        "Failed to sign package {Id} {Version} during upload, package will be available unsigned",
                        package.Id,
                        package.NormalizedVersionString);
                    // Don't fail the upload if signing fails for self-signed certificates
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
