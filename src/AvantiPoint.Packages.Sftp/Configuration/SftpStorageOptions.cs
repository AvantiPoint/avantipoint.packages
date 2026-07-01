using System;
using System.ComponentModel.DataAnnotations;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Sftp;

public class SftpStorageOptions : IConnectionStringOptions
{
    /// <summary>
    /// A URI-style connection string, for example
    /// <c>sftp://user:pass@host:22/packages?privateKeyPath=/keys/id_rsa</c>.
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

    public int Port { get; set; } = 22;

    [Required]
    public string Username { get; set; } = string.Empty;

    public string? Password { get; set; }

    public string? PrivateKeyPath { get; set; }

    public string? PrivateKeyPassphrase { get; set; }

    public string RemotePath { get; set; } = "/";

    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public int MaxConnections { get; set; } = 4;

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
                "The SFTP storage connection string must be a URI, for example 'sftp://user:pass@host:22/path'.");
        }

        if (!string.IsNullOrEmpty(uri.Host)) Host = uri.Host;
        if (uri.Port > 0) Port = uri.Port;
        if (!string.IsNullOrEmpty(uri.UserName)) Username = uri.UserName;
        if (!string.IsNullOrEmpty(uri.Password)) Password = uri.Password;
        if (!string.IsNullOrEmpty(uri.Path)) RemotePath = "/" + uri.Path;

        if (uri.GetString("privateKeyPath") is { Length: > 0 } privateKeyPath) PrivateKeyPath = privateKeyPath;
        if (uri.GetString("privateKeyPassphrase") is { Length: > 0 } passphrase) PrivateKeyPassphrase = passphrase;
        if (uri.GetInt("maxConnections") is { } maxConnections) MaxConnections = maxConnections;
        if (uri.GetInt("connectionTimeout") is { } connectionTimeout) ConnectionTimeout = TimeSpan.FromSeconds(connectionTimeout);
        if (uri.GetInt("operationTimeout") is { } operationTimeout) OperationTimeout = TimeSpan.FromSeconds(operationTimeout);
    }
}
