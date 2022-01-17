using System;
using System.Text.RegularExpressions;

namespace AvantiPoint.Packages.Core
{
    internal static class AgentParser
    {
        public static NuGetClientVersion Parse(string userAgentString)
        {
            userAgentString = userAgentString.Trim();
            if (string.IsNullOrEmpty(userAgentString))
                return new NuGetClientVersion(string.Empty, string.Empty, string.Empty, string.Empty);

            var pattern = @"^(.*)/(\d+(\.\d+)?(\.\d+)?) \((.*)\)$";
            var match = Regex.Match(userAgentString, pattern);

            if (match.Success)
            {
                var name = match.Groups[1].Value;
                var version = match.Groups[2].Value;
                string platform = string.Empty;
                string platformVersion = string.Empty;
                var platformGroup = match.Groups[match.Groups.Count - 1].Value;

                if (platformGroup.Contains("Darwin"))
                    platform = "MacOS";
                else if (platformGroup.Contains("Ubuntu"))
                    platform = "Linux (Ubuntu)";
                else if (platformGroup.Contains("CentOS"))
                    platform = "Linux (CentOS)";
                else if (platformGroup.Contains("Debian"))
                    platform = "Linux (Debian)";
                else if (platformGroup.Contains("Linux"))
                    platform = "Linux";
                else if (platformGroup.Contains("Windows"))
                    platform = "Windows";
                else
                    platform = "Unknown";

                var versionMatch = Regex.Match(platformGroup, @" (\d+(\.\d+)?(\.\d+)?(\.\d+)?)");
                if (versionMatch.Success)
                {
                    var versionString = versionMatch.Groups[1].Value;
                    if (Version.TryParse(versionString, out _))
                        platformVersion = versionString;
                }

                return new NuGetClientVersion(name, version, platform, platformVersion);
            }

            return new NuGetClientVersion(string.Empty, string.Empty, string.Empty, string.Empty);
        }
    }
}
