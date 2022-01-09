using System;
using System.Text.Json.Serialization;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting;
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
                .AddControllers()
                .AddApplicationPart(typeof(PackageContentController).Assembly)
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.WriteIndented = true;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
                });

            services.AddHttpContextAccessor();
            services.AddTransient<IUrlGenerator, NuGetFeedUrlGenerator>();
            services.AddScoped<IPackageContext, PackageContext>();

            services.AddNuGetApiApplication(configureAction);

            return services;
        }
    }
}
