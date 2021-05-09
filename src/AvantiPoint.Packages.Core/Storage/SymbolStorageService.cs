using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core
{
    public class SymbolStorageService : ISymbolStorageService
    {
        private const string SymbolsPathPrefix = "symbols";
        private const string PdbContentType = "binary/octet-stream";

        private readonly IStorageService _storage;

        public SymbolStorageService(IStorageService storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public async Task SavePortablePdbContentAsync(
            string filename,
            string key,
            Stream pdbStream,
            CancellationToken cancellationToken)
        {
            var path = GetPathForKey(filename, key);
            var result = await _storage.PutAsync(path, pdbStream, PdbContentType, cancellationToken);

            if (result == StoragePutResult.Conflict)
            {
                throw new InvalidOperationException($"Could not save PDB {filename} {key} due to conflict");
            }
        }

        public async Task<Stream> GetPortablePdbContentStreamOrNullAsync(string filename, string key)
        {
            var path = GetPathForKey(filename, key);

            try
            {
                return await _storage.GetAsync(path);
            }
            catch
            {
                return null;
            }
        }

        public Task<Stream> GetSymbolsAsync(string packageId, NuGetVersion packageVersion, CancellationToken cancellationToken = default)
        {
            var path = Path.Combine(SymbolsPathPrefix,
                packageId,
                packageVersion.OriginalVersion,
                $"{packageId}.{packageVersion}.snupkg");

            return _storage.GetAsync(path, cancellationToken);
        }

        public async Task SaveSymbolPackage(NuspecReader nuspec, Stream stream)
        {
            var packageId = nuspec.GetId().ToLower();
            var packageVersion = nuspec.GetVersion().OriginalVersion;
            var path = Path.Combine(SymbolsPathPrefix,
                packageId,
                packageVersion,
                $"{packageId}.{packageVersion}.snupkg");

            await _storage.PutAsync(path, stream, PdbContentType);
        }

        private string GetPathForKey(string filename, string key)
        {
            // Ensure the filename doesn't try to escape out of the current directory.
            var tempPath = Path.GetDirectoryName(Path.GetTempPath());
            var expandedPath = Path.GetDirectoryName(Path.Combine(tempPath, filename));
            
            if (expandedPath != tempPath)
            {
                throw new ArgumentException(null, nameof(filename));
            }

            if (!key.All(char.IsLetterOrDigit))
            {
                throw new ArgumentException(null, nameof(key));
            }

            return Path.Combine(
                SymbolsPathPrefix,
                filename.ToLowerInvariant(),
                key.ToLowerInvariant());
        }
    }
}
