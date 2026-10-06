using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class SwiftToolchainTests
{
    private const string Version = "3.0.174-g932ff14073";
    private const string PublicRelease = "https://github.com/AvantiPoint/appportal-apple-packages/releases/download/v" + Version + "/";
    private static readonly (string Name, string Hash, string Smoke)[] Modules =
    [
        ("AppPortalTelemetry", "9355ea8a91cf6562562c5d55b68d546e0547457ec3ea9fff7d9bd52b31a5a133", "let client: AppPortalClient = AppPortal.current; print(type(of: client))"),
        ("AppPortalMessaging", "56121e3ffdeb6dbe616b3c3d62c04340ec0b5a4d09a290fa7289805ffef33bfa", "print(AppPortalMessages.handlePushPayload([\"title\": \"Hello\"]) as Any)"),
        ("AppPortalLocation", "c3f2283380dc78698916881c8ee6aa68deb26b50ae82d4c6d4a536a5ceeb2f71", "print(String(describing: LiveAppPortalLocation.self))"),
        ("AppPortalSmartLinks", "6d00bec6da986be65f6a0dee16c71f0304311ae4031106d19b9119f3a524a817", "print(AppPortalSmartLinkHandlingResult.rejected(.invalidAssociatedURL))"),
    ];

    [Fact]
    public async Task SwiftPmColdConsumersUseReadOnlyNetrcAndVerifiedBinaryArtifacts()
    {
        var swift = Environment.GetEnvironmentVariable("AVP_SWIFT_EXECUTABLE");
        var certificate = Environment.GetEnvironmentVariable("AVP_SWIFT_CERTIFICATE_PATH");
        if (!OperatingSystem.IsMacOS() || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted"
            || string.IsNullOrWhiteSpace(swift) || string.IsNullOrWhiteSpace(certificate))
            Assert.Skip("Set AVP_SWIFT_EXECUTABLE and AVP_SWIFT_CERTIFICATE_PATH on an approved macOS TLS test runner.");

        var ct = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("avp-swift-client-").FullName;
        try
        {
            // The runner provisions trust separately. Never disable TLS validation here.
            await using var host = await NativeTestHost.StartAsync(realHttp: true, certificatePath: certificate,
                maxArtifactBytes: 256L * 1024 * 1024);
            Assert.Equal("https", host.Client.BaseAddress!.Scheme);
            Assert.True(host.Client.BaseAddress.IsLoopback);
            using var publicClient = new HttpClient(); // No feed credentials, netrc or keychain.
            host.Authenticate("writer");
            foreach (var module in Modules)
            {
                var archive = Path.Combine(root, module.Name + ".xcframework.zip");
                using var response = await publicClient.GetAsync(PublicRelease + module.Name + "-" + Version + ".xcframework.zip",
                    HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
                Assert.Equal("https", response.RequestMessage!.RequestUri!.Scheme);
                await using (var input = await response.Content.ReadAsStreamAsync(ct))
                await using (var output = File.Create(archive))
                {
                    var buffer = new byte[81920];
                    long length = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, ct)) != 0)
                    {
                        length += read;
                        Assert.True(length <= 256L * 1024 * 1024, "Public fixture exceeded its download limit.");
                        await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    }
                }
                await using var bytes = File.OpenRead(archive);
                Assert.Equal(module.Hash, Convert.ToHexStringLower(await SHA256.HashDataAsync(bytes, ct)));
                bytes.Position = 0;
                using var content = new StreamContent(bytes);
                using var published = await host.Client.PutAsync(ArtifactPath(module.Name), content, ct);
                Assert.Equal(HttpStatusCode.Created, published.StatusCode);
                using var metadata = JsonDocument.Parse(await published.Content.ReadAsStringAsync(ct));
                Assert.Equal(module.Hash, metadata.RootElement.GetProperty("checksum").GetString());
                Assert.Equal(new Uri(host.Client.BaseAddress, ArtifactPath(module.Name)).AbsoluteUri,
                    metadata.RootElement.GetProperty("url").GetString());
            }

            host.Client.DefaultRequestHeaders.Authorization = null;
            using var unauthenticated = await host.Client.GetAsync(ArtifactPath(Modules[0].Name), ct);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
            Assert.Contains(unauthenticated.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Basic");
            host.Authenticate("reader");
            using var forbidden = await host.Client.PutAsync("/swift/appportal/99.0.0/AppPortalTelemetry.xcframework.zip", new ByteArrayContent([]), ct);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            host.Client.DefaultRequestHeaders.Authorization = null;

            var anonymous = await RunConsumer(swift!, root, "anonymous", Modules[0], host.Client.BaseAddress, null, false, ct);
            Assert.NotEqual(0, anonymous.ExitCode);
            Assert.Contains("401", anonymous.Output);
            var invalid = await RunConsumer(swift!, root, "invalid", Modules[0], host.Client.BaseAddress, "invalid-fixture-token", false, ct);
            Assert.NotEqual(0, invalid.ExitCode);
            Assert.Contains("401", invalid.Output);
            var badChecksum = await RunConsumer(swift!, root, "bad-checksum", Modules[0], host.Client.BaseAddress, "reader", true, ct);
            Assert.NotEqual(0, badChecksum.ExitCode);
            Assert.Contains("checksum", badChecksum.Output, StringComparison.OrdinalIgnoreCase);

            foreach (var module in Modules)
            {
                host.Requests.Clear();
                var result = await RunConsumer(swift!, root, module.Name, module, host.Client.BaseAddress, "reader", false, ct);
                Assert.True(result.ExitCode == 0, $"SwiftPM {module.Name} failed: {result.Output}");
                Assert.Contains("AVP_SWIFT_SMOKE_OK", result.Output);
                Assert.Contains(host.Requests, request => request.Method == "GET" && request.Path == ArtifactPath(module.Name)
                    && request.Status == 200 && request.AuthenticationScheme == "Basic" && request.Location.Length == 0);
                Assert.DoesNotContain(host.Requests, request => request.Status is >= 300 and < 400 || request.Location.Length > 0);
            }
        }
        finally
        {
            foreach (var attempt in new[] { "anonymous", "invalid", "bad-checksum" }.Concat(Modules.Select(module => module.Name)))
            {
                var credentials = Path.Combine(root, attempt, "credentials.netrc");
                if (File.Exists(credentials)) File.Delete(credentials);
            }
            // Xcode can place read-only SDK/mount content beneath build workspaces.
            // Never traverse those mounts or mask a test result with recursive cleanup.
            // This opt-in qualification runs on a disposable macOS runner, which owns teardown.
        }
    }

    private static string ArtifactPath(string module) => $"/swift/appportal/{Version}/{module}.xcframework.zip";

    private static async Task<(int ExitCode, string Output)> RunConsumer(string swift, string root, string attempt,
        (string Name, string Hash, string Smoke) module, Uri baseUri, string? token, bool badChecksum, CancellationToken ct)
    {
        var directory = Path.Combine(root, attempt);
        var binaryPackage = Path.Combine(directory, "binary-package");
        var consumer = Path.Combine(directory, "consumer");
        Directory.CreateDirectory(binaryPackage);
        Directory.CreateDirectory(Path.Combine(consumer, "Sources", "Smoke"));
        var products = string.Join(",\n", Modules.Select(m =>
            $".library(name: \"{m.Name}\", targets: [{(m.Name == Modules[0].Name ? "" : "\"AppPortalTelemetry\", ")}\"{m.Name}\"])"));
        var targets = string.Join(",\n", Modules.Select(m =>
            $".binaryTarget(name: \"{m.Name}\", url: \"{new Uri(baseUri, ArtifactPath(m.Name))}\", checksum: \"{(badChecksum && m.Name == module.Name ? new string('0', 64) : m.Hash)}\")"));
        await File.WriteAllTextAsync(Path.Combine(binaryPackage, "Package.swift"), $$"""
            // swift-tools-version: 5.9
            import PackageDescription
            let package = Package(name: "AppPortalApple",
                platforms: [.iOS(.v13), .macOS(.v11), .tvOS(.v13), .watchOS(.v6), .macCatalyst(.v13)],
                products: [{{products}}], targets: [{{targets}}])
            """, ct);
        await File.WriteAllTextAsync(Path.Combine(consumer, "Package.swift"), $$"""
            // swift-tools-version: 5.9
            import PackageDescription
            let package = Package(name: "Smoke", platforms: [.macOS(.v11)],
                dependencies: [.package(path: "../binary-package")],
                targets: [.executableTarget(name: "Smoke", dependencies: [
                    .product(name: "{{module.Name}}", package: "binary-package")])])
            """, ct);
        await File.WriteAllTextAsync(Path.Combine(consumer, "Sources", "Smoke", "main.swift"),
            $"import {module.Name}\n{module.Smoke}\nprint(\"AVP_SWIFT_SMOKE_OK\")\n", ct);
        var arguments = new List<string>
        {
            "run", "--package-path", consumer, "--configuration", "release", "--scratch-path", Path.Combine(directory, "build"),
            "--cache-path", Path.Combine(directory, "cache"), "--config-path", Path.Combine(directory, "config"),
            "--security-path", Path.Combine(directory, "security"), "--disable-dependency-cache", "--disable-keychain",
        };
        if (token is null) arguments.Add("--disable-netrc");
        else
        {
            var netrc = Path.Combine(directory, "credentials.netrc");
            await File.WriteAllTextAsync(netrc, $"machine {baseUri.Host} login person@example.test password {token}\n", ct);
            if (OperatingSystem.IsMacOS()) File.SetUnixFileMode(netrc, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            else throw new PlatformNotSupportedException("Swift binary qualification requires macOS.");
            arguments.AddRange(["--netrc-file", netrc]);
        }
        arguments.Add("Smoke");
        var start = new ProcessStartInfo(swift) { WorkingDirectory = directory, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false };
        start.Environment.Clear();
        foreach (var name in new[] { "PATH", "DEVELOPER_DIR", "TMPDIR" })
            if (Environment.GetEnvironmentVariable(name) is { } value) start.Environment[name] = value;
        var home = Directory.CreateDirectory(Path.Combine(directory, "home")).FullName;
        start.Environment["HOME"] = home;
        start.Environment["CFFIXED_USER_HOME"] = home;
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["CLANG_MODULE_CACHE_PATH"] = Path.Combine(directory, "clang-cache");
        start.Environment["SWIFTPM_MODULECACHE_OVERRIDE"] = Path.Combine(directory, "swift-module-cache");
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(4));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start SwiftPM.");
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        var output = (await stdout + await stderr).Replace("person@example.test", "[test-user]", StringComparison.Ordinal);
        // Fixture credentials are synthetic, but never include them in failure logs.
        foreach (var credential in new[] { "reader", "writer", "invalid-fixture-token" })
        {
            output = output.Replace(Convert.ToBase64String(Encoding.UTF8.GetBytes("person@example.test:" + credential)), "[redacted]", StringComparison.Ordinal);
            output = output.Replace(credential, "[redacted]", StringComparison.Ordinal);
        }
        return (process.ExitCode, output);
    }
}
