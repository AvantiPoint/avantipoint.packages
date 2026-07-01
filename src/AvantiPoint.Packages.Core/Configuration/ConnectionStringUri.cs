using System;
using System.Collections.Generic;
using System.Globalization;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Parses a URI-style storage connection string (for example
/// <c>ftp://user:pass@host:21/path?passive=true</c> or
/// <c>s3://key:secret@bucket?region=us-east-1</c>) into its components.
/// Storage providers use this to accept a single connection string (often supplied by a
/// hosting platform such as .NET Aspire) in place of individually configured fields.
/// </summary>
public sealed class ConnectionStringUri
{
    private ConnectionStringUri(
        string scheme,
        string host,
        int port,
        string userName,
        string password,
        string path,
        IReadOnlyDictionary<string, string> parameters)
    {
        Scheme = scheme;
        Host = host;
        Port = port;
        UserName = userName;
        Password = password;
        Path = path;
        Parameters = parameters;
    }

    /// <summary>The URI scheme (for example <c>ftp</c>, <c>sftp</c>, <c>s3</c>, <c>gs</c>), lowercased.</summary>
    public string Scheme { get; }

    /// <summary>The host component (for S3/GCS this is typically the bucket name).</summary>
    public string Host { get; }

    /// <summary>The port, or <c>-1</c> when not specified.</summary>
    public int Port { get; }

    /// <summary>The user name from the user-info component, or <c>null</c>.</summary>
    public string UserName { get; }

    /// <summary>The password from the user-info component, or <c>null</c>.</summary>
    public string Password { get; }

    /// <summary>The path component with the leading slash removed, or <c>null</c> when empty.</summary>
    public string Path { get; }

    /// <summary>Case-insensitive query-string parameters.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    public string GetString(string key) =>
        Parameters.TryGetValue(key, out var value) ? value : null;

    public bool? GetBool(string key) =>
        Parameters.TryGetValue(key, out var value) && bool.TryParse(value, out var result)
            ? result
            : null;

    public int? GetInt(string key) =>
        Parameters.TryGetValue(key, out var value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    /// <summary>
    /// Attempts to parse the supplied value as a URI-style connection string. Returns <c>false</c>
    /// when the value is not an absolute URI (for example a provider-native connection string such
    /// as an Azure Storage connection string).
    /// </summary>
    public static bool TryParse(string value, out ConnectionStringUri result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        string userName = null;
        string password = null;
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var separator = uri.UserInfo.IndexOf(':');
            if (separator >= 0)
            {
                userName = Uri.UnescapeDataString(uri.UserInfo.Substring(0, separator));
                password = Uri.UnescapeDataString(uri.UserInfo.Substring(separator + 1));
            }
            else
            {
                userName = Uri.UnescapeDataString(uri.UserInfo);
            }
        }

        var path = uri.AbsolutePath?.TrimStart('/');
        if (string.IsNullOrEmpty(path))
        {
            path = null;
        }
        else
        {
            path = Uri.UnescapeDataString(path);
        }

        result = new ConnectionStringUri(
            uri.Scheme?.ToLowerInvariant(),
            uri.Host,
            uri.Port,
            userName,
            password,
            path,
            ParseQuery(uri.Query));

        return true;
    }

    private static IReadOnlyDictionary<string, string> ParseQuery(string query)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(query))
        {
            return parameters;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator < 0)
            {
                parameters[Uri.UnescapeDataString(pair)] = string.Empty;
                continue;
            }

            var key = Uri.UnescapeDataString(pair.Substring(0, separator));
            var value = Uri.UnescapeDataString(pair.Substring(separator + 1));
            parameters[key] = value;
        }

        return parameters;
    }
}
