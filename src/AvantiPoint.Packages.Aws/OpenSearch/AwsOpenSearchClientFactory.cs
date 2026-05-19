using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Amazon;
using AvantiPoint.Packages.Aws.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using OpenSearch.Net;
using OpenSearch.Net.Auth.AwsSigV4;

namespace AvantiPoint.Packages.Aws.OpenSearch;

internal static class AwsOpenSearchClientFactory
{
    public static IOpenSearchClient Create(IServiceProvider services, IOptions<OpenSearchOptions> options)
    {
        var value = options.Value;
        var uri = new Uri(value.Endpoint);
        var connection = new AwsSigV4HttpConnection(
            region: RegionEndpoint.GetBySystemName(value.Region ?? "us-east-1"),
            service: AwsSigV4HttpConnection.OpenSearchServerlessService);

        var pool = new SingleNodeConnectionPool(uri);
        var settings = new ConnectionSettings(pool, connection)
            .DefaultIndex(value.IndexName)
            .DisableDirectStreaming();

        if (value.DisableCertificateValidation)
        {
            settings.ServerCertificateValidationCallback((_, _, _, _) => true);
        }

        if (!value.UseIamAuth && !string.IsNullOrEmpty(value.Username))
        {
            settings.BasicAuthentication(value.Username, value.Password ?? string.Empty);
        }

        return new OpenSearchClient(settings);
    }
}
