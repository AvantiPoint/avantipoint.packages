using System.Text;
using System.Xml.Linq;

namespace AvantiPoint.Packages.Protocol.Tests.Infrastructure;

/// <summary>
/// Runs dotnet/nuget CLI commands against a test feed (pack, push, search, install).
/// </summary>
internal static class NativeToolchainTestHelper
{
    public const string HelloWorldPackageId = "AvantiPoint.Packages.HelloWorld.TestPackage";
    public const string DefaultApiKey = "test-api-key-12345";

    public static string CreateWorkingDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ap-native-toolchain", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    public static string WriteNuGetConfig(Uri feedIndexUrl, string directory)
    {
        var configPath = Path.Combine(directory, "NuGet.config");
        var document = new XDocument(
            new XElement("configuration",
                new XElement("packageSources",
                    new XElement("clear"),
                    new XElement("add",
                        new XAttribute("key", "test-feed"),
                        new XAttribute("value", feedIndexUrl.ToString()),
                        new XAttribute("allowInsecureConnections", "true"))),
                new XElement("config",
                    new XElement("add",
                        new XAttribute("key", "allowInsecureConnections"),
                        new XAttribute("value", "true")))));

        File.WriteAllText(configPath, document.ToString(), Encoding.UTF8);
        return configPath;
    }

    public static IReadOnlyDictionary<string, string?> CreateNuGetEnvironment(
        string nuGetConfigPath,
        string? httpCachePath = null)
    {
        return NativeToolchainRuntime.MergeEnvironment(new Dictionary<string, string?>
        {
            ["NUGET_HTTP_CACHE_PATH"] = httpCachePath
                ?? Path.Combine(Path.GetTempPath(), "ap-nuget-http-cache", Guid.NewGuid().ToString("N")),
            ["NUGET_PLUGINS_CACHE_PATH"] = Path.Combine(Path.GetTempPath(), "ap-nuget-plugins-cache", Guid.NewGuid().ToString("N")),
            ["NUGET_PACKAGES"] = Path.Combine(Path.GetTempPath(), "ap-nuget-packages", Guid.NewGuid().ToString("N")),
            ["NUGET_ALLOW_INSECURE_CONNECTIONS"] = "true",
            ["NUGET_SIGNATURE_VERIFICATION_MODE"] = "Accept",
            ["DOTNET_NOLOGO"] = "true",
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "true",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "true",
            ["NUGET_CONFIGFILE"] = nuGetConfigPath,
        });
    }

    public static string PackHelloWorldPackage(string outputDirectory, string version)
    {
        var projectPath = RepoPathResolver.HelloWorldTestPackageProjectPath;
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException("Hello world test package project was not found.", projectPath);
        }

        var result = NativeToolchainProcess.Run(
            "dotnet",
            $"pack \"{projectPath}\" -c Release -o \"{outputDirectory}\" /p:Version={version} /p:PackageVersion={version} --nologo",
            workingDirectory: NativeToolchainRuntime.TestAssetsRoot,
            environment: NativeToolchainRuntime.BaseEnvironment,
            timeout: TimeSpan.FromMinutes(2));

        result.EnsureSuccess("dotnet pack");

        var packagePath = Directory
            .EnumerateFiles(outputDirectory, $"{HelloWorldPackageId}.*.nupkg", SearchOption.TopDirectoryOnly)
            .SingleOrDefault(file => !file.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase));

        if (packagePath is null)
        {
            throw new FileNotFoundException(
                $"Expected package {HelloWorldPackageId}.{version}.nupkg in {outputDirectory}. stdout: {result.StandardOutput}");
        }

        if (!Path.GetFileName(packagePath).Contains(version, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Packed version did not match requested version '{version}'. Package: {packagePath}. stdout: {result.StandardOutput}");
        }

        return packagePath;
    }

    public static void PushPackage(Uri feedIndexUrl, string packagePath, string apiKey, string nuGetConfigPath)
    {
        var environment = CreateNuGetEnvironment(nuGetConfigPath);

        var result = NativeToolchainProcess.Run(
            "dotnet",
            $"nuget push \"{packagePath}\" --source \"{feedIndexUrl}\" --api-key \"{apiKey}\" --skip-duplicate --disable-buffering",
            workingDirectory: Path.GetDirectoryName(packagePath),
            environment: environment,
            timeout: TimeSpan.FromMinutes(2));

        if (result.ExitCode != 0
            && !result.StandardOutput.Contains("Your package was pushed", StringComparison.OrdinalIgnoreCase))
        {
            result.EnsureSuccess("dotnet nuget push");
        }
    }

    public static async Task SearchPackageAsync(
        Uri feedIndexUrl,
        string packageId,
        string nuGetConfigPath,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        string? lastOutput = null;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var environment = CreateNuGetEnvironment(
                nuGetConfigPath,
                httpCachePath: Path.Combine(Path.GetTempPath(), "ap-nuget-http-cache", Guid.NewGuid().ToString("N")));

            var result = NativeToolchainProcess.Run(
                "dotnet",
                $"package search \"{packageId}\" --source \"{feedIndexUrl}\" --take 5 --prerelease --configfile \"{nuGetConfigPath}\"",
                workingDirectory: NativeToolchainRuntime.TestAssetsRoot,
                environment: environment,
                timeout: TimeSpan.FromMinutes(2));

            lastOutput = result.StandardOutput;

            result.EnsureSuccess("dotnet package search");

            if (OutputContainsPackageId(result.StandardOutput, packageId))
            {
                return;
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new InvalidOperationException(
            $"dotnet package search did not list package '{packageId}' within 30 seconds.{Environment.NewLine}" +
            $"Last output:{Environment.NewLine}{lastOutput}");
    }

    private static bool OutputContainsPackageId(string output, string packageId)
    {
        if (output.Contains("No results found", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (output.Contains(packageId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // CLI table output wraps long package IDs across multiple rows.
        static string Compact(string value) =>
            string.Concat(value.Where(c => !char.IsWhiteSpace(c) && c is not '|' and not '-'));

        var compactOutput = Compact(output);
        var compactPackageId = Compact(packageId);

        if (compactOutput.Contains(compactPackageId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var segments = packageId.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0
            && compactOutput.Contains(segments[0], StringComparison.OrdinalIgnoreCase)
            && compactOutput.Contains(segments[^1], StringComparison.OrdinalIgnoreCase);
    }

    public static (string WorkDirectory, string GlobalPackagesPath) RestorePackageToProject(
        Uri feedIndexUrl,
        string packageId,
        string version,
        string nuGetConfigPath)
    {
        var workDir = CreateWorkingDirectory();
        File.Copy(nuGetConfigPath, Path.Combine(workDir, "NuGet.config"), overwrite: true);
        File.Copy(
            Path.Combine(NativeToolchainRuntime.TestAssetsRoot, "global.json"),
            Path.Combine(workDir, "global.json"),
            overwrite: true);

        var environment = CreateNuGetEnvironment(Path.Combine(workDir, "NuGet.config"));
        var globalPackagesPath = environment["NUGET_PACKAGES"]!;

        try
        {
            var createResult = NativeToolchainProcess.Run(
                "dotnet",
                "new classlib -n Consumer -f net10.0 --force",
                workingDirectory: workDir,
                environment: NativeToolchainRuntime.MergeEnvironment(environment),
                timeout: TimeSpan.FromMinutes(2));

            createResult.EnsureSuccess("dotnet new classlib");

            var deadline = DateTime.UtcNow.AddSeconds(30);
            NativeToolchainResult? lastAddResult = null;

            while (DateTime.UtcNow < deadline)
            {
                environment = CreateNuGetEnvironment(
                    Path.Combine(workDir, "NuGet.config"),
                    httpCachePath: Path.Combine(Path.GetTempPath(), "ap-nuget-http-cache", Guid.NewGuid().ToString("N")));

                lastAddResult = NativeToolchainProcess.Run(
                    "dotnet",
                    $"add package \"{packageId}@{version}\" --source \"{feedIndexUrl}\"",
                    workingDirectory: Path.Combine(workDir, "Consumer"),
                    environment: NativeToolchainRuntime.MergeEnvironment(environment),
                    timeout: TimeSpan.FromMinutes(2));

                if (lastAddResult.ExitCode == 0)
                {
                    break;
                }

                Thread.Sleep(250);
            }

            lastAddResult?.EnsureSuccess("dotnet add package");

            var restoreResult = NativeToolchainProcess.Run(
                "dotnet",
                "restore",
                workingDirectory: Path.Combine(workDir, "Consumer"),
                environment: NativeToolchainRuntime.MergeEnvironment(environment),
                timeout: TimeSpan.FromMinutes(2));

            restoreResult.EnsureSuccess("dotnet restore");

            return (workDir, globalPackagesPath);
        }
        catch
        {
            DeleteDirectory(workDir);
            throw;
        }
    }
}
