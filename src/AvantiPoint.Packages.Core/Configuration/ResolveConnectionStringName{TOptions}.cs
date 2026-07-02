using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// Resolves <see cref="IConnectionStringOptions.ConnectionString"/> from a named connection string
    /// (<see cref="IConnectionStringOptions.ConnectionStringName"/>) when an explicit connection string
    /// was not supplied. This lets options be configured either with an inline connection string or by
    /// referencing a name under the root <c>ConnectionStrings</c> section (for example the connection
    /// strings supplied by .NET Aspire or a hosting platform).
    /// </summary>
    /// <typeparam name="TOptions">The options type. Only acts on <see cref="IConnectionStringOptions"/> instances.</typeparam>
    public class ResolveConnectionStringName<TOptions> : IPostConfigureOptions<TOptions>
        where TOptions : class
    {
        private readonly IConfiguration _configuration;

        public ResolveConnectionStringName(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public void PostConfigure(string name, TOptions options)
        {
            if (options is not IConnectionStringOptions connectionStringOptions)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(connectionStringOptions.ConnectionString)
                || string.IsNullOrWhiteSpace(connectionStringOptions.ConnectionStringName))
            {
                return;
            }

            var resolved = _configuration.GetConnectionString(connectionStringOptions.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                throw new InvalidOperationException(
                    $"Connection string '{connectionStringOptions.ConnectionStringName}' was not found in the ConnectionStrings configuration.");
            }

            connectionStringOptions.ConnectionString = resolved;
        }
    }
}
