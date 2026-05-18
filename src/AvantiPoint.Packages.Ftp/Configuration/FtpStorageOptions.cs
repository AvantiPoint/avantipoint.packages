using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Ftp;

public class FtpStorageOptions
{
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
}
