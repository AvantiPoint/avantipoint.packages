using System;
using System.ComponentModel.DataAnnotations;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Ftp;

public class FtpStorageOptions : IConnectionStringOptions
{
    /// <summary>
    /// A URI-style connection string, for example
    /// <c>ftp://user:pass@host:21/packages?passive=true</c> (use <c>ftps://</c> to enable SSL).
    /// When supplied, its components populate the fields below.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The name of a connection string under the root <c>ConnectionStrings</c> section to use for
    /// <see cref="ConnectionString"/> (for example <c>ConnectionStrings__Storage</c>).
    /// </summary>
    public string? ConnectionStringName { get; set; }

    [Required]
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 21;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public bool UseSsl { get; set; }

    public string RemotePath { get; set; } = "/";

    public bool UsePassiveMode { get; set; } = true;

    /// <summary>
    /// When set, overrides the address used for FTP passive data connections (useful behind Docker/NAT).
    /// </summary>
    public string? PassiveAddress { get; set; }

    public int DataConnectionPortMin { get; set; }

    public int DataConnectionPortMax { get; set; }

    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Populates the individual fields from <see cref="ConnectionString"/> when it is a URI-style
    /// value. Fields not present in the connection string are left unchanged. Called as a
    /// post-configure step after any named connection string has been resolved.
    /// </summary>
    public void ApplyConnectionString()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        if (!ConnectionStringUri.TryParse(ConnectionString, out var uri))
        {
            throw new InvalidOperationException(
                "The FTP storage connection string must be a URI, for example 'ftp://user:pass@host:21/path'.");
        }

        if (string.Equals(uri.Scheme, "ftps", StringComparison.OrdinalIgnoreCase))
        {
            UseSsl = true;
        }

        if (!string.IsNullOrEmpty(uri.Host)) Host = uri.Host;
        if (uri.Port > 0) Port = uri.Port;
        if (!string.IsNullOrEmpty(uri.UserName)) Username = uri.UserName;
        if (!string.IsNullOrEmpty(uri.Password)) Password = uri.Password;
        if (!string.IsNullOrEmpty(uri.Path)) RemotePath = "/" + uri.Path;

        if (uri.GetBool("ssl") is { } ssl) UseSsl = ssl;
        if (uri.GetBool("passive") is { } passive) UsePassiveMode = passive;
        if (uri.GetString("passiveAddress") is { Length: > 0 } passiveAddress) PassiveAddress = passiveAddress;
        if (uri.GetInt("dataConnectionPortMin") is { } portMin) DataConnectionPortMin = portMin;
        if (uri.GetInt("dataConnectionPortMax") is { } portMax) DataConnectionPortMax = portMax;
        if (uri.GetInt("connectTimeout") is { } connectTimeout) ConnectTimeout = TimeSpan.FromSeconds(connectTimeout);
        if (uri.GetInt("readTimeout") is { } readTimeout) ReadTimeout = TimeSpan.FromSeconds(readTimeout);
    }
}
