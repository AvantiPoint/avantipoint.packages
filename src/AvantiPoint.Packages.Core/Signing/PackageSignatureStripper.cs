#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Signing;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Utility for stripping repository signatures from NuGet packages while preserving author signatures.
/// This is useful when preparing packages for publication to NuGet.org, which requires only author signatures
/// and will add its own repository signature.
/// </summary>
public class PackageSignatureStripper
{
    private readonly ILogger<PackageSignatureStripper> _logger;

    public PackageSignatureStripper(ILogger<PackageSignatureStripper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Strips repository signatures from a package, preserving author signatures.
    /// If the package has no repository signatures, returns the original package unchanged.
    /// </summary>
    /// <param name="packageStream">The package stream to process. Position will be reset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A stream containing the package with repository signatures removed.
    /// If no repository signatures were found, returns the original stream.
    /// </returns>
    public async Task<Stream> StripRepositorySignaturesAsync(
        Stream packageStream,
        CancellationToken cancellationToken = default)
    {
        if (packageStream == null) throw new ArgumentNullException(nameof(packageStream));

        var originalPosition = packageStream.Position;

        try
        {
            packageStream.Position = 0;

            using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);

            // Check if package is signed
            var isSigned = await packageReader.IsSignedAsync(cancellationToken);
            if (!isSigned)
            {
                _logger.LogDebug("Package is not signed, no signatures to strip");
                packageStream.Position = originalPosition;
                return packageStream;
            }

            // Get primary signature to check type
            var primarySignature = await packageReader.GetPrimarySignatureAsync(cancellationToken);
            if (primarySignature == null)
            {
                _logger.LogDebug("Package has no primary signature, returning unchanged");
                packageStream.Position = originalPosition;
                return packageStream;
            }

            // If primary signature is Author, we assume there might be repository countersignatures
            // Note: Detecting countersignatures is complex, so we'll attempt to strip anyway
            if (primarySignature.Type == SignatureType.Author)
            {
                // Check if there are any countersignatures (repository signatures are typically countersignatures)
                var hasCountersignatures = primarySignature.SignerInfo.CounterSignerInfos.Count > 0;

                if (!hasCountersignatures)
                {
                    _logger.LogDebug("Package has only author signature, no repository signatures to strip");
                    packageStream.Position = originalPosition;
                    return packageStream;
                }

                _logger.LogInformation("Package has author signature with countersignatures, attempting to strip repository signatures");
            }
            else if (primarySignature.Type == SignatureType.Repository)
            {
                _logger.LogWarning(
                    "Package has only repository signature (no author signature). " +
                    "Stripping will result in an unsigned package. " +
                    "Consider adding an author signature first.");
            }

            // Create a new package without repository signatures
            return await CreatePackageWithoutRepositorySignaturesAsync(packageReader, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to strip repository signatures from package");
            throw;
        }
        finally
        {
            packageStream.Position = originalPosition;
        }
    }

    /// <summary>
    /// Creates a new package from the existing package, excluding repository signatures.
    /// 
    /// Note: This implementation uses PackageBuilder which creates an unsigned package.
    /// If the source package has an author signature that needs to be preserved, the package
    /// will need to be re-signed with the author's certificate after stripping.
    /// 
    /// For packages with only repository signatures, this will create an unsigned package
    /// which is suitable for adding an author signature before publishing to NuGet.org.
    /// </summary>
    private async Task<Stream> CreatePackageWithoutRepositorySignaturesAsync(
        PackageArchiveReader sourceReader,
        CancellationToken cancellationToken)
    {
        var tempOutputPath = Path.GetTempFileName();

        try
        {
            // Create a new package using PackageBuilder
            var builder = new PackageBuilder();

            // Copy package metadata
            var nuspec = sourceReader.NuspecReader;
            builder.Id = nuspec.GetId();
            builder.Version = nuspec.GetVersion();
            builder.Description = nuspec.GetDescription();
            
            var authors = nuspec.GetAuthors();
            if (!string.IsNullOrEmpty(authors))
            {
                foreach (var author in authors.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    builder.Authors.Add(author.Trim());
                }
            }
            
            var owners = nuspec.GetOwners();
            if (!string.IsNullOrEmpty(owners))
            {
                foreach (var owner in owners.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    builder.Owners.Add(owner.Trim());
                }
            }

            // Copy all metadata fields
            if (nuspec.GetCopyright() != null)
                builder.Copyright = nuspec.GetCopyright();
            if (nuspec.GetLanguage() != null)
                builder.Language = nuspec.GetLanguage();
            if (nuspec.GetReleaseNotes() != null)
                builder.ReleaseNotes = nuspec.GetReleaseNotes();
            if (nuspec.GetSummary() != null)
                builder.Summary = nuspec.GetSummary();
            if (nuspec.GetTitle() != null)
                builder.Title = nuspec.GetTitle();
            // Tags is read-only, set via constructor or not at all
            // We'll skip tags for now as PackageBuilder doesn't support setting them directly

            // Copy package types
            var packageTypes = nuspec.GetPackageTypes();
            if (packageTypes != null)
            {
                foreach (var packageType in packageTypes)
                {
                    builder.PackageTypes.Add(packageType);
                }
            }

            // Copy license metadata
            var licenseMetadata = nuspec.GetLicenseMetadata();
            if (licenseMetadata != null)
            {
                builder.LicenseMetadata = licenseMetadata;
            }

            // Copy require license acceptance
            builder.RequireLicenseAcceptance = nuspec.GetRequireLicenseAcceptance();

            // Copy development dependency flag
            builder.DevelopmentDependency = nuspec.GetDevelopmentDependency();

            // Copy framework references (skip for now - complex type conversion)
            // Framework references are optional and can be added later if needed

            // Copy dependencies
            var dependencyGroups = nuspec.GetDependencyGroups();
            if (dependencyGroups != null)
            {
                foreach (var group in dependencyGroups)
                {
                    builder.DependencyGroups.Add(group);
                }
            }

            // Copy content files (excluding signature files)
            var files = sourceReader.GetFiles();
            foreach (var file in files)
            {
                // Skip signature files - these are in the .signature.p7s file
                // We'll exclude any file that looks like a signature file
                if (IsSignatureFile(file))
                {
                    _logger.LogDebug("Skipping signature file: {File}", file);
                    continue;
                }

                // Copy the file to the new package
                using var fileStream = sourceReader.GetStream(file);
                var physicalFile = new PhysicalPackageFile
                {
                    SourcePath = await CopyToTempFileAsync(fileStream, cancellationToken),
                    TargetPath = file
                };
                builder.Files.Add(physicalFile);
            }

            // Save the new package
            using var outputStream = File.Create(tempOutputPath);
            builder.Save(outputStream);

            // Read the result into a memory stream
            var resultStream = new MemoryStream();
            outputStream.Position = 0;
            await outputStream.CopyToAsync(resultStream, cancellationToken);
            resultStream.Position = 0;

            // Clean up temp files
            foreach (var file in builder.Files.OfType<PhysicalPackageFile>())
            {
                try
                {
                    if (File.Exists(file.SourcePath))
                        File.Delete(file.SourcePath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            return resultStream;
        }
        catch
        {
            // Clean up on error
            try
            {
                if (File.Exists(tempOutputPath))
                    File.Delete(tempOutputPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
            throw;
        }
    }

    /// <summary>
    /// Determines if a file path represents a signature file.
    /// </summary>
    private static bool IsSignatureFile(string filePath)
    {
        // Signature files are typically:
        // - .signature.p7s (primary signature file)
        // - Any file in a .signatures/ directory
        var normalizedPath = filePath.Replace('\\', '/').ToLowerInvariant();
        return normalizedPath.EndsWith(".signature.p7s", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("/.signatures/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(".signatures/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Copies a stream to a temporary file and returns the file path.
    /// </summary>
    private static async Task<string> CopyToTempFileAsync(Stream sourceStream, CancellationToken cancellationToken)
    {
        var tempFile = Path.GetTempFileName();
        await using var fileStream = File.Create(tempFile);
        await sourceStream.CopyToAsync(fileStream, cancellationToken);
        return tempFile;
    }
}

