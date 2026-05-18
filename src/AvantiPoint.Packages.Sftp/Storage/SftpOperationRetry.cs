using System.Net.Sockets;
using Renci.SshNet.Common;

namespace AvantiPoint.Packages.Sftp.Storage;

internal static class SftpOperationRetry
{
    private const int MaxAttempts = 3;

    public static void Execute(Action action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex))
            {
                Thread.Sleep(GetDelay(attempt));
            }
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is SshConnectionException or
        ProxyException or
        SocketException or
        IOException;

    private static TimeSpan GetDelay(int attempt) =>
        TimeSpan.FromMilliseconds(100 * attempt);
}
