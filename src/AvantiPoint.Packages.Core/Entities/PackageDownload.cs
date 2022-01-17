using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;

namespace AvantiPoint.Packages.Core
{
    public class PackageDownload
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public int PackageKey { get; set; }

        public IPAddress RemoteIp { get; set; }

        public string UserAgentString { get; set; }

        public string NuGetClient { get; set; }

        public string NuGetClientVersion { get; set; }

        public string ClientPlatform { get; set; }

        public string ClientPlatformVersion { get; set; }

        public string User { get; set; }

        private DateTimeOffset _timestamp = DateTimeOffset.UtcNow;
        public DateTimeOffset Timestamp => _timestamp;

        public Package Package { get; set; }
    }
}
