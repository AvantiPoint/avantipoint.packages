using System.Diagnostics;

namespace AvantiPoint.Packages.Registry.Native.Tests;

/// <summary>Opt-in real native tools, isolated caches and generated public fixtures only.</summary>
public sealed class NativeToolchainTests
{
    [Fact]
    public async Task DartPubPublishesAndColdConsumerBuildsWithReadOnlyToken()
    {
        var dart = Environment.GetEnvironmentVariable("AVP_DART_EXECUTABLE");
        if (string.IsNullOrWhiteSpace(dart)) Assert.Skip("Set AVP_DART_EXECUTABLE to run the real Dart pub compatibility test.");
        await using var host = await NativeTestHost.StartAsync(realHttp: true);
        var root = Directory.CreateTempSubdirectory("avp-dart-client-").FullName;
        try
        {
            var url = new Uri(host.Client.BaseAddress!, "pub").AbsoluteUri;
            var publisher = WriteDirectory(root, "publisher", new()
            {
                ["pubspec.yaml"] = $"name: native_fixture_sdk\nversion: 1.0.0\ndescription: Generated native feed test package.\npublish_to: {url}\nenvironment:\n  sdk: '>=3.0.0 <4.0.0'\n",
                ["lib/native_fixture_sdk.dart"] = "String greeting() => 'native feed works';\n",
                ["README.md"] = "# Fixture\nGenerated native compatibility fixture.\n",
                ["CHANGELOG.md"] = "## 1.0.0\nInitial fixture.\n",
                ["LICENSE"] = "This generated test fixture is dedicated to the public domain.\n",
            });
            var env = EnvironmentFor(root, "writer");
            await Run(dart!, publisher, env, "--disable-analytics");
            await Run(dart!, publisher, env, "pub", "token", "add", url, "--env-var", "PACKAGES_TOKEN");
            await Run(dart!, publisher, env, "pub", "publish", "--force");
            var consumer = WriteDirectory(root, "consumer", new()
            {
                ["pubspec.yaml"] = $"name: native_consumer\npublish_to: none\nenvironment:\n  sdk: '>=3.0.0 <4.0.0'\ndependencies:\n  native_fixture_sdk:\n    hosted: {url}\n    version: ^1.0.0\n",
                ["bin/main.dart"] = "import 'package:native_fixture_sdk/native_fixture_sdk.dart'; void main() { print(greeting()); }\n",
            });
            env["PACKAGES_TOKEN"] = "reader";
            env["PUB_CACHE"] = Path.Combine(root, "cold-consumer-cache");
            await Run(dart!, consumer, env, "pub", "get");
            Assert.Contains("native feed works", await Run(dart!, consumer, env, "run", "bin/main.dart"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task GradlePublishesPomModuleAndJarAndColdConsumerCompiles()
    {
        var gradle = Environment.GetEnvironmentVariable("AVP_GRADLE_EXECUTABLE");
        if (string.IsNullOrWhiteSpace(gradle)) Assert.Skip("Set AVP_GRADLE_EXECUTABLE and JAVA_HOME (a full JDK) for real Gradle compatibility testing.");
        await using var host = await NativeTestHost.StartAsync(realHttp: true);
        var root = Directory.CreateTempSubdirectory("avp-gradle-client-").FullName;
        try
        {
            var url = new Uri(host.Client.BaseAddress!, "maven").AbsoluteUri;
            var repository = $"maven {{ url = uri('{url}'); allowInsecureProtocol = true; credentials {{ username = 'person@example.test'; password = System.getenv('PACKAGES_TOKEN') }}; authentication {{ basic(BasicAuthentication) }} }}";
            var publisher = WriteDirectory(root, "publisher", new()
            {
                ["settings.gradle"] = "rootProject.name = 'native-fixture'\n",
                ["build.gradle"] = "plugins { id 'java-library'; id 'maven-publish' }\ngroup = 'com.example'\nversion = '1.0.0'\npublishing { publications { library(MavenPublication) { from components.java } }; repositories { " + repository + " } }\n",
                ["src/main/java/com/example/Greeting.java"] = "package com.example; public class Greeting { public static String say() { return \"native feed works\"; } }\n",
            });
            var env = EnvironmentFor(root, "writer");
            await Run(gradle!, publisher, env, "--no-daemon", "--console=plain", "--max-workers=1", "publish");
            host.Authenticate("reader");
            Assert.Contains("component", await host.Client.GetStringAsync(
                "/maven/com/example/native-fixture/1.0.0/native-fixture-1.0.0.module", TestContext.Current.CancellationToken));
            var consumer = WriteDirectory(root, "consumer", new()
            {
                ["settings.gradle"] = "rootProject.name = 'native-consumer'\n",
                ["build.gradle"] = "plugins { id 'application' }\nrepositories { " + repository + " }\ndependencies { implementation 'com.example:native-fixture:1.0.0' }\napplication { mainClass = 'Consumer' }\n",
                ["src/main/java/Consumer.java"] = "import com.example.Greeting; public class Consumer { public static void main(String[] args) { System.out.println(Greeting.say()); } }\n",
            });
            env["PACKAGES_TOKEN"] = "reader";
            env["GRADLE_USER_HOME"] = Path.Combine(root, "cold-gradle-cache");
            Assert.Contains("native feed works", await Run(gradle!, consumer, env, "--no-daemon", "--console=plain", "--max-workers=1", "run"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static Dictionary<string, string> EnvironmentFor(string root, string token) => new()
    {
        ["HOME"] = Path.Combine(root, "home"), ["USERPROFILE"] = Path.Combine(root, "home"),
        ["XDG_CONFIG_HOME"] = Path.Combine(root, "config"), ["CI"] = "true",
        ["PUB_CACHE"] = Path.Combine(root, "pub-cache"), ["GRADLE_USER_HOME"] = Path.Combine(root, "gradle-cache"),
        ["PACKAGES_TOKEN"] = token,
    };

    private static string WriteDirectory(string root, string name, Dictionary<string, string> files)
    {
        var directory = Path.Combine(root, name);
        foreach (var (path, content) in files)
        {
            var target = Path.Combine(directory, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, content);
        }
        return directory;
    }

    private static async Task<string> Run(string executable, string directory, Dictionary<string, string> environment, params string[] args)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in args) start.ArgumentList.Add(argument);
        foreach (var (name, value) in environment) start.Environment[name] = value;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start native tool.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        var output = await stdout + await stderr;
        Assert.True(process.ExitCode == 0, $"{Path.GetFileName(executable)} {string.Join(' ', args)} failed ({process.ExitCode}):\n{output}");
        return output;
    }
}
