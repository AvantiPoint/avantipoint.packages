using System.Security.Cryptography;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.UI.Tests;

internal static class NativeUiTestArtifacts
{
    public static async Task SeedAsync(IServiceProvider services, string protocol, string package, params string[] versions)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var feed = scope.ServiceProvider.GetRequiredService<IFeedRegistry>().Feed.FeedId;
        foreach (var version in versions)
        {
            var path = protocol switch
            {
                "Maven" => $"com/example/sdk/{version}/sdk-{version}.pom",
                "Swift" => $"{package}/{version}/Example.xcframework.zip",
                _ => $"packages/{package}/versions/{version}.tar.gz",
            };
            foreach (var identity in new[] { (feed, protocol), (feed.ToUpperInvariant(), protocol), (feed, protocol.ToLowerInvariant()), ("other-feed", protocol) })
            {
                context.NativeArtifacts.Add(new NativeArtifact
                {
                    FeedId = identity.Item1,
                    Protocol = identity.Item2,
                    PackageName = package,
                    Version = version,
                    Path = path,
                    PathHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(path))),
                    ContentHash = new string('a', 64),
                    Length = identity == (feed, protocol) ? 123 : 987654,
                    ContentType = "application/octet-stream",
                    PublishedUtc = DateTime.UtcNow,
                    MetadataJson = "{\"name\":\"sdk\",\"version\":\"" + version + "\"}",
                });
            }
        }
        await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
    }
}
