using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AvantiPoint.Packages.Integration.Tests.TestInfrastructure;

/// <summary>
/// Utility to check if Docker is available on the system.
/// Performs a one-time check and caches the result.
/// </summary>
public static class DockerAvailability
{
    private static readonly Lazy<bool> _isAvailable = new(CheckDockerAvailability);
    private static readonly Lazy<string?> _skipReason = new(GetSkipReason);

    /// <summary>
    /// Gets whether Docker is available on the system.
    /// </summary>
    public static bool IsAvailable => _isAvailable.Value;

    /// <summary>
    /// Gets the skip reason if Docker is not available, or null if it is available.
    /// </summary>
    public static string? SkipReason => _skipReason.Value;

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

            process.WaitForExit(5000); // Wait up to 5 seconds

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

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Docker is not available on this Windows system. Please install Docker Desktop.";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Docker is not available on this Linux system. Please install Docker.";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "Docker is not available on this macOS system. Please install Docker Desktop.";
        }

        return "Docker is not available on this system.";
    }
}

