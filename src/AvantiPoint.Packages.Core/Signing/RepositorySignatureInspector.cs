#nullable enable

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Signing;

namespace AvantiPoint.Packages.Core.Signing;

internal static class RepositorySignatureInspector
{
    public static async Task<X509Certificate2?> TryGetRepositoryCertificateAsync(
        Stream packageStream,
        ILogger? logger,
        CancellationToken cancellationToken)
    {

        var originalPosition = packageStream.Position;

        try
        {
            packageStream.Position = 0;
            using var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
            var primarySignature = await reader.GetPrimarySignatureAsync(cancellationToken);

            if (primarySignature?.Type == SignatureType.Repository &&
                primarySignature.SignerInfo?.Certificate != null)
            {
                return new X509Certificate2(primarySignature.SignerInfo.Certificate);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Unable to inspect repository signature certificate");
        }
        finally
        {
            packageStream.Position = originalPosition;
        }

        return null;
    }
}

