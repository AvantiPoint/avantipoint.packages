using System;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages
{
    public static class APPackagesApplicationExtensions
    {
        public static NuGetApiOptions AddFileStorage(this NuGetApiOptions options)
        {
            options.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<FileStorageService>());
            return options;
        }

        public static NuGetApiOptions AddFileStorage(
            this NuGetApiOptions options,
            Action<FileSystemStorageOptions> configure)
        {
            options.AddFileStorage();
            options.Services.Configure(configure);
            return options;
        }

        public static NuGetApiOptions AddNullStorage(this NuGetApiOptions options)
        {
            options.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<NullStorageService>());
            return options;
        }

        public static NuGetApiOptions AddNullSearch(this NuGetApiOptions options)
        {
            options.Services.TryAddTransient<ISearchIndexer>(provider => provider.GetRequiredService<NullSearchIndexer>());
            options.Services.TryAddTransient<ISearchService>(provider => provider.GetRequiredService<NullSearchService>());
            return options;
        }
    }
}
