using System.Diagnostics;
using System.Text;

namespace AvantiPoint.Packages.Registry.Tests.Shared;

public sealed record CliProcessResult(int ExitCode, string StandardOutput, string StandardError)
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

public static class CliProcessRunner
{
    public static CliProcessResult Run(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        TimeSpan? timeout = null,
        string? stdin = null)
    {
        timeout ??= TimeSpan.FromMinutes(2);

        fileName = ResolveExecutable(fileName);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null,
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

        if (stdin is not null)
        {
            process.StandardInput.Write(stdin);
            process.StandardInput.Close();
        }

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

        if (!process.WaitForExit((int)timeout.Value.TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            throw new TimeoutException($"Process timed out after {timeout.Value.TotalSeconds:F0}s: {fileName} {arguments}");
        }

        process.WaitForExit();
        return new CliProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static string ResolveExecutable(string fileName)
    {
        if (Path.IsPathRooted(fileName) || fileName.Contains(Path.DirectorySeparatorChar))
        {
            return fileName;
        }

        if (!OperatingSystem.IsWindows() || Path.HasExtension(fileName))
        {
            return fileName;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return fileName;
        }

        foreach (var directory in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in new[] { ".exe", ".cmd", ".bat" })
            {
                var candidate = Path.Combine(directory.Trim(), fileName + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return fileName;
    }
}
