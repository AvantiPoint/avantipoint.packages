using System;
using System.Text.Json.Serialization;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Feed.Platform.Extensions;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting;
using AvantiPoint.Packages.Hosting.Extensions;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddNuGetPackageApi(
            this IServiceCollection services,
            Action<NuGetApiOptions> configureAction)
        {
            services
#if NET6_0
                .AddControllers()
                .AddApplicationPart(typeof(PackageContentController).Assembly)
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.WriteIndented = true;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
                });
#else
                .Configure<JsonOptions>(options =>
                {
                    options.SerializerOptions.WriteIndented = true;
                    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
                });
#endif

            services.AddHttpContextAccessor();
            services.AddTransient<IUrlGenerator, NuGetFeedUrlGenerator>();
            services.AddScoped<IPackageContext, PackageContext>();

            services.AddAvantiPointFeedPlatform();
            services.AddNuGetFeedActionHandlerAdapter();

            services.AddNuGetApiApplication(configureAction);

            return services;
        }
    }
}
