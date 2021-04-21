using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Core
{
    public class NuGetApiApplication
    {
        public NuGetApiApplication(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));

            var hostEnvironment = services.FirstOrDefault(x => x.ServiceType.FullName == "Microsoft.Extensions.Hosting.IHostEnvironment")?.ImplementationInstance;
            EnvironmentName = (string)hostEnvironment.GetType().GetProperty("EnvironmentName").GetValue(hostEnvironment);
        }

        public IServiceCollection Services { get; }

        public string EnvironmentName { get; }

        public bool IsDevelopment => EnvironmentName.Equals("Development", StringComparison.OrdinalIgnoreCase);
    }
}
