using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Registry.Native.Maven;
using AvantiPoint.Packages.Registry.Native.Pub;
using AvantiPoint.Packages.Registry.Native.Storage;
using AvantiPoint.Packages.Registry.Native.Swift;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class NativeRegistryTests
{
    private const string MavenPath = "/maven/com/example/test-sdk/1.0.0/test-sdk-1.0.0.aar";

    [Theory]
    [InlineData("../escape")]
    [InlineData("/root/file")]
    [InlineData("a/%2e%2e/b")]
    [InlineData("a/%252e%252e/b")]
    [InlineData("a\\b")]
    [InlineData("a//b")]
    [InlineData("a/b.")]
    [InlineData("a/.hidden")]
    public void RejectsUnsafeStoragePaths(string path) => Assert.False(ArtifactPath.IsValid(path));

    [Theory]
    [InlineData("com/example/sdk/1.0.0/sdk-1.0.0.aar")]
    [InlineData("com/example/sdk/1.0.0/sdk-1.0.0.pom.sha256")]
    [InlineData("com/example/sdk/1.0.0/sdk-1.0.0.module")]
    [InlineData("com/example/sdk/1.0.0/sdk-1.0.0-sources.jar.asc")]
    [InlineData("com/example/sdk/maven-metadata.xml.sha512")]
    public void AcceptsNativeMavenPaths(string path) => Assert.True(MavenArtifactPath.TryParse(path, out _));

    [Theory]
    [InlineData("com/example/sdk/1.0.0/other-1.0.0.aar")]
    [InlineData("com/example/sdk/1.0-SNAPSHOT/sdk-1.0-SNAPSHOT.aar")]
    [InlineData("com/example/sdk/1.0.0/sdk-1.0.0.exe")]
    public void RejectsInvalidMavenCoordinates(string path) => Assert.False(MavenArtifactPath.TryParse(path, out _));

    [Fact]
    public async Task MavenRequiresAuthenticationAndSeparatesReadWriteScopes()
    {
        await using var host = await NativeTestHost.StartAsync();
        var missing = await host.Client.GetAsync(MavenPath);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Contains("Basic", missing.Headers.WwwAuthenticate.ToString());
        host.Authenticate("reader");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PutAsync(MavenPath, new StringContent("binary"))).StatusCode);
        host.Authenticate("writer");
        Assert.Equal(HttpStatusCode.Created, (await host.Client.PutAsync(MavenPath, new StringContent("binary"))).StatusCode);
        host.Authenticate("reader");
        Assert.Equal("binary", await host.Client.GetStringAsync(MavenPath));
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/maven/com/denied/sdk/1.0.0/sdk-1.0.0.aar")).StatusCode);
    }

    [Fact]
    public async Task MavenArtifactsAreImmutableAndChecksumsMatch()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        Assert.Equal(HttpStatusCode.Created, (await host.Client.PutAsync(MavenPath, new StringContent("first"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PutAsync(MavenPath, new StringContent("first"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PutAsync(MavenPath, new StringContent("second"))).StatusCode);
        var checksum = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("first")));
        Assert.Equal(checksum, await host.Client.GetStringAsync(MavenPath + ".sha256"));
        Assert.Equal(HttpStatusCode.Created, (await host.Client.PutAsync(MavenPath + ".sha256", new StringContent(checksum))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsync(MavenPath + ".sha256", new StringContent(new string('0', 64)))).StatusCode);
        Assert.Equal("first", await host.Client.GetStringAsync(MavenPath));
        using var head = await host.Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, MavenPath));
        Assert.Equal(5, head.Content.Headers.ContentLength);
        Assert.Equal(checksum, head.Headers.ETag!.Tag.Trim('"'));
    }

    [Fact]
    public async Task MavenMetadataIsDerivedFromCommittedPomCoordinates()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        var pom = "<project><groupId>com.example</groupId><artifactId>test-sdk</artifactId><version>1.0.0</version></project>";
        Assert.Equal(HttpStatusCode.Created, (await host.Client.PutAsync(MavenPath.Replace(".aar", ".pom"), new StringContent(pom))).StatusCode);
        var index = await host.Client.GetStringAsync("/maven/com/example/test-sdk/maven-metadata.xml");
        Assert.Contains("<version>1.0.0</version>", index);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsync(MavenPath.Replace(".aar", ".pom"), new StringContent(pom.Replace("com.example", "other")))).StatusCode);
    }

    [Fact]
    public async Task AnonymousPullDoesNotEnableAnonymousPush()
    {
        await using var host = await NativeTestHost.StartAsync(anonymous: true);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(MavenPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PutAsync(MavenPath, new StringContent("binary"))).StatusCode);
    }

    [Fact]
    public async Task BasicCredentialsSupportNativeTooling()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Client.DefaultRequestHeaders.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("person@example.test:writer")));
        Assert.Equal(HttpStatusCode.Created, (await host.Client.PutAsync(MavenPath, new StringContent("binary"))).StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = new("Basic", "not-base64");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(MavenPath)).StatusCode);
    }

    [Fact]
    public async Task PubNativePublishDownloadAndConflictFlow()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        var parameters = JsonDocument.Parse(await host.Client.GetStringAsync("/pub/api/packages/versions/new"));
        var uploadUrl = parameters.RootElement.GetProperty("url").GetString();
        Assert.Equal("https://registry.test/pub/api/packages/versions/upload", uploadUrl);
        var bytes = PubTar("name: example_sdk\nversion: 1.0.0\nenvironment:\n  sdk: ^3.0.0\n".Replace("\n", "\n"));
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(bytes), "file", "package.tar.gz");
        var uploaded = await host.Client.PostAsync(uploadUrl, multipart);
        Assert.Equal(HttpStatusCode.NoContent, uploaded.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync(uploaded.Headers.Location)).StatusCode);
        host.Authenticate("reader");
        using var versions = JsonDocument.Parse(await host.Client.GetStringAsync("/pub/api/packages/example_sdk"));
        var latest = versions.RootElement.GetProperty("latest");
        Assert.Equal("1.0.0", latest.GetProperty("version").GetString());
        Assert.Equal("^3.0.0", latest.GetProperty("pubspec").GetProperty("environment").GetProperty("sdk").GetString());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), latest.GetProperty("archive_sha256").GetString());
        Assert.Equal(bytes, await host.Client.GetByteArrayAsync(latest.GetProperty("archive_url").GetString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/pub/api/packages/versions/new")).StatusCode);
    }

    [Fact]
    public async Task SwiftBinaryUploadReturnsManifestChecksumAndRejectsSource()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        var bytes = Xcframework();
        var path = "/swift/example/1.0.0/Example.xcframework.zip";
        var response = await host.Client.PutAsync(path, new ByteArrayContent(bytes));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), json.RootElement.GetProperty("checksum").GetString());
        host.Authenticate("reader");
        Assert.Equal(bytes, await host.Client.GetByteArrayAsync(path));
        Assert.Contains("Example", await host.Client.GetStringAsync("/swift/example/1.0.0/index.json"));
        host.Authenticate("writer");
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsync(path, new ByteArrayContent(Xcframework(source: true)))).StatusCode);
    }

    [Fact]
    public async Task SwiftSourceMetadataIsRejectedBeforePublication()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        const string path = "/swift/example/1.0.0/Example.xcframework.zip";
        var ct = TestContext.Current.CancellationToken;
        foreach (var filename in new[] { "arm64.swiftsourceinfo", "arm64.SWIFTSOURCEINFO" })
        {
            using var content = new ByteArrayContent(Xcframework(source: true, sourceFilename: filename));
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsync(path, content, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(path, ct)).StatusCode);
        }
    }

    [Fact]
    public async Task ArtifactSizeLimitsApplyBeforeCommit()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await host.Client.PutAsync(MavenPath, new ByteArrayContent(new byte[1048577]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(MavenPath)).StatusCode);
    }

    [Fact]
    public async Task ArtifactMetadataDoesNotCrossFeeds()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        await host.Client.PutAsync(MavenPath, new StringContent("binary"));
        using var scope = host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<NativeArtifactStore>();
        var other = new SurfaceContext("other-feed", FeedProtocol.Maven, "maven", null, "/maven", new Uri("https://registry.test/maven/"));
        Assert.Null(await store.FindAsync(other, MavenPath[7..], CancellationToken.None));
    }

    [Fact]
    public async Task PubRejectsTraversalLinksAndYamlAliases()
    {
        var options = new NativeRegistryOptions();
        await Assert.ThrowsAsync<InvalidDataException>(() => PubArchive.ReadAsync(new MemoryStream(PubTar("name: test\nversion: 1.0.0\n", "../escape")), options, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => PubArchive.ReadAsync(new MemoryStream(PubTar("name: &name test\nversion: 1.0.0\nother: *name\n")), options, CancellationToken.None));
    }

    [Fact]
    public async Task AcceptsRealDartArchiveWithoutChangingPublishedBytes()
    {
        var encoded = await File.ReadAllTextAsync(System.IO.Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "dart-pub-3.13.5.tar.gz.b64"), TestContext.Current.CancellationToken);
        var bytes = Convert.FromBase64String(encoded);
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(bytes), "file", "package.tar.gz");
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PostAsync(
            "/pub/api/packages/versions/upload", multipart, TestContext.Current.CancellationToken)).StatusCode);
        host.Authenticate("reader");
        Assert.Equal(bytes, await host.Client.GetByteArrayAsync(
            "/pub/packages/native_fixture_sdk/versions/1.0.0.tar.gz", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentDifferentPublicationsCannotReplaceAnIdentity()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        var results = await Task.WhenAll(
            host.Client.PutAsync(MavenPath, new StringContent("one"), TestContext.Current.CancellationToken),
            host.Client.PutAsync(MavenPath, new StringContent("two"), TestContext.Current.CancellationToken));
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
        var body = await host.Client.GetStringAsync(MavenPath, TestContext.Current.CancellationToken);
        Assert.Contains(body, new[] { "one", "two" });
    }

    [Fact]
    public async Task SqliteMigrationsIncludeNativeArtifactsAndMatchModel()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "native-migration-" + Guid.NewGuid() + ".db");
        try
        {
            await using var db = new AvantiPoint.Packages.Database.Sqlite.SqliteContext(
                new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AvantiPoint.Packages.Database.Sqlite.SqliteContext>()
                    .UseSqlite("Data Source=" + path).Options);
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(0, await db.NativeArtifacts.CountAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public void XcframeworkLinksMustResolveToExistingEntriesWithoutCycles()
    {
        var options = new NativeRegistryOptions();
        XcframeworkValidator.Validate(new MemoryStream(Xcframework(symlink: "Example")), "Example", options);
        Assert.Throws<InvalidDataException>(() => XcframeworkValidator.Validate(
            new MemoryStream(Xcframework(symlink: "Missing")), "Example", options));
        Assert.Throws<InvalidDataException>(() => XcframeworkValidator.Validate(
            new MemoryStream(Xcframework(symlink: "Link")), "Example", options));
    }

    [Fact]
    public async Task MavenPathAuthorizationIsNotRecheckedAsADifferentDigestIdentity()
    {
        await using var host = await NativeTestHost.StartAsync(pathAuthorization: true);
        host.Authenticate("writer");
        Assert.Equal(HttpStatusCode.Created, (await host.Client.PutAsync(MavenPath, new StringContent("binary"), TestContext.Current.CancellationToken)).StatusCode);
        host.Authenticate("reader");
        Assert.Equal("binary", await host.Client.GetStringAsync(MavenPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NativeAuthenticationPreservesProviderFailureHeaders()
    {
        await using var host = await NativeTestHost.StartAsync(customChallenge: true);
        host.Authenticate("expired");
        var result = await host.Client.GetAsync(MavenPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Contains("native-test", result.Headers.WwwAuthenticate.ToString());
        Assert.Equal("rotate-token", Assert.Single(result.Headers.GetValues("X-Feed-Recovery")));
    }

    [Fact]
    public void XcframeworkDirectoriesAndPrivateInterfacesAreNotPublicPayloads()
    {
        var options = new NativeRegistryOptions();
        foreach (var bytes in new[] { Xcframework(directoryBinary: true), Xcframework(directoryInterface: true), Xcframework(privateInterface: true) })
            Assert.Throws<InvalidDataException>(() => XcframeworkValidator.Validate(new MemoryStream(bytes), "Example", options));
    }

    [Fact]
    public async Task NativePublishingHonorsReadOnlyMode()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AvantiPoint.Packages.Core.PackageFeedOptions>>()
            .Value.IsReadOnlyMode = true;
        host.Authenticate("writer");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PutAsync(MavenPath, new StringContent("binary"), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(MavenPath, TestContext.Current.CancellationToken)).StatusCode);
    }

    private static byte[] PubTar(string pubspec, string extra = "lib/example.dart")
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var tar = new TarWriter(gzip, leaveOpen: true))
        {
            tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "pubspec.yaml") { DataStream = new MemoryStream(Encoding.UTF8.GetBytes(pubspec)) });
            tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, extra) { DataStream = new MemoryStream(Encoding.UTF8.GetBytes("class Example {}")) });
        }
        return output.ToArray();
    }

    private static byte[] Xcframework(bool source = false, string? symlink = null, bool directoryBinary = false, bool directoryInterface = false, bool privateInterface = false, string sourceFilename = "Secret.swift")
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string text) { using var writer = new StreamWriter(zip.CreateEntry(path).Open()); writer.Write(text); }
            Add("Example.xcframework/Info.plist", "<plist><dict><key>AvailableLibraries</key><array><dict><key>LibraryIdentifier</key><string>ios-arm64</string><key>LibraryPath</key><string>Example.framework</string><key>SupportedPlatform</key><string>ios</string><key>SupportedArchitectures</key><array><string>arm64</string></array></dict></array></dict></plist>");
            const string binary = "Example.xcframework/ios-arm64/Example.framework/Example";
            var publicInterface = "Example.xcframework/ios-arm64/Example.framework/Modules/Example.swiftmodule/arm64-apple-ios."
                + (privateInterface ? "private." : "") + "swiftinterface";
            if (directoryBinary) zip.CreateEntry(binary + "/"); else Add(binary, "binary");
            if (directoryInterface) zip.CreateEntry(publicInterface + "/"); else Add(publicInterface, "public struct Example {}");
            if (symlink is not null)
            {
                var entry = zip.CreateEntry("Example.xcframework/ios-arm64/Example.framework/Link");
                entry.ExternalAttributes = (0xA000 | 0x1ff) << 16;
                using var writer = new StreamWriter(entry.Open());
                writer.Write(symlink);
            }
            if (source) Add("Example.xcframework/ios-arm64/Example.framework/" + sourceFilename, "private implementation");
        }
        return output.ToArray();
    }
}
