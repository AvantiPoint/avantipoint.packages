using System.Text;
using System.Text.RegularExpressions;
using AvantiPoint.Packages.Registry.Tests.Shared;

namespace AvantiPoint.Packages.Registry.Oci.Tests.Infrastructure;

internal static class HelmNativeToolchainTestHelper
{
    public const string HelmOciSegment = "helm";

    public static string CreateWorkingDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ap-helm-native", Guid.NewGuid().ToString("N"));
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

    public static string PrepareChartDirectory(string chartName, string version)
    {
        var workDir = CreateWorkingDirectory();
        var sourceDir = RepoPathResolver.HelloWorldHelmChartDirectory;
        CopyDirectory(sourceDir, workDir);

        var chartYamlPath = Path.Combine(workDir, "Chart.yaml");
        var chartYaml = File.ReadAllText(chartYamlPath);
        chartYaml = Regex.Replace(chartYaml, @"^name:\s*.+$", $"name: {chartName}", RegexOptions.Multiline);
        chartYaml = Regex.Replace(chartYaml, @"^version:\s*.+$", $"version: {version}", RegexOptions.Multiline);
        File.WriteAllText(chartYamlPath, chartYaml, Encoding.UTF8);

        return workDir;
    }

    public static string PackageChart(string chartDirectory)
    {
        var result = CliProcessRunner.Run(
            "helm",
            "package .",
            workingDirectory: chartDirectory,
            timeout: TimeSpan.FromMinutes(2));

        result.EnsureSuccess("helm package");

        var package = Directory.GetFiles(chartDirectory, "*.tgz").SingleOrDefault()
            ?? throw new InvalidOperationException("helm package did not produce a .tgz artifact.");

        return package;
    }

    public static void RegistryLogin(string registryHost, string apiKey)
    {
        var result = CliProcessRunner.Run(
            "helm",
            $"registry login \"{registryHost}\" -u user -p \"{apiKey}\" --plain-http",
            timeout: TimeSpan.FromMinutes(2));

        if (IsInsecureRegistryError(result))
        {
            throw new InvalidOperationException(
                "helm registry login failed because plain HTTP registries are not permitted. " +
                "Use Helm 3.13+ with --plain-http, or run on Linux CI against an HTTPS registry." +
                $"{Environment.NewLine}{result.StandardError}{result.StandardOutput}");
        }

        result.EnsureSuccess("helm registry login");
    }

    public static void PushChart(string chartPackagePath, string registryHost, string ociSegment)
    {
        var ociReference = $"oci://{registryHost}/{ociSegment}";
        var result = CliProcessRunner.Run(
            "helm",
            $"push \"{chartPackagePath}\" \"{ociReference}\" --plain-http",
            timeout: TimeSpan.FromMinutes(5));

        if (IsInsecureRegistryError(result))
        {
            throw new InvalidOperationException(
                "helm push failed because plain HTTP registries are not permitted. " +
                "Use Helm 3.13+ with --plain-http, or run on Linux CI against an HTTPS registry." +
                $"{Environment.NewLine}{result.StandardError}{result.StandardOutput}");
        }

        result.EnsureSuccess("helm push");
    }

    private static bool IsInsecureRegistryError(CliProcessResult result)
    {
        var text = result.StandardOutput + result.StandardError;
        return text.Contains("server gave HTTP response to HTTPS client", StringComparison.OrdinalIgnoreCase)
            || text.Contains("plain-http", StringComparison.OrdinalIgnoreCase)
            || text.Contains("tls: first record does not look like a tls handshake", StringComparison.OrdinalIgnoreCase)
            || text.Contains("http: server gave HTTP response", StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(sourceDir, destinationDir, StringComparison.Ordinal));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var target = file.Replace(sourceDir, destinationDir, StringComparison.Ordinal);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
