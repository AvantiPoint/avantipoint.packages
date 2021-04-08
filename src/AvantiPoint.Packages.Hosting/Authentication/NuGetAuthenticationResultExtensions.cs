using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Mvc;

namespace AvantiPoint.Packages.Hosting.Authentication
{
    internal static class NuGetAuthenticationResultExtensions
    {
        public static IActionResult CreateActionResult(this NuGetAuthenticationResult result) =>
            new NuGetAutheticationActionResult(result);
    }
}
