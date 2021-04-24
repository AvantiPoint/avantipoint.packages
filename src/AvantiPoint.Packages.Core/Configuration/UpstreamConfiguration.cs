using System;

namespace AvantiPoint.Packages.Core
{
    public record UpstreamConfiguration
    {
        public Uri FeedUrl { get; init; }

        public string Username { get; init; }

        public string ApiToken { get; init; }

        private int _timeout;
        public int Timeout
        {
            get => _timeout > 0 ? _timeout : 600;
            init => _timeout = value;
        }
    }
}
