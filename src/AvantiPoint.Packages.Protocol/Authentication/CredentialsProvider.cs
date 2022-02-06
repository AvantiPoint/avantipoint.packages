using System.Net.Http;

namespace AvantiPoint.Packages.Protocol.Authentication
{
    public abstract class CredentialsProvider : ICredentialsProvider
    {
        public string Credentials { get; protected set; }
        public abstract void AddHeader(HttpRequestMessage request);

        public static ICredentialsProvider Null() =>
            new NullCredentialsProvider();

        public static ICredentialsProvider Basic(string userName, string password) =>
            new BasicCredentialsProvider(userName, password);

        public static ICredentialsProvider Token(string apiToken) =>
            new TokenCredentialsProvider(apiToken);
    }
}
