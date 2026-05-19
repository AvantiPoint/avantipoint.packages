using System.Collections.Generic;
using System.Linq;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Filters package queries for search and registration discovery based on <see cref="SearchOptions.IncludeMirroredPackages"/>.
/// </summary>
public static class PackageOriginFilter
{
    public static IQueryable<Package> ApplyDiscoveryFilter(IQueryable<Package> query, SearchOptions options)
    {
        if (options?.IncludeMirroredPackages != false)
        {
            return query.Where(p => p.Origin != PackageOrigin.Cached);
        }

        return query.Where(p => p.Origin == PackageOrigin.Published);
    }

    public static IReadOnlyList<string> GetAllowedOriginNames(SearchOptions options)
    {
        if (options?.IncludeMirroredPackages != false)
        {
            return
            [
                nameof(PackageOrigin.Published),
                nameof(PackageOrigin.Mirrored),
            ];
        }

        return [nameof(PackageOrigin.Published)];
    }
}
