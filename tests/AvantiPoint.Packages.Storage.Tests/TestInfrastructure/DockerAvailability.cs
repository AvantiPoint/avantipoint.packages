using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AvantiPoint.Packages.Storage.Tests.TestInfrastructure;

public static class DockerAvailability
{
    private static readonly Lazy<bool> IsAvailableLazy = new(CheckDockerAvailability);
    private static readonly Lazy<string?> SkipReasonLazy = new(GetSkipReason);

    public static bool IsAvailable => IsAvailableLazy.Value;

    public static string? SkipReason => SkipReasonLazy.Value;

    private static bool CheckDockerAvailability()
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "version --format {{.Server.Version}}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                return false;
            }

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetSkipReason()
    {
        if (IsAvailable)
        {
            return null;
        }

        return "Docker is not available on this system.";
    }
}
