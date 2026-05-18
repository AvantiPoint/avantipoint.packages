using System;
using System.Collections.Generic;
using System.Linq;

namespace AvantiPoint.Packages.Core.Discovery;

internal static class ProviderDictionaryBuilder
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public static IReadOnlyDictionary<string, TProvider> Build<TProvider>(
        IEnumerable<TProvider> providers,
        Func<TProvider, string> nameSelector)
    {
        return providers
            .GroupBy(nameSelector, Comparer)
            .ToDictionary(g => g.Key, g => g.Last(), Comparer);
    }
}

