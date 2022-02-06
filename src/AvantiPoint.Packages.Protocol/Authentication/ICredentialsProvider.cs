using System.Net.Http;

namespace AvantiPoint.Packages.Protocol.Authentication
{
    public interface ICredentialsProvider
    {
        string Credentials { get; }

        void AddHeader(HttpRequestMessage request);
    }
}
