using System.Diagnostics;

namespace AvantiPoint.Packages.Protocol.Tests.Infrastructure;

/// <summary>
/// Verifies that the .NET SDK CLI is available for native toolchain integration tests.
/// </summary>
internal static class NativeToolchainAvailability
{
    private static readonly Lazy<bool> IsAvailable = new(CheckAvailability);
    private static readonly Lazy<string?> SkipReason = new(GetSkipReason);

    public static bool SdkIsAvailable => IsAvailable.Value;

    public static string? SdkSkipReason => SkipReason.Value;

    private static bool CheckAvailability()
    {
        try
        {
            var result = NativeToolchainProcess.Run(
                "dotnet",
                "--version",
                workingDirectory: NativeToolchainRuntime.TestAssetsRoot,
                environment: NativeToolchainRuntime.BaseEnvironment,
                timeout: TimeSpan.FromSeconds(30));

            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetSkipReason()
    {
        if (SdkIsAvailable)
        {
            return null;
        }

        return "The .NET SDK (dotnet CLI) is not available. Native toolchain integration tests require dotnet pack, dotnet nuget push, dotnet package search, and dotnet add package.";
    }
}
