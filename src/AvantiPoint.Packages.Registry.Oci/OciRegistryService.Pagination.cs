using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Registry.Oci;

public sealed partial class OciRegistryService
{
    internal const int DefaultPageSize = 100;
    internal const int MaximumPageSize = 1000;

    internal static int PageSize(int? requested) => requested is < 0
        ? throw new ArgumentOutOfRangeException(nameof(requested))
        : Math.Min(requested ?? DefaultPageSize, MaximumPageSize);

    private IQueryable<OciTag> DiscoverableTags(OciScope scope, OciFeedOptions options)
    {
        var includeMirrored = options.IncludeMirroredInCatalog;
        // Keep the compatibility-index predicate in SQL, before paging. Removing all
        // hexadecimal characters is portable across our four relational providers and
        // exactly matches IsInternalTag, including uppercase digests and near-miss tags.
        return _context.OciTags.AsNoTracking().Where(tag =>
            tag.FeedId == scope.FeedId && tag.OciSegment == scope.OciSegment
            && (tag.Origin == PackageOrigin.Published || includeMirrored && tag.Origin == PackageOrigin.Mirrored)
            && !tag.Tag.Contains(":")
            && !(tag.Tag.Length == 71
                 && (tag.Tag.Substring(0, 7).ToLower() == "sha256-" || tag.Tag.Substring(0, 7).ToLower() == "sha512-")
                 && tag.Tag.Substring(7, 64).ToLower()
                     .Replace("0", "").Replace("1", "").Replace("2", "").Replace("3", "")
                     .Replace("4", "").Replace("5", "").Replace("6", "").Replace("7", "")
                     .Replace("8", "").Replace("9", "").Replace("a", "").Replace("b", "")
                     .Replace("c", "").Replace("d", "").Replace("e", "").Replace("f", "") == ""));
    }

    private async Task<List<string>> ReadPageAsync(IQueryable<string> names, int count, string? last, CancellationToken token)
    {
        // OCI requires ASCII (case-sensitive) ordering, independent of database defaults.
        var collation = _context.Database.ProviderName switch
        {
            "Microsoft.EntityFrameworkCore.Sqlite" => "BINARY",
            "Npgsql.EntityFrameworkCore.PostgreSQL" => "C",
            "Microsoft.EntityFrameworkCore.SqlServer" => "Latin1_General_100_BIN2",
            "Pomelo.EntityFrameworkCore.MySql" => "utf8mb4_bin",
            "MySql.EntityFrameworkCore" => "utf8mb4_bin",
            _ => null,
        };
        if (collation is not null)
        {
            if (!string.IsNullOrEmpty(last))
                names = names.Where(name => string.Compare(EF.Functions.Collate(name, collation), last) > 0);
            return await names.OrderBy(name => EF.Functions.Collate(name, collation)).Take(count).ToListAsync(token);
        }

        if (!string.IsNullOrEmpty(last)) names = names.Where(name => string.Compare(name, last) > 0);
        return await names.OrderBy(name => name).Take(count).ToListAsync(token);
    }
}
