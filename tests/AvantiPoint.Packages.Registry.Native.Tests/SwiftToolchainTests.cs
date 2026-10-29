using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class SwiftToolchainTests
{
    private const string Version = "1.0.0";
    private static readonly string[] ModuleNames =
        ["NativeFixtureAlpha", "NativeFixtureBeta", "NativeFixtureGamma", "NativeFixtureDelta"];

    [Fact]
    public async Task SwiftPmColdConsumersUseReadOnlyNetrcAndVerifiedBinaryArtifacts()
    {
        var swift = Environment.GetEnvironmentVariable("AVP_SWIFT_EXECUTABLE");
        var certificate = Environment.GetEnvironmentVariable("AVP_SWIFT_CERTIFICATE_PATH");
        var fixtures = Environment.GetEnvironmentVariable("AVP_SWIFT_FIXTURE_DIRECTORY");
        if (!OperatingSystem.IsMacOS() || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted"
            || string.IsNullOrWhiteSpace(swift) || string.IsNullOrWhiteSpace(certificate) || string.IsNullOrWhiteSpace(fixtures))
            Assert.Skip("Set AVP_SWIFT_EXECUTABLE, AVP_SWIFT_CERTIFICATE_PATH and AVP_SWIFT_FIXTURE_DIRECTORY on an approved macOS TLS test runner.");

        var ct = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("avp-swift-client-").FullName;
        var modules = new List<(string Name, string Hash, string Smoke)>();
        foreach (var name in ModuleNames)
        {
            await using var archive = File.OpenRead(Path.Combine(fixtures!, name + ".xcframework.zip"));
            Assert.True(archive.Length <= 256L * 1024 * 1024, "Synthetic fixture exceeded its size limit.");
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(archive, ct));
            modules.Add((name, hash, "print(fixtureGreeting())"));
        }
        try
        {
            // The runner provisions trust separately. Never disable TLS validation here.
            await using var host = await NativeTestHost.StartAsync(realHttp: true, certificatePath: certificate,
                maxArtifactBytes: 256L * 1024 * 1024);
            Assert.Equal("https", host.Client.BaseAddress!.Scheme);
            Assert.True(host.Client.BaseAddress.IsLoopback);
            host.Authenticate("writer");
            foreach (var module in modules)
            {
                // These archives are compiled from synthetic public test source
                // on this disposable runner; they contain no product SDK artifacts.
                await using var bytes = File.OpenRead(Path.Combine(fixtures!, module.Name + ".xcframework.zip"));
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
            using var unauthenticated = await host.Client.GetAsync(ArtifactPath(modules[0].Name), ct);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
            Assert.Contains(unauthenticated.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Basic");
            host.Authenticate("reader");
            using var forbidden = await host.Client.PutAsync("/swift/native-fixtures/99.0.0/NativeFixtureAlpha.xcframework.zip", new ByteArrayContent([]), ct);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            host.Client.DefaultRequestHeaders.Authorization = null;

            var anonymous = await RunConsumer(swift!, root, "anonymous", modules[0], modules, host.Client.BaseAddress, null, false, ct);
            Assert.NotEqual(0, anonymous.ExitCode);
            Assert.Contains("401", anonymous.Output);
            var invalid = await RunConsumer(swift!, root, "invalid", modules[0], modules, host.Client.BaseAddress, "invalid-fixture-token", false, ct);
            Assert.NotEqual(0, invalid.ExitCode);
            Assert.Contains("401", invalid.Output);
            var badChecksum = await RunConsumer(swift!, root, "bad-checksum", modules[0], modules, host.Client.BaseAddress, "reader", true, ct);
            Assert.NotEqual(0, badChecksum.ExitCode);
            Assert.Contains("checksum", badChecksum.Output, StringComparison.OrdinalIgnoreCase);

            foreach (var module in modules)
            {
                host.Requests.Clear();
                var result = await RunConsumer(swift!, root, module.Name, module, modules, host.Client.BaseAddress, "reader", false, ct);
                Assert.True(result.ExitCode == 0, $"SwiftPM {module.Name} failed: {result.Output}");
                Assert.Contains("AVP_SWIFT_SMOKE_OK", result.Output);
                Assert.Contains(host.Requests, request => request.Method == "GET" && request.Path == ArtifactPath(module.Name)
                    && request.Status == 200 && request.AuthenticationScheme == "Basic" && request.Location.Length == 0);
                Assert.DoesNotContain(host.Requests, request => request.Status is >= 300 and < 400 || request.Location.Length > 0);
            }
        }
        finally
        {
            foreach (var attempt in new[] { "anonymous", "invalid", "bad-checksum" }.Concat(modules.Select(module => module.Name)))
            {
                var credentials = Path.Combine(root, attempt, "credentials.netrc");
                if (File.Exists(credentials)) File.Delete(credentials);
            }
            // Xcode can place read-only SDK/mount content beneath build workspaces.
            // Never traverse those mounts or mask a test result with recursive cleanup.
            // This opt-in qualification runs on a disposable macOS runner, which owns teardown.
        }
    }

    private static string ArtifactPath(string module) => $"/swift/native-fixtures/{Version}/{module}.xcframework.zip";

    private static async Task<(int ExitCode, string Output)> RunConsumer(string swift, string root, string attempt,
        (string Name, string Hash, string Smoke) module, IReadOnlyList<(string Name, string Hash, string Smoke)> modules, Uri baseUri, string? token, bool badChecksum, CancellationToken ct)
    {
        var directory = Path.Combine(root, attempt);
        var binaryPackage = Path.Combine(directory, "binary-package");
        var consumer = Path.Combine(directory, "consumer");
        Directory.CreateDirectory(binaryPackage);
        Directory.CreateDirectory(Path.Combine(consumer, "Sources", "Smoke"));
        var products = string.Join(",\n", modules.Select(m =>
            $".library(name: \"{m.Name}\", targets: [\"{m.Name}\"])"));
        var targets = string.Join(",\n", modules.Select(m =>
            $".binaryTarget(name: \"{m.Name}\", url: \"{new Uri(baseUri, ArtifactPath(m.Name))}\", checksum: \"{(badChecksum && m.Name == module.Name ? new string('0', 64) : m.Hash)}\")"));
        await File.WriteAllTextAsync(Path.Combine(binaryPackage, "Package.swift"), $$"""
            // swift-tools-version: 5.9
            import PackageDescription
            let package = Package(name: "SyntheticNativeFixtures",
                platforms: [.macOS(.v11)],
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
