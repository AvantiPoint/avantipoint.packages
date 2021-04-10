using System;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting.Authentication;
using AvantiPoint.Packages.Hosting.Internals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Hosting
{
    [AllowAnonymous]
    public class SymbolController : Controller
    {
        private readonly ISymbolIndexingService _indexer;
        private readonly ISymbolStorageService _storage;
        private readonly ILogger<SymbolController> _logger;

        public SymbolController(
            ISymbolIndexingService indexer,
            ISymbolStorageService storage,
            ILogger<SymbolController> logger)
        {
            _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // See: https://docs.microsoft.com/en-us/nuget/api/package-publish-resource#push-a-package
        [AuthorizedNuGetPublisher]
        [HandleSymbolsUploaded]
        public async Task Upload([FromServices]IPackageContext packageContext, CancellationToken cancellationToken)
        {
            try
            {
                using var uploadStream = await Request.GetUploadStreamOrNullAsync(cancellationToken);
                if (uploadStream == null)
                {
                    HttpContext.Response.StatusCode = 400;
                    return;
                }

                var result = await _indexer.IndexAsync(uploadStream, cancellationToken);

                switch (result.Status)
                {
                    case SymbolIndexingStatus.InvalidSymbolPackage:
                        HttpContext.Response.StatusCode = 400;
                        break;

                    case SymbolIndexingStatus.PackageNotFound:
                        HttpContext.Response.StatusCode = 404;
                        break;

                    case SymbolIndexingStatus.Success:
                        HttpContext.Response.StatusCode = 201;
                        packageContext.PackageId = result.PackageId;
                        packageContext.PackageVersion = result.PackageVersion;
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception thrown during symbol upload");

                HttpContext.Response.StatusCode = 500;
            }
        }

        [AuthorizedNuGetConsumer]
        public async Task<IActionResult> Get(string file, string key)
        {
            using var pdbStream = await _storage.GetPortablePdbContentStreamOrNullAsync(file, key);
            if (pdbStream == null)
            {
                return NotFound();
            }

            return File(pdbStream.AsMemoryStream(), "application/octet-stream");
        }
    }
}
