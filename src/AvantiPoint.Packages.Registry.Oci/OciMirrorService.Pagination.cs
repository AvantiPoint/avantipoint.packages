using System.Text.Json;
using System.Text.RegularExpressions;

namespace AvantiPoint.Packages.Registry.Oci;

public sealed partial class OciMirrorService
{
    private async Task<IReadOnlyList<string>?> ReadTagPagesAsync(HttpClient client, Uri initialUrl, int maximumTags, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var token = timeout.Token;
        var tags = new List<string>();
        var seenTags = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<Uri>();
        Uri? url = initialUrl;
        var bytesRemaining = MaximumTagListResponseBytes;
        // A registry can paginate below n. Follow only this repository's endpoint,
        // with aggregate size/time/page limits and cycle detection. A Link header is
        // untrusted and must never redirect upstream credentials to another origin.
        while (url is not null && visited.Count < 32 && visited.Add(url) && bytesRemaining > 0)
        {
            using var request = CreateTagListRequest(url.ToString());
            using var response = await SendWithBearerChallengeAsync(client, request,
                () => CreateTagListRequest(url.ToString()), token, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) return tags.Count == 0 ? null : tags;
            if (response.Content.Headers.ContentLength > bytesRemaining) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            await using var bounded = await ReadBoundedAsync(stream, bytesRemaining, token);
            if (bounded is null) return null;
            bytesRemaining -= checked((int)bounded.Length);
            using var document = await JsonDocument.ParseAsync(bounded, cancellationToken: token);
            if (!document.RootElement.TryGetProperty("tags", out var values) || values.ValueKind == JsonValueKind.Null) return tags;
            if (values.ValueKind != JsonValueKind.Array) return null;
            foreach (var value in values.EnumerateArray())
            {
                if (value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 and <= 128 } tag
                    && seenTags.Add(tag)) tags.Add(tag);
                if (tags.Count >= maximumTags) return tags;
            }
            url = NextTagPage(response, url, initialUrl);
        }
        return tags;
    }

    private static Uri? NextTagPage(HttpResponseMessage response, Uri currentUrl, Uri initialUrl)
    {
        if (!response.Headers.TryGetValues("Link", out var links)) return null;
        foreach (var header in links)
        foreach (Match match in LinkPattern().Matches(header))
        {
            var parameters = match.Groups[2].Value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var next = parameters.Any(parameter => parameter.Split('=', 2, StringSplitOptions.TrimEntries) is [var name, var value]
                && name.Equals("rel", StringComparison.OrdinalIgnoreCase)
                && value.Trim('"').Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("next", StringComparer.OrdinalIgnoreCase));
            if (!next || !Uri.TryCreate(currentUrl, match.Groups[1].Value, out var candidate)) continue;
            if (candidate.Scheme == initialUrl.Scheme && candidate.Host == initialUrl.Host && candidate.Port == initialUrl.Port
                && candidate.AbsolutePath == initialUrl.AbsolutePath && candidate.UserInfo.Length == 0 && candidate.Fragment.Length == 0)
                return candidate;
        }
        return null;
    }

    [GeneratedRegex("<([^<>]+)>((?:[^<>,]|\"[^\"]*\")*)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex LinkPattern();
}
