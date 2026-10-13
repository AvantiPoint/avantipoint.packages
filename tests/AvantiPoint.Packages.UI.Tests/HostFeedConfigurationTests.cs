using System.ComponentModel.DataAnnotations;
using AvantiPoint.Packages.Host.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Text.RegularExpressions;
using AvantiPoint.Packages.Host.Admin.Data;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class HostFeedConfigurationTests
{
    [Fact]
    public async Task LegacySavedEnablementAndCustomOciConfigurationArePreserved()
    {
        await using var factory = new ManagedHostFactory(enabled: false);
        using var client = factory.CreateClient();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        configuration["Feed:Oci:Custom:Enabled"] = "true";
        configuration["Feed:Oci:Default:Mirror:RegistryUrl"] = "https://registry.example.test/";
        var path = configuration["Host:FeedConfigurationPath"]!;
        await File.WriteAllTextAsync(path,
            "{\"Feed\":{\"PublicBaseUrl\":\"https://packages.example.test/prefix/\",\"Maven\":{\"Enabled\":true}}}",
            Xunit.TestContext.Current.CancellationToken);
        var store = factory.Services.GetRequiredService<HostFeedConfigurationStore>();
        var saved = store.ReadSaved();
        Assert.True(saved.MavenEnabled);
        Assert.False(saved.NpmEnabled || saved.OciDefaultEnabled || saved.SwiftEnabled || saved.PubEnabled);
        await store.SaveAsync(saved with { NpmEnabled = true, OciDefaultEnabled = true }, Xunit.TestContext.Current.CancellationToken);
        using var merged = (ConfigurationRoot)new ConfigurationBuilder().AddConfiguration(configuration).AddJsonFile(path).Build();
        Assert.True(merged.GetValue<bool>("Feed:Maven:Enabled"));
        Assert.True(merged.GetValue<bool>("Feed:Npm:Enabled"));
        Assert.True(merged.GetValue<bool>("Feed:Oci:Custom:Enabled"));
        Assert.Equal("https://registry.example.test/", merged["Feed:Oci:Default:Mirror:RegistryUrl"]);
    }

    [Theory]
    [InlineData("FileSystem")]
    [InlineData("filesystem")]
    [InlineData("FILESYSTEM")]
    public void FileSystemStorageCasingPreservesTheDurableSettingsDirectory(string storageType)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Type"] = storageType,
            ["Storage:Path"] = "/tmp/durable-native-feeds",
        }).Build();
        Assert.Equal("/tmp/durable-native-feeds/feed-settings.json",
            HostFeedConfigurationStore.ConfigurationPath(configuration, "/tmp/content-root"));
    }

    [Fact]
    public async Task SettingsRequireAdministratorAndAntiforgeryAndShowPersistedNativeControls()
    {
        await using var factory = new ManagedHostFactory();
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddAuthentication("Cookies").AddCookie("Cookies");
            services.AddSingleton<IStartupFilter, CaptureCookieStartupFilter>();
        }));
        using var client = app.CreateClient(new() { AllowAutoRedirect = false });
        var config = app.Services.GetRequiredService<IConfiguration>();
        var path = config["Host:FeedConfigurationPath"]!;
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Settings")).StatusCode);
        await client.GetAsync("/__capture/login?role=reader");
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Settings")).StatusCode);
        Assert.False(File.Exists(path));
        await client.GetAsync("/__capture/login?role=admin");
        var html = await client.GetStringAsync("/Account/Settings");
        Assert.DoesNotContain("Deployment notes", html);
        Assert.DoesNotContain("shared configuration volume", html);
        foreach (var control in new[] { "Feed.NpmEnabled", "Feed.OciDefaultEnabled", "Feed.OciDockerEnabled", "Feed.OciHelmEnabled", "Feed.MavenEnabled", "Feed.SwiftEnabled", "Feed.PubEnabled", "Feed.PublicBaseUrl" })
            Assert.Contains($"name=\"{control}\"", html);
        var fields = new Dictionary<string, string>
        {
            ["Feed.MavenEnabled"] = "true", ["Feed.SwiftEnabled"] = "true", ["Feed.PubEnabled"] = "true",
            ["Feed.PublicBaseUrl"] = "https://packages.example.test/prefix/", ["Feed.ReadOnly"] = "true",
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Account/Settings", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.False(File.Exists(path));
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        using var saved = await client.PostAsync("/Account/Settings", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var edited = app.Services.GetRequiredService<HostFeedConfigurationStore>().ReadSaved();
        Assert.True(edited.MavenEnabled && edited.SwiftEnabled && edited.PubEnabled && edited.ReadOnly);
        var savedHtml = await client.GetStringAsync("/Account/Settings");
        Assert.Contains("Restart the host to apply feed changes.", savedHtml);
        Assert.Equal(1, Regex.Matches(savedHtml, "role=\"status\"").Count);
        var before = await File.ReadAllTextAsync(path);
        fields["Feed.PublicBaseUrl"] = "https://user:secret@packages.example.test/";
        using var invalid = await client.PostAsync("/Account/Settings", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("Use an HTTPS public feed URL", await invalid.Content.ReadAsStringAsync());
        Assert.Equal(before, await File.ReadAllTextAsync(path));
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IHostIdentityContext>();
            var user = await db.HostUsers.SingleAsync();
            user.IsAdmin = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/Account/Settings", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(before, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task SavedConfigurationPersistsAndIsLoadedAsStartupOverridesWithoutSavingCredentials()
    {
        await using var factory = new ManagedHostFactory();
        using var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<HostFeedConfigurationStore>();
        var active = store.Active;
        var edited = active with { NpmEnabled = true, OciDefaultEnabled = true, OciDockerEnabled = true, OciHelmEnabled = true, MavenEnabled = true, SwiftEnabled = true, PubEnabled = true,
            PublicBaseUrl = "https://packages.example.test/prefix/", ReadOnly = true, AllowAnonymousPull = false,
            MavenArtifactMiB = 1m / 1048576m, SwiftArtifactMiB = 4096, SwiftExpandedArchiveMiB = 8192,
            SwiftMaxArchiveEntries = 23, PubMaxArchiveEntries = 31 };
        await store.SaveAsync(edited, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(edited, store.ReadSaved());
        Assert.Equal(active, store.Active);
        var config = factory.Services.GetRequiredService<IConfiguration>();
        var path = config["Host:FeedConfigurationPath"]!;
        using var restarted = (ConfigurationRoot)new ConfigurationBuilder().AddJsonFile(path).Build();
        Assert.Equal(edited, HostFeedConfiguration.Read(restarted));
        Assert.True(restarted.GetValue<bool>("Feed:Npm:Enabled"));
        Assert.True(restarted.GetValue<bool>("Feed:Oci:Default:Enabled"));
        Assert.True(restarted.GetValue<bool>("Feed:Oci:Docker:Enabled"));
        Assert.True(restarted.GetValue<bool>("Feed:Oci:Helm:Enabled"));
        Assert.Equal(1, restarted.GetValue<long>("Feed:Maven:MaxArtifactBytes"));
        Assert.Equal(4294967296L, restarted.GetValue<long>("Feed:Swift:MaxArtifactBytes"));
        Assert.Equal(31, restarted.GetValue<int>("Feed:Pub:MaxArchiveEntries"));
        Assert.Null(restarted["Feed:Maven:MaxExpandedArchiveBytes"]);
        Assert.Null(restarted["Feed:Maven:MaxArchiveEntries"]);
        Assert.DoesNotContain("ApiKey", await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("http://public.example.test")]
    [InlineData("https://name:secret@example.test")]
    [InlineData("https://example.test?token=secret")]
    public void NativeConfigurationRejectsUnsafePublicUrls(string url)
    {
        var value = new HostFeedConfiguration { PubEnabled = true, PublicBaseUrl = url };
        Assert.False(Validator.TryValidateObject(value, new(value), new List<ValidationResult>(), true));
    }
}
