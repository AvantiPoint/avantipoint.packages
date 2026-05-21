namespace AvantiPoint.Packages.Registry.Tests.Shared;

public static class ToolAvailability
{
    private static readonly Lazy<bool> NpmIsAvailable = new(CheckNpm);
    private static readonly Lazy<string?> NpmSkipReason = new(GetNpmSkipReason);
    private static readonly Lazy<bool> DockerIsAvailable = new(CheckDocker);
    private static readonly Lazy<string?> DockerSkipReason = new(GetDockerSkipReason);

    public static bool IsNpmAvailable => NpmIsAvailable.Value;

    public static string? NpmSkipReasonValue => NpmSkipReason.Value;

    public static bool IsDockerAvailable => DockerIsAvailable.Value;

    public static string? DockerSkipReasonValue => DockerSkipReason.Value;

    private static bool CheckNpm()
    {
        try
        {
            var result = CliProcessRunner.Run("npm", "--version", timeout: TimeSpan.FromSeconds(30));
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetNpmSkipReason() =>
        IsNpmAvailable ? null : "The npm CLI is not available. Native npm integration tests require npm publish, npm view, and npm pack.";

    private static bool CheckDocker()
    {
        try
        {
            var result = CliProcessRunner.Run("docker", "version", timeout: TimeSpan.FromSeconds(30));
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetDockerSkipReason() =>
        IsDockerAvailable ? null : "Docker is not available. Native OCI integration tests require docker build, push, and pull.";
}
