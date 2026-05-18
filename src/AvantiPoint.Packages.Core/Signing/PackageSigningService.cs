#nullable enable
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Default implementation of package signing service using NuGet.Packaging APIs.
/// </summary>
public class PackageSigningService(
    ILogger<PackageSigningService> logger,
    IUrlGenerator urlGenerator,
    IOptions<SigningOptions> signingOptions) : IPackageSigningService
{
    private const string DefaultTimestampServerUrl = "http://timestamp.digicert.com";
    private readonly SigningOptions _signingOptions = signingOptions.Value;

    /// <inheritdoc />
    public async Task<Stream> SignPackageAsync(
        string packageId,
        NuGetVersion version,
        Stream packageStream,
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
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
                var serviceIndexUrl = urlGenerator.GetServiceIndexUrl();
                var v3ServiceIndexUrl = new Uri(serviceIndexUrl);
                using var signRequest = new RepositorySignPackageRequest(
                    certificate,
                    HashAlgorithmName.SHA256,
                    HashAlgorithmName.SHA256,
                    v3ServiceIndexUrl,
                    packageOwners: null);

                // Create timestamp provider if timestamping is enabled
                ITimestampProvider? timestampProvider = null;
                var timestampServerUrl = _signingOptions.TimestampServerUrl;

                if (timestampServerUrl is null)
                {
                    try
                    {
                        var defaultTimestampServerUri = new Uri(DefaultTimestampServerUrl);
                        timestampProvider = new Rfc3161TimestampProvider(defaultTimestampServerUri);
                        logger.LogInformation(
                            "Using default timestamp server: {TimestampServerUrl}",
                            DefaultTimestampServerUrl);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Failed to create default timestamp provider, signing without timestamp. Signatures will become invalid when certificate expires.");
                    }
                }
                else if (!string.IsNullOrWhiteSpace(timestampServerUrl))
                {
                    try
                    {
                        var timestampServerUri = new Uri(timestampServerUrl);
                        timestampProvider = new Rfc3161TimestampProvider(timestampServerUri);
                        logger.LogInformation(
                            "Using timestamp server: {TimestampServerUrl}",
                            timestampServerUrl);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Invalid timestamp server URL '{TimestampServerUrl}', signing without timestamp. Signatures will become invalid when certificate expires.",
                            timestampServerUrl);
                    }
                }

                // Create signing options using constructor
                var signatureProvider = new X509SignatureProvider(timestampProvider: timestampProvider);
                using var options = new NuGet.Packaging.Signing.SigningOptions(
                    new Lazy<Stream>(() => File.OpenRead(tempInputPath)),
                    new Lazy<Stream>(() => File.Open(tempOutputPath, FileMode.Create, FileAccess.ReadWrite)),
                    overwrite: true,
                    signatureProvider: signatureProvider,
                    logger: NullLogger.Instance);

                // Sign the package
                await SigningUtility.SignAsync(options, signRequest, cancellationToken);
                
                // Dispose options to ensure all streams are closed before reading the output file
                options.Dispose();

                // Read signed package into memory stream
                var signedStream = new MemoryStream();
                await using (var fileStream = File.OpenRead(tempOutputPath))
                {
                    await fileStream.CopyToAsync(signedStream, cancellationToken);
                }

                signedStream.Position = 0;

                logger.LogInformation(
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
                    logger.LogWarning(cleanupEx, "Failed to clean up temporary signing files");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
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
            logger.LogWarning(ex, "Failed to check if package is signed, assuming unsigned");
            return false;
        }
        finally
        {
            packageStream.Position = originalPosition;
        }
    }
}
