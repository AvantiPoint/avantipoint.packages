using System.Net.Http;

namespace AvantiPoint.Packages.Protocol.Authentication
{
    internal sealed class NullCredentialsProvider : ICredentialsProvider
    {
        public string Credentials { get; }

        public void AddHeader(HttpRequestMessage request)
        {
        }
    }
}
