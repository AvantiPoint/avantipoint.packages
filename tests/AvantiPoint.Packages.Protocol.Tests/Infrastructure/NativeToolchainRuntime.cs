using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AvantiPoint.Packages.Protocol.Tests.Infrastructure;

/// <summary>
/// Resolves the .NET SDK root used by native toolchain subprocesses so pack/push/restore
/// use the same SDK band as the test project (avoids preview SDK workload conflicts).
/// </summary>
internal static class NativeToolchainRuntime
{
    private static readonly Lazy<string?> DotNetRoot = new(ResolveDotNetRoot);
    private static readonly Lazy<string> TestAssetsDirectory = new(GetTestAssetsDirectory);

    public static string TestAssetsRoot => TestAssetsDirectory.Value;

    public static IReadOnlyDictionary<string, string?> BaseEnvironment =>
        MergeEnvironment(null);

    public static IReadOnlyDictionary<string, string?> MergeEnvironment(
        IReadOnlyDictionary<string, string?>? additional)
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (DotNetRoot.Value is not null)
        {
            environment["DOTNET_ROOT"] = DotNetRoot.Value;
            environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
        }

        if (additional is not null)
        {
            foreach (var (key, value) in additional)
            {
                environment[key] = value;
            }
        }

        return environment;
    }

    private static string GetTestAssetsDirectory()
    {
        return Path.Combine(RepoPathResolver.RepositoryRoot, "tests", "TestAssets");
    }

    private static string? ResolveDotNetRoot()
    {
        if (TryGetDotNetRootFromProcessPath(out var fromProcess))
        {
            return fromProcess;
        }

        try
        {
            var result = NativeToolchainProcess.Run(
                "dotnet",
                "--info",
                workingDirectory: TestAssetsDirectory.Value,
                environment: null,
                timeout: TimeSpan.FromSeconds(30));

            if (result.ExitCode != 0)
            {
                return null;
            }

            var match = Regex.Match(
                result.StandardOutput,
                @"Base Path:\s*(.+)",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);

            if (!match.Success)
            {
                return null;
            }

            var sdkPath = match.Groups[1].Value.Trim();
            // SDK path: {dotnetRoot}/sdk/{version}
            var sdkDirectory = Path.GetDirectoryName(sdkPath);
            return sdkDirectory is null ? null : Path.GetDirectoryName(sdkDirectory);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetDotNetRootFromProcessPath(out string? dotNetRoot)
    {
        dotNetRoot = null;
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(processPath);
        if (directory is null)
        {
            return false;
        }

        if (Path.GetFileName(directory).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            dotNetRoot = Path.GetDirectoryName(directory);
            return dotNetRoot is not null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && processPath.Contains(@"\dotnet\", StringComparison.OrdinalIgnoreCase))
        {
            var index = processPath.IndexOf(@"\dotnet\", StringComparison.OrdinalIgnoreCase);
            dotNetRoot = processPath[..index];
            return true;
        }

        if (processPath.Contains("/dotnet/", StringComparison.Ordinal))
        {
            var index = processPath.IndexOf("/dotnet/", StringComparison.Ordinal);
            dotNetRoot = processPath[..index];
            return true;
        }

        return false;
    }
}
