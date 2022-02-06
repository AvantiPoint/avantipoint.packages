using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace AvantiPoint.Packages.Protocol.Authentication
{
    internal sealed class BasicCredentialsProvider : CredentialsProvider
    {
        public BasicCredentialsProvider(string userName, string password)
        {
            var authenticationString = $"{userName}:{password}";
            Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationString));
        }

        public override void AddHeader(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Credentials);
        }
    }
}
