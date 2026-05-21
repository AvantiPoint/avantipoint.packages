using System;
using Microsoft.Extensions.Configuration;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// Implements <see cref="IProvider{TService}"/> as a delegate.
    /// </summary>
    internal class DelegateProvider<TService> : IProvider<TService>
    {
        private readonly Func<IServiceProvider, IConfiguration, TService> _func;

        /// <summary>
        /// Create an <see cref="IProvider{TService}"/> using a delegate.
        /// </summary>
        /// <param name="func">
        /// A delegate that returns an instance of <typeparamref name="TService"/>, or,
        /// null if the provider is not currently active due to the app's configuration.</param>
        public DelegateProvider(Func<IServiceProvider, IConfiguration, TService> func)
        {
            _func = func ?? throw new ArgumentNullException(nameof(func));
        }

        public TService GetOrNull(IServiceProvider provider, IConfiguration configuration)
        {
            return _func(provider, configuration);
        }
    }
}
