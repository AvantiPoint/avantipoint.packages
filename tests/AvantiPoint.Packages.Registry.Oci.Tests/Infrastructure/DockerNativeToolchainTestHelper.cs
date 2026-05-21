using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AvantiPoint.Packages.Registry.Tests.Shared;

namespace AvantiPoint.Packages.Registry.Oci.Tests.Infrastructure;

internal static class DockerNativeToolchainTestHelper
{
    public static string CreateWorkingDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ap-docker-native", Guid.NewGuid().ToString("N"));
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

    public static string ConfigureDockerConfig(string workDir, string registryHost, string apiKey)
    {
        var dockerDir = Path.Combine(workDir, ".docker");
        Directory.CreateDirectory(dockerDir);

        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"user:{apiKey}"));
        var loginHost = registryHost.Contains("://", StringComparison.Ordinal)
            ? registryHost
            : $"http://{registryHost}";
        var config = $$"""
                       {
                         "auths": {
                           "{{registryHost}}": {
                             "auth": "{{auth}}"
                           },
                           "{{loginHost}}": {
                             "auth": "{{auth}}"
                           }
                         }
                       }
                       """;

        File.WriteAllText(Path.Combine(dockerDir, "config.json"), config);
        return dockerDir;
    }

    public static string BuildImage(string contextDirectory, string imageTag, string dockerConfigDir)
    {
        var environment = new Dictionary<string, string?>
        {
            ["DOCKER_CONFIG"] = dockerConfigDir,
        };

        var result = CliProcessRunner.Run(
            "docker",
            $"build -t \"{imageTag}\" \"{contextDirectory}\"",
            environment: environment,
            timeout: TimeSpan.FromMinutes(5));

        result.EnsureSuccess("docker build");
        return imageTag;
    }

    public static void Login(string registryHost, string apiKey, string dockerConfigDir)
    {
        var environment = new Dictionary<string, string?>
        {
            ["DOCKER_CONFIG"] = dockerConfigDir,
        };

        var loginTarget = registryHost.Contains("://", StringComparison.Ordinal)
            ? registryHost
            : $"http://{registryHost}";

        var result = CliProcessRunner.Run(
            "docker",
            $"login \"{loginTarget}\" -u user --password-stdin",
            environment: environment,
            stdin: apiKey);

        if (result.ExitCode != 0)
        {
            if (IsInsecureRegistryError(result))
            {
                throw new InvalidOperationException(
                    "docker login failed because the daemon does not allow insecure (HTTP) registries. " +
                    "Add the test registry host to Docker's insecure-registries configuration, or run on Linux CI where loopback HTTP registries are often permitted." +
                    $"{Environment.NewLine}{result.StandardError}{result.StandardOutput}");
            }

            result.EnsureSuccess("docker login");
        }
    }

    public static void PushImage(string imageTag, string dockerConfigDir)
    {
        var environment = new Dictionary<string, string?>
        {
            ["DOCKER_CONFIG"] = dockerConfigDir,
            ["DOCKER_CONTENT_TRUST"] = "0",
        };

        var result = CliProcessRunner.Run(
            "docker",
            $"push \"{imageTag}\"",
            environment: environment,
            timeout: TimeSpan.FromMinutes(5));

        if (IsInsecureRegistryError(result))
        {
            throw new InvalidOperationException(
                "docker push failed because the daemon does not allow insecure (HTTP) registries. " +
                "Add the test registry host to Docker's insecure-registries configuration, or run on Linux CI where loopback HTTP registries are often permitted." +
                $"{Environment.NewLine}{result.StandardError}{result.StandardOutput}");
        }

        result.EnsureSuccess("docker push");
    }

    public static void PullImage(string imageTag, string dockerConfigDir)
    {
        var environment = new Dictionary<string, string?>
        {
            ["DOCKER_CONFIG"] = dockerConfigDir,
            ["DOCKER_CONTENT_TRUST"] = "0",
        };

        var result = CliProcessRunner.Run(
            "docker",
            $"pull \"{imageTag}\"",
            environment: environment,
            timeout: TimeSpan.FromMinutes(5));

        if (IsInsecureRegistryError(result))
        {
            throw new InvalidOperationException(
                "docker pull failed because the daemon does not allow insecure (HTTP) registries." +
                $"{Environment.NewLine}{result.StandardError}{result.StandardOutput}");
        }

        result.EnsureSuccess("docker pull");
    }

    public static async Task AssertTagsListContainsAsync(
        HttpClient client,
        string repository,
        string tag,
        CancellationToken cancellationToken,
        string? ociSegment = null)
    {
        var apiPrefix = string.IsNullOrEmpty(ociSegment) ? "/v2" : $"/{ociSegment}/v2";
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync($"{apiPrefix}/{repository}/tags/list", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var tags = json.RootElement.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToList();
                if (tags.Contains(tag))
                {
                    return;
                }
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new InvalidOperationException($"Tag '{tag}' was not listed for repository '{repository}' within 30 seconds.");
    }

    private static bool IsInsecureRegistryError(CliProcessResult result)
    {
        var text = result.StandardOutput + result.StandardError;
        return text.Contains("server gave HTTP response to HTTPS client", StringComparison.OrdinalIgnoreCase)
            || text.Contains("insecure-registries", StringComparison.OrdinalIgnoreCase)
            || text.Contains("http: server gave HTTP response", StringComparison.OrdinalIgnoreCase);
    }
}
