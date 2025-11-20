#nullable enable
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Default implementation of package signing service using NuGet.Packaging APIs.
/// </summary>
public class PackageSigningService : IPackageSigningService
{
    private readonly ILogger<PackageSigningService> _logger;

    public PackageSigningService(ILogger<PackageSigningService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Stream> SignPackageAsync(
        string packageId,
        NuGetVersion version,
        Stream packageStream,
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        if (packageStream == null) throw new ArgumentNullException(nameof(packageStream));
        if (certificate == null) throw new ArgumentNullException(nameof(certificate));

        _logger.LogInformation(
            "Signing package {PackageId} {Version} with certificate {Thumbprint}",
            packageId,
            version,
            certificate.Thumbprint);

        try
        {
            // Create temp files
            var tempInputPath = Path.GetTempFileName();
            var tempOutputPath = Path.GetTempFileName();

            try
            {
                // Write input package to temp file
                packageStream.Position = 0;
                await using (var fileStream = File.Create(tempInputPath))
                {
                    await packageStream.CopyToAsync(fileStream, cancellationToken);
                }

                // Create signing request for repository signature
                var v3ServiceIndexUrl = new Uri("https://repository.local/v3/index.json");
                using var signRequest = new RepositorySignPackageRequest(
                    certificate,
                    HashAlgorithmName.SHA256,
                    HashAlgorithmName.SHA256,
                    v3ServiceIndexUrl,
                    packageOwners: null);

                // Create signing options using constructor
                var signatureProvider = new X509SignatureProvider(timestampProvider: null);
                using var options = new NuGet.Packaging.Signing.SigningOptions(
                    new Lazy<Stream>(() => File.OpenRead(tempInputPath)),
                    new Lazy<Stream>(() => File.Open(tempOutputPath, FileMode.Create, FileAccess.ReadWrite)),
                    overwrite: true,
                    signatureProvider: signatureProvider,
                    logger: NullLogger.Instance);

                // Sign the package
                await SigningUtility.SignAsync(options, signRequest, cancellationToken);

                // Read signed package into memory stream
                var signedStream = new MemoryStream();
                await using (var fileStream = File.OpenRead(tempOutputPath))
                {
                    await fileStream.CopyToAsync(signedStream, cancellationToken);
                }

                signedStream.Position = 0;

                _logger.LogInformation(
                    "Successfully signed package {PackageId} {Version}",
                    packageId,
                    version);

                return signedStream;
            }
            finally
            {
                // Clean up temp files
                try
                {
                    if (File.Exists(tempInputPath)) File.Delete(tempInputPath);
                    if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to clean up temporary signing files");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to sign package {PackageId} {Version}",
                packageId,
                version);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsPackageSignedAsync(
        Stream packageStream,
        CancellationToken cancellationToken = default)
    {
        if (packageStream == null) throw new ArgumentNullException(nameof(packageStream));

        var originalPosition = packageStream.Position;

        try
        {
            packageStream.Position = 0;

            using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);

            // Check if package has any repository signatures
            var isRepositorySigned = await packageReader.IsSignedAsync(cancellationToken);

            if (isRepositorySigned)
            {
                // Verify it's a repository signature (not author signature)
                var primarySignature = await packageReader.GetPrimarySignatureAsync(cancellationToken);
                return primarySignature?.Type == SignatureType.Repository;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check if package is signed, assuming unsigned");
            return false;
        }
        finally
        {
            packageStream.Position = originalPosition;
        }
    }
}
