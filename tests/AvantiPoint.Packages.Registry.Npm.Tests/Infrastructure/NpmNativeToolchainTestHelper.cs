using System.Text;
using System.Text.Json.Nodes;
using AvantiPoint.Packages.Registry.Tests.Shared;

namespace AvantiPoint.Packages.Registry.Npm.Tests.Infrastructure;

internal static class NpmNativeToolchainTestHelper
{
    public const string HelloWorldPackageName = "avantipoint-hello-world-npm-test-package";

    public static string CreateWorkingDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ap-npm-native", Guid.NewGuid().ToString("N"));
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
        }
    }

    public static string PreparePublishDirectory(string version)
    {
        var workDir = CreateWorkingDirectory();
        var sourceDir = RepoPathResolver.HelloWorldNpmPackageDirectory;
        CopyDirectory(sourceDir, workDir);

        var packageJsonPath = Path.Combine(workDir, "package.json");
        var packageJson = JsonNode.Parse(File.ReadAllText(packageJsonPath))!.AsObject();
        packageJson["version"] = version;
        File.WriteAllText(packageJsonPath, packageJson.ToJsonString());

        return workDir;
    }

    public static string WriteNpmrc(Uri registryUrl, string apiKey, string directory)
    {
        var registry = registryUrl.ToString();
        if (!registry.EndsWith('/'))
        {
            registry += "/";
        }

        var registryHost = new Uri(registry).Authority;
        var npmrc = new StringBuilder();
        npmrc.AppendLine($"registry={registry}");
        npmrc.AppendLine($"//{registryHost}/npm/:_authToken={apiKey}");

        var npmrcPath = Path.Combine(directory, ".npmrc");
        File.WriteAllText(npmrcPath, npmrc.ToString(), Encoding.UTF8);
        return npmrcPath;
    }

    public static void PublishPackage(string publishDirectory, string apiKey)
    {
        var environment = new Dictionary<string, string?>
        {
            ["NPM_CONFIG_USERCONFIG"] = Path.Combine(publishDirectory, ".npmrc"),
            ["npm_config_userconfig"] = Path.Combine(publishDirectory, ".npmrc"),
            ["NPM_CONFIG_FUND"] = "false",
            ["NPM_CONFIG_AUDIT"] = "false",
        };

        var result = CliProcessRunner.Run(
            "npm",
            "publish --access public --tag ci",
            workingDirectory: publishDirectory,
            environment: environment);

        result.EnsureSuccess("npm publish");
    }

    public static async Task ViewPackageAsync(
        Uri registryUrl,
        string packageName,
        string version,
        string publishDirectory,
        CancellationToken cancellationToken)
    {
        var environment = new Dictionary<string, string?>
        {
            ["NPM_CONFIG_USERCONFIG"] = Path.Combine(publishDirectory, ".npmrc"),
            ["npm_config_userconfig"] = Path.Combine(publishDirectory, ".npmrc"),
        };

        var deadline = DateTime.UtcNow.AddSeconds(30);
        string? lastOutput = null;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = CliProcessRunner.Run(
                "npm",
                $"view {packageName}@{version} version --registry \"{registryUrl}\"",
                workingDirectory: publishDirectory,
                environment: environment);

            lastOutput = result.StandardOutput + result.StandardError;

            if (result.ExitCode == 0
                && result.StandardOutput.Trim().Contains(version, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new InvalidOperationException(
            $"npm view did not list {packageName}@{version} within 30 seconds.{Environment.NewLine}{lastOutput}");
    }

    public static string PackPackage(Uri registryUrl, string packageName, string version, string outputDirectory, string publishDirectory)
    {
        var environment = new Dictionary<string, string?>
        {
            ["NPM_CONFIG_USERCONFIG"] = Path.Combine(publishDirectory, ".npmrc"),
            ["npm_config_userconfig"] = Path.Combine(publishDirectory, ".npmrc"),
        };

        var result = CliProcessRunner.Run(
            "npm",
            $"pack {packageName}@{version} --pack-destination \"{outputDirectory}\" --registry \"{registryUrl}\"",
            workingDirectory: outputDirectory,
            environment: environment);

        result.EnsureSuccess("npm pack");

        var tarball = Directory
            .EnumerateFiles(outputDirectory, "*.tgz", SearchOption.TopDirectoryOnly)
            .SingleOrDefault();

        if (tarball is null)
        {
            throw new FileNotFoundException(
                $"npm pack did not produce a .tgz in {outputDirectory}. stdout: {result.StandardOutput}");
        }

        return tarball;
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
