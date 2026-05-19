using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core;

public class ValidateSearchOptions : IValidateOptions<SearchOptions>
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Database",
        "Null",
        "AzureSearch",
        "OpenSearch",
        "Elasticsearch",
    };

    public ValidateOptionsResult Validate(string name, SearchOptions options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("Search options are required.");
        }

        if (string.IsNullOrWhiteSpace(options.Type))
        {
            return ValidateOptionsResult.Fail("Search:Type must be configured.");
        }

        if (!AllowedTypes.Contains(options.Type))
        {
            return ValidateOptionsResult.Fail(
                $"Search:Type '{options.Type}' is not supported. Allowed values: {string.Join(", ", AllowedTypes)}.");
        }

        if (options.ReconcileBatchSize < 1)
        {
            return ValidateOptionsResult.Fail("Search:ReconcileBatchSize must be at least 1.");
        }

        return ValidateOptionsResult.Success;
    }
}
