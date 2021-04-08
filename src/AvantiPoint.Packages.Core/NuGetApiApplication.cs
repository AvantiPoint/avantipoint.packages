using System;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Core
{
    public class NuGetApiApplication
    {
        public NuGetApiApplication(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IServiceCollection Services { get; }
    }
}
