using System;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting.Authentication;
using AvantiPoint.Packages.Hosting.Internals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Hosting
{
    [AllowAnonymous]
    [AuthorizedNuGetPublisher]
    public class PackagePublishController : Controller
    {
        private readonly IPackageIndexingService _indexer;
        private readonly IPackageService _packages;
        private readonly IPackageDeletionService _deleteService;
        private readonly ILogger<PackagePublishController> _logger;

        public PackagePublishController(
            IPackageIndexingService indexer,
            IPackageService packages,
            IPackageDeletionService deletionService,
            ILogger<PackagePublishController> logger)
        {
            _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _deleteService = deletionService ?? throw new ArgumentNullException(nameof(deletionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // See: https://docs.microsoft.com/en-us/nuget/api/package-publish-resource#push-a-package
        [HandlePackageUploaded]
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
                    case PackageIndexingStatus.InvalidPackage:
                        HttpContext.Response.StatusCode = 400;
                        break;

                    case PackageIndexingStatus.PackageAlreadyExists:
                        HttpContext.Response.StatusCode = 409;
                        break;

                    case PackageIndexingStatus.Success:
                        HttpContext.Response.StatusCode = 201;
                        packageContext.PackageId = result.PackageId;
                        packageContext.PackageVersion = result.PackageVersion;
                        break;
                }
            }
            catch (ArgumentException ae) 
                when (ae.Message.StartsWith("An item with the same key has already been added"))
            {
                // This probably means that package already exists in the database but there is no content stored.
                HttpContext.Response.StatusCode = 410;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception thrown during package upload");

                HttpContext.Response.StatusCode = 500;
            }
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(string id, string version, CancellationToken cancellationToken)
        {
            if (!NuGetVersion.TryParse(version, out var nugetVersion))
            {
                return NotFound();
            }

            if (await _deleteService.TryDeletePackageAsync(id, nugetVersion, cancellationToken))
            {
                return NoContent();
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost]
        public async Task<IActionResult> Relist(string id, string version, CancellationToken cancellationToken)
        {
            if (!NuGetVersion.TryParse(version, out var nugetVersion))
            {
                return NotFound();
            }

            if (await _packages.RelistPackageAsync(id, nugetVersion, cancellationToken))
            {
                return Ok();
            }
            else
            {
                return NotFound();
            }
        }
    }
}
