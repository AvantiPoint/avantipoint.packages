namespace AvantiPoint.Packages.Search.Tests.TestInfrastructure;

internal static class DockerAvailability
{
    private static readonly Lazy<bool> IsAvailableLazy = new(CheckDocker);

    public static bool IsAvailable => IsAvailableLazy.Value;

    public static string? SkipReason { get; } = IsAvailable ? null : "Docker is not available on this system.";

    private static bool CheckDocker()
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "info",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            return process.Start() && process.WaitForExit(15_000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
