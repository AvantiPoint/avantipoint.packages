using System.Text;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace AvantiPoint.Packages.Sftp.Storage;

internal sealed class SftpConnectionFactory(IOptions<SftpStorageOptions> options)
{
    private readonly SftpStorageOptions _options = options.Value;

    public SftpClient CreateClient()
    {
        ConnectionInfo connectionInfo;

        if (!string.IsNullOrWhiteSpace(_options.PrivateKeyPath))
        {
            var keyFile = string.IsNullOrWhiteSpace(_options.PrivateKeyPassphrase)
                ? new PrivateKeyFile(_options.PrivateKeyPath)
                : new PrivateKeyFile(_options.PrivateKeyPath, _options.PrivateKeyPassphrase);

            connectionInfo = new ConnectionInfo(
                _options.Host,
                _options.Port,
                _options.Username,
                new PrivateKeyAuthenticationMethod(_options.Username, keyFile));
        }
        else
        {
            connectionInfo = new ConnectionInfo(
                _options.Host,
                _options.Port,
                _options.Username,
                new PasswordAuthenticationMethod(_options.Username, _options.Password ?? string.Empty));
        }

        connectionInfo.Timeout = _options.ConnectionTimeout;

        return new SftpClient(connectionInfo)
        {
            OperationTimeout = _options.OperationTimeout
        };
    }

    public string NormalizeRemoteRoot()
    {
        var root = (_options.RemotePath ?? "/").Replace('\\', '/');
        if (!root.StartsWith('/'))
        {
            root = "/" + root;
        }

        return root.TrimEnd('/');
    }
}
