#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Signing;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Removes repository signatures without rebuilding the package or discarding
/// the author's cryptographic signature, timestamps, metadata, or archive entries.
/// </summary>
public class PackageSignatureStripper(ILogger<PackageSignatureStripper> logger)
{
    public async Task<Stream> StripRepositorySignaturesAsync(Stream packageStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageStream);
        if (!packageStream.CanSeek) throw new ArgumentException("The package stream must support seeking.", nameof(packageStream));
        var originalPosition = packageStream.Position;
        MemoryStream? unsigned = null;
        MemoryStream? result = null;
        try
        {
            packageStream.Position = 0;
            PrimarySignature? primary;
            using (var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true))
                primary = await reader.GetPrimarySignatureAsync(cancellationToken);
            if (primary is null) return packageStream;

            byte[]? authorSignature = null;
            if (primary.Type == SignatureType.Author)
            {
                var repository = RepositoryCountersignature.GetRepositoryCountersignature(primary);
                if (repository is null) return packageStream;
                primary.SignedCms.SignerInfos[0].RemoveCounterSignature(repository.SignerInfo);
                authorSignature = primary.GetBytes();
            }
            else if (primary.Type != SignatureType.Repository)
            {
                throw new InvalidOperationException("The package has an unsupported signature type.");
            }

            // NuGet's ZIP-signature writer preserves the original package contents.
            // Rebuilding via PackageBuilder changes both signed bytes and metadata.
            packageStream.Position = 0;
            unsigned = new MemoryStream();
            using (var archive = new SignedPackageArchive(packageStream, unsigned))
                await archive.RemoveSignatureAsync(cancellationToken);
            unsigned.Position = 0;
            if (authorSignature is null)
            {
                var unsignedResult = unsigned;
                unsigned = null;
                return unsignedResult;
            }

            result = new MemoryStream();
            using (var signature = new MemoryStream(authorSignature, writable: false))
            using (var archive = new SignedPackageArchive(unsigned, result))
                await archive.AddSignatureAsync(signature, cancellationToken);
            result.Position = 0;
            var signedResult = result;
            result = null;
            return signedResult;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to remove repository signatures from package");
            throw;
        }
        finally
        {
            unsigned?.Dispose();
            result?.Dispose();
            packageStream.Position = originalPosition;
        }
    }
}
