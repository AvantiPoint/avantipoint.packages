using System.Net;
using System.Runtime.CompilerServices;
using Xunit;

namespace AvantiPoint.Packages.Integration.Tests.TestInfrastructure;

/// <summary>
/// Runs only when outbound HTTPS to NuGet.org succeeds (optional live-upstream tests).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ExternalNetworkFactAttribute : FactAttribute
{
    public const string NuGetOrgServiceIndex = "https://api.nuget.org/v3/index.json";

    public ExternalNetworkFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!ExternalNetworkAvailability.CanReachNuGetOrg)
        {
            Skip = ExternalNetworkAvailability.SkipReason
                ?? "Outbound network access to NuGet.org is not available.";
        }
    }
}

internal static class ExternalNetworkAvailability
{
    private static readonly Lazy<bool> _canReachNuGetOrg = new(CheckNuGetOrg);
    private static readonly Lazy<string?> _skipReason = new(GetSkipReason);

    public static bool CanReachNuGetOrg => _canReachNuGetOrg.Value;

    public static string? SkipReason => _skipReason.Value;

    private static bool CheckNuGetOrg()
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10),
            };

            using var request = new HttpRequestMessage(HttpMethod.Get, ExternalNetworkFactAttribute.NuGetOrgServiceIndex);
            using var response = client.Send(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetSkipReason() =>
        CanReachNuGetOrg ? null : "NuGet.org is not reachable from this environment.";
}
