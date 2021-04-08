using System;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages
{
    public static class APPackagesApplicationExtensions
    {
        public static NuGetApiApplication AddFileStorage(this NuGetApiApplication app)
        {
            app.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<FileStorageService>());
            return app;
        }

        public static NuGetApiApplication AddFileStorage(
            this NuGetApiApplication app,
            Action<FileSystemStorageOptions> configure)
        {
            app.AddFileStorage();
            app.Services.Configure(configure);
            return app;
        }

        public static NuGetApiApplication AddNullStorage(this NuGetApiApplication app)
        {
            app.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<NullStorageService>());
            return app;
        }

        public static NuGetApiApplication AddNullSearch(this NuGetApiApplication app)
        {
            app.Services.TryAddTransient<ISearchIndexer>(provider => provider.GetRequiredService<NullSearchIndexer>());
            app.Services.TryAddTransient<ISearchService>(provider => provider.GetRequiredService<NullSearchService>());
            return app;
        }
    }
}
