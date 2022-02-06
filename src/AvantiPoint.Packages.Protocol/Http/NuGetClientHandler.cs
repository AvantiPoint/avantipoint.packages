using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol.Authentication;

namespace AvantiPoint.Packages.Protocol.Http
{
    internal class NuGetClientHandler : DelegatingHandler
    {
        private ICredentialsProvider _credentialsProvider { get; }

        public NuGetClientHandler(ICredentialsProvider credentialsProvider)
            : base(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            })
        {
            _credentialsProvider = credentialsProvider;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if(!string.IsNullOrEmpty(_credentialsProvider.Credentials))
                _credentialsProvider.AddHeader(request);

            return base.SendAsync(request, cancellationToken);
        }
    }
}
