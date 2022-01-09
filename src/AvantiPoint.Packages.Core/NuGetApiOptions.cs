using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages.Core
{
    public class NuGetApiOptions
    {
        private static readonly string[] hostEnvironmentTypes = new[]
        {
            "Microsoft.Extensions.Hosting.IHostEnvironment",
            "Microsoft.AspNetCore.Hosting.IWebHostEnvironment"
        };

        public NuGetApiOptions(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));

            var hostEnvironment = services.FirstOrDefault(x => hostEnvironmentTypes.Any(he => he == x.ServiceType.FullName))?.ImplementationInstance;
            EnvironmentName = (string)hostEnvironment.GetType().GetProperty("EnvironmentName").GetValue(hostEnvironment);

            var configurationDescriptors = services.Where(x => x.ServiceType == typeof(IConfiguration));

            if(configurationDescriptors.Any(x => x.ImplementationInstance != null))
            {
                Configuration = (IConfiguration)configurationDescriptors.First(x => x.ImplementationInstance != null).ImplementationInstance;
            }
            else if(configurationDescriptors.Any(x => x.ImplementationFactory != null))
            {
                var configurationDescriptor = configurationDescriptors.First(x => x.ImplementationFactory != null);
                var instance = configurationDescriptor.ImplementationFactory(null);
                Configuration = (IConfiguration)instance;
            }

            if(Configuration != null)
            {
                var options = new PackageFeedOptions();
                Configuration.Bind(options);
                Options = options;
            }
        }

        public PackageFeedOptions Options { get; }

        public IConfiguration Configuration { get; }

        public IServiceCollection Services { get; }

        public string EnvironmentName { get; }

        public bool IsDevelopment => EnvironmentName.Equals("Development", StringComparison.OrdinalIgnoreCase);
    }
}
