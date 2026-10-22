namespace AvantiPoint.Packages.UI.Services;

public static class NuGetCommandBuilder
{
    public static string BuildAddSource(
        string feedUrl,
        string feedName,
        string? username,
        string tokenPlaceholder = "<token>")
    {
        var resolvedUsername = string.IsNullOrWhiteSpace(username) ? "<email>" : username;
        var insecure = Uri.TryCreate(feedUrl, UriKind.Absolute, out var url) && url.Scheme == "http"
            ? " --allow-insecure-connections" : string.Empty;
        return $"dotnet nuget add source {feedUrl} --name \"{feedName}\" --username \"{resolvedUsername}\" --password {tokenPlaceholder} --store-password-in-clear-text{insecure}";
    }
}
