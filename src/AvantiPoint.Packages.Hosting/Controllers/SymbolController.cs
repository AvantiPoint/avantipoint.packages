using System;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AvantiPoint.Packages.Hosting.Authentication;
using Microsoft.AspNetCore.Authorization;

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
        public async Task Upload(CancellationToken cancellationToken)
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

                switch (result)
                {
                    case SymbolIndexingResult.InvalidSymbolPackage:
                        HttpContext.Response.StatusCode = 400;
                        break;

                    case SymbolIndexingResult.PackageNotFound:
                        HttpContext.Response.StatusCode = 404;
                        break;

                    case SymbolIndexingResult.Success:
                        HttpContext.Response.StatusCode = 201;
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
