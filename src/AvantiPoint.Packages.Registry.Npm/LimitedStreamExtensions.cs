namespace AvantiPoint.Packages.Registry.Npm;

internal static class LimitedStreamExtensions
{
    public static async Task CopyToWithLimitAsync(
        this Stream source,
        Stream destination,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new NpmPublishSizeLimitExceededException(
                    $"Publish payload exceeds the configured limit of {maxBytes} bytes.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }
}
