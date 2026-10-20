namespace AvantiPoint.Packages.Registry.Oci;

public sealed partial class OciRegistryService
{
    internal const int MaximumManifestBytes = 16 * 1024 * 1024;

    private static async Task<MemoryStream> ReadManifestContentAsync(Stream content, CancellationToken token)
    {
        var result = new MemoryStream();
        try
        {
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = await content.ReadAsync(buffer, token)) != 0)
            {
                if (result.Length + read > MaximumManifestBytes)
                    throw new OciRegistryException("The manifest exceeds the 16 MiB limit.", 413, "SIZE_INVALID");
                await result.WriteAsync(buffer.AsMemory(0, read), token);
            }
            result.Position = 0;
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }
}
