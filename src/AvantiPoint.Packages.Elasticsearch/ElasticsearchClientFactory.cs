using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using OpenSearch.Net;

namespace AvantiPoint.Packages.Elasticsearch;

public static class ElasticsearchClientFactory
{
    public static IOpenSearchClient Create(IOptions<ElasticsearchSearchOptions> options)
    {
        var value = options.Value;
        var uri = new Uri(value.Endpoint);

        var pool = new SingleNodeConnectionPool(uri);
        var settings = new ConnectionSettings(pool)
            .DefaultIndex(value.IndexName)
            .DisableDirectStreaming();

        if (value.DisableCertificateValidation)
        {
            settings.ServerCertificateValidationCallback(CertificateValidationCallback);
        }

        if (!string.IsNullOrEmpty(value.Username))
        {
            settings.BasicAuthentication(value.Username, value.Password ?? string.Empty);
        }

        return new OpenSearchClient(settings);
    }

    private static bool CertificateValidationCallback(
        object sender,
        X509Certificate certificate,
        X509Chain chain,
        SslPolicyErrors errors)
        => true;
}
