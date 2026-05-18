using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Sftp;

public class SftpStorageOptions
{
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
}
