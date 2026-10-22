using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Host.Admin.Services.Upstreams;

/// <summary>
/// Performs a lightweight, protocol-aware reachability check without downloading a catalog.
/// Package-level credentials are still validated naturally when a requested package is resolved.
/// </summary>
public sealed class PackageSourceConnectionTester(
    IHttpClientFactory httpClientFactory,
    ISecretProtector secretProtector) : IPackageSourceConnectionTester
{
    public const string HttpClientName = "PackageSourceConnectionTester";

    public async Task<PackageSourceConnectionResult> TestAsync(
        PackageSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!TryGetProbeUri(source, out var probeUri, out var validationError))
        {
            return new(false, validationError!);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, probeUri);
        ApplyCredentials(request, source);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (source.Protocol == PackageSourceProtocol.Oci
                && response.StatusCode == HttpStatusCode.Unauthorized
                && response.Headers.WwwAuthenticate.Any(header =>
                    string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)))
            {
                return new(
                    true,
                    "Registry is reachable and advertised OCI bearer authentication. Credentials will be validated for the requested repository.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new(
                    false,
                    $"The upstream returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            if (source.Protocol == PackageSourceProtocol.NuGet)
            {
                await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
                if (!document.RootElement.TryGetProperty("resources", out var resources)
                    || resources.ValueKind != JsonValueKind.Array)
                {
                    return new(false, "The URL responded, but it is not a NuGet v3 service index.");
                }

                return new(true, "NuGet v3 service index reached successfully.");
            }

            return source.Protocol switch
            {
                PackageSourceProtocol.Npm => new(true, "npm registry reached successfully."),
                PackageSourceProtocol.Oci => new(true, "OCI registry reached successfully."),
                _ => new(true, "Upstream reached successfully."),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "The connection attempt timed out.");
        }
        catch (HttpRequestException exception)
        {
            return new(false, $"The upstream could not be reached: {exception.Message}");
        }
        catch (JsonException)
        {
            return new(false, "The URL responded, but did not return valid registry metadata.");
        }
    }

    private void ApplyCredentials(HttpRequestMessage request, PackageSource source)
    {
        var username = secretProtector.Unprotect(source.Username);
        var password = secretProtector.Unprotect(source.Password);
        var apiKey = secretProtector.Unprotect(source.ApiKey);

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
            return;
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return;
        }

        if (source.Protocol == PackageSourceProtocol.Npm)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
        else
        {
            request.Headers.TryAddWithoutValidation("X-NuGet-ApiKey", apiKey);
        }
    }

    private static bool TryGetProbeUri(
        PackageSource source,
        out Uri? probeUri,
        out string? validationError)
    {
        probeUri = null;
        validationError = null;

        if (!Uri.TryCreate(source.FeedUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            validationError = "The feed URL must be an absolute HTTP or HTTPS URL.";
            return false;
        }

        if (source.Protocol == PackageSourceProtocol.NuGet)
        {
            probeUri = baseUri;
            return true;
        }

        var normalizedBase = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/");
        probeUri = source.Protocol switch
        {
            PackageSourceProtocol.Npm => new Uri(normalizedBase, "-/ping"),
            PackageSourceProtocol.Oci => new Uri(normalizedBase, "v2/"),
            _ => normalizedBase,
        };
        return true;
    }
}
