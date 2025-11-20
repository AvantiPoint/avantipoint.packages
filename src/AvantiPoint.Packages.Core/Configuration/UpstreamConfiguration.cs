using System;

namespace AvantiPoint.Packages.Core
{
    public record UpstreamConfiguration
    {
        public Uri FeedUrl { get; init; }

        public string Username { get; init; }

        public string ApiToken { get; init; }

        /// <summary>
        /// Optional path to a NuGet.config file to load package sources from.
        /// If specified, sources from this file will be loaded automatically.
        /// Sources with encrypted passwords will be ignored.
        /// </summary>
        public string NuGetConfigPath { get; init; }

        private int _timeout;
        public int Timeout
        {
            get => _timeout > 0 ? _timeout : 600;
            init => _timeout = value;
        }
    }
}
