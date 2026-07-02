using System.Security.Cryptography;
using System.Text;
using AvantiPoint.Packages.Host.Admin.Services.Events;

namespace AvantiPoint.Packages.Host.Admin.Tests.Events;

public sealed class WebhookDispatcherTests
{
    [Fact]
    public void Sign_ProducesVerifiableHmacSha256()
    {
        const string secret = "webhook-secret";
        const string payload = """{"eventType":"package.published"}""";

        var signature = WebhookDispatcherService.Sign(secret, payload);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        Assert.Equal(expected, signature);
        Assert.StartsWith("sha256=", signature);
    }

    [Theory]
    [InlineData("package.published", true)]  // explicit match
    [InlineData("PACKAGE.PUBLISHED", true)]  // case-insensitive
    [InlineData("group.promoted", false)]    // not subscribed
    public void Matches_FiltersByEventType(string eventType, bool expected)
    {
        var subscription = new HostWebhookSubscription { Events = ["package.published"] };

        Assert.Equal(expected, WebhookDispatcherService.Matches(subscription, eventType));
    }

    [Fact]
    public void Matches_EmptyEventList_MatchesEverything()
    {
        var subscription = new HostWebhookSubscription();

        Assert.True(WebhookDispatcherService.Matches(subscription, "anything.at.all"));
    }
}
