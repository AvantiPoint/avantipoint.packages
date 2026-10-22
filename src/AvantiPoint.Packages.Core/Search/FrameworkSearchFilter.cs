using System;
using System.Collections.Generic;
using System.Linq;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Normalizes the legacy and multi-framework search inputs into groups of package asset
/// frameworks that satisfy each requested target framework.
/// </summary>
public static class FrameworkSearchFilter
{
    public static IReadOnlyList<string> GetRequestedFrameworks(SearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var configured = request.Frameworks is { Count: > 0 }
            ? request.Frameworks
            : string.IsNullOrWhiteSpace(request.Framework)
                ? Array.Empty<string>()
                : [request.Framework];

        return configured
            .Where(static framework => !string.IsNullOrWhiteSpace(framework))
            .Select(static framework => framework.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<IReadOnlyList<string>> GetMatchGroups(
        SearchRequest request,
        IFrameworkCompatibilityService compatibility)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(compatibility);

        return GetRequestedFrameworks(request)
            .Select(framework => request.IncludeCompatibleFrameworks
                ? compatibility.FindAllCompatibleFrameworks(framework)
                : (IReadOnlyList<string>)[framework])
            .Select(static frameworks => (IReadOnlyList<string>)frameworks
                .Where(static framework => !string.IsNullOrWhiteSpace(framework))
                .Select(static framework => framework.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray())
            .Where(static frameworks => frameworks.Count > 0)
            .ToList();
    }
}
