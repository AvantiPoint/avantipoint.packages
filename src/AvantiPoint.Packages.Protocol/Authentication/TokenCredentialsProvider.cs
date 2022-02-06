using System.Net.Http;

namespace AvantiPoint.Packages.Protocol.Authentication
{
    internal sealed class TokenCredentialsProvider : CredentialsProvider
    {
        public const string ApiKeyHeader = "X-NuGet-ApiKey";

        public TokenCredentialsProvider(string apiToken)
        {
            Credentials = apiToken;
        }

        public override void AddHeader(HttpRequestMessage request)
        {
            request.Headers.Add(ApiKeyHeader, Credentials);
        }
    }
}
