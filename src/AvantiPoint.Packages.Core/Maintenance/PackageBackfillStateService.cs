using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Core.Maintenance
{
    /// <summary>
    /// Default implementation of <see cref="IPackageBackfillStateService"/> that stores state in the storage service.
    /// </summary>
    public class PackageBackfillStateService : IPackageBackfillStateService
    {
        private const string StateFilePath = ".metadata/backfill-state.json";
        
        private readonly IStorageService _storage;
        private readonly ILogger<PackageBackfillStateService> _logger;

        public PackageBackfillStateService(
            IStorageService storage,
            ILogger<PackageBackfillStateService> logger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PackageBackfillState> GetStateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var stream = await _storage.GetAsync(StateFilePath, cancellationToken);
                if (stream == null)
                {
                    _logger.LogInformation("No existing backfill state found, creating new state");
                    return new PackageBackfillState();
                }

                using (stream)
                {
                    var state = await JsonSerializer.DeserializeAsync<PackageBackfillState>(stream, cancellationToken: cancellationToken);
                    return state ?? new PackageBackfillState();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load backfill state, creating new state");
                return new PackageBackfillState();
            }
        }

        public async Task SaveStateAsync(PackageBackfillState state, CancellationToken cancellationToken = default)
        {
            try
            {
                using var stream = new MemoryStream();
                await JsonSerializer.SerializeAsync(stream, state, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
                stream.Position = 0;

                await _storage.PutAsync(StateFilePath, stream, "application/json", cancellationToken);
                _logger.LogInformation("Successfully saved backfill state");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save backfill state");
                throw;
            }
        }
    }
}
