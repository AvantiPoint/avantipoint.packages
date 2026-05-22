using System.Diagnostics;
using System.Text;

namespace AvantiPoint.Packages.Protocol.Tests.Infrastructure;

internal sealed record NativeToolchainResult(int ExitCode, string StandardOutput, string StandardError)
{
    public void EnsureSuccess(string operation)
    {
        if (ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{operation} failed with exit code {ExitCode}.{Environment.NewLine}" +
            $"stdout:{Environment.NewLine}{StandardOutput}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{StandardError}");
    }
}

internal static class NativeToolchainProcess
{
    public static NativeToolchainResult Run(
        string fileName,
        string arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string?>? environment,
        TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                if (value is null)
                {
                    startInfo.Environment.Remove(key);
                }
                else
                {
                    startInfo.Environment[key] = value;
                }
            }
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName} {arguments}");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort
            }

            throw new TimeoutException($"Process timed out after {timeout.TotalSeconds:F0}s: {fileName} {arguments}");
        }

        process.WaitForExit();
        return new NativeToolchainResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
