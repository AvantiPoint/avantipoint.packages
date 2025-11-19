using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using AvantiPoint.Packages.Core;

namespace SampleDataGenerator;

/// <summary>
/// Generates synthetic download records that mimic a mix of client tooling and operating systems.
/// </summary>
internal static class SampleDownloadFactory
{
    private static readonly Random Randomizer = new();

    private static readonly DownloadScenario[] ScenarioPool = new[]
    {
        new DownloadScenario(
            NuGetClient: ".NET CLI",
            NuGetClientVersion: "8.0.100",
            ClientPlatform: "Windows",
            ClientPlatformVersion: "10.0.19045",
            UserAgent: "dotnet/8.0.100 (.NET 8.0.0; win10-x64)",
            UserAliases: new[] { "ci-win-runner", "desktop-dev01" },
            AddressPool: new[] { "52.239.152.12", "20.57.19.44", "104.44.87.12" }),
        new DownloadScenario(
            NuGetClient: "Visual Studio",
            NuGetClientVersion: "17.10",
            ClientPlatform: "Windows",
            ClientPlatformVersion: "11.0.22631",
            UserAgent: "NuGet VS VSIX/17.10 (VisualStudio/17.10; .NETFramework,Version=v4.8)",
            UserAliases: new[] { "visualstudio-alice", "visualstudio-bob" },
            AddressPool: new[] { "73.151.77.24", "98.203.44.12" }),
        new DownloadScenario(
            NuGetClient: ".NET CLI",
            NuGetClientVersion: "8.0.100",
            ClientPlatform: "Linux",
            ClientPlatformVersion: "6.8.0-1020-azure",
            UserAgent: "dotnet/8.0.100 (.NET 8.0.0; linux-x64)",
            UserAliases: new[] { "github-actions", "azure-pipeline" },
            AddressPool: new[] { "13.89.104.12", "52.250.12.44", "40.118.92.11" }),
        new DownloadScenario(
            NuGetClient: "Visual Studio for Mac",
            NuGetClientVersion: "17.6",
            ClientPlatform: "macOS",
            ClientPlatformVersion: "14.1",
            UserAgent: "NuGet VS4Mac/17.6 (macOS 14.1; arm64)",
            UserAliases: new[] { "macbook-dev", "ios-team" },
            AddressPool: new[] { "2601:646:8a80:cf0::25", "2601:646:8a80:ce0::26" }),
        new DownloadScenario(
            NuGetClient: "NuGet.exe",
            NuGetClientVersion: "6.9.1",
            ClientPlatform: "Windows",
            ClientPlatformVersion: "10.0.20348",
            UserAgent: "NuGet.exe/6.9.1 (Microsoft Windows NT 10.0.20348.0)",
            UserAliases: new[] { "build-agent-01", "teamcity-runner" },
            AddressPool: new[] { "40.113.200.101", "40.113.200.55" }),
        new DownloadScenario(
            NuGetClient: ".NET CLI",
            NuGetClientVersion: "9.0.100-preview",
            ClientPlatform: "Linux",
            ClientPlatformVersion: "6.1.0-container",
            UserAgent: "dotnet/9.0.100-preview (.NET 9.0.0; linux-musl-x64)",
            UserAliases: new[] { "docker-ci", "gitlab-runner" },
            AddressPool: new[] { "172.19.0.2", "10.42.0.18" })
    };

    public static IReadOnlyList<PackageDownload> CreateDownloads(int packageKey, int count)
    {
        if (count <= 0)
        {
            return Array.Empty<PackageDownload>();
        }

        var downloads = new List<PackageDownload>(count);

        for (var i = 0; i < count; i++)
        {
            var scenario = ScenarioPool[Randomizer.Next(ScenarioPool.Length)];
            downloads.Add(CreateDownload(packageKey, scenario));
        }

        return downloads;
    }

    private static PackageDownload CreateDownload(int packageKey, DownloadScenario scenario)
    {
        return new PackageDownload
        {
            PackageKey = packageKey,
            RemoteIp = scenario.GetRemoteAddress(Randomizer),
            UserAgentString = scenario.UserAgent,
            NuGetClient = scenario.NuGetClient,
            NuGetClientVersion = scenario.NuGetClientVersion,
            ClientPlatform = scenario.ClientPlatform,
            ClientPlatformVersion = scenario.ClientPlatformVersion,
            User = scenario.GetUserAlias(Randomizer)
        };
    }

    private sealed record DownloadScenario(
        string NuGetClient,
        string NuGetClientVersion,
        string ClientPlatform,
        string ClientPlatformVersion,
        string UserAgent,
        IReadOnlyList<string> UserAliases,
        IReadOnlyList<string> AddressPool)
    {
        public string GetUserAlias(Random random) => UserAliases[random.Next(UserAliases.Count)];

        public IPAddress GetRemoteAddress(Random random)
        {
            var candidate = AddressPool[random.Next(AddressPool.Count)];
            return IPAddress.Parse(candidate);
        }
    }
}
