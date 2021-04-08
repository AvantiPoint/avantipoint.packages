using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace AvantiPoint.Packages
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddNuGetPackagApi(
            this IServiceCollection services,
            Action<NuGetApiApplication> configureAction)
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(PackageContentController).Assembly)
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0)
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.IgnoreNullValues = true;
                });

            services.AddHttpContextAccessor();
            services.AddTransient<IUrlGenerator, NuGetFeedUrlGenerator>();

            services.AddNuGetApiApplication(configureAction);

            return services;
        }
    }
}
