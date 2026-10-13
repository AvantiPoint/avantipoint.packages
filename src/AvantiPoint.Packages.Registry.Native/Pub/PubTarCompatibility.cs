using System.Text;
using AvantiPoint.Packages.Registry.Native.Storage;

namespace AvantiPoint.Packages.Registry.Native.Pub;

/// <summary>
/// Dart's tar encoder writes the USTAR version as "0 " rather than POSIX "00".
/// Normalize only a bounded validation copy, after checking the original header
/// checksum. Publication always stores and hashes the unmodified .tar.gz bytes.
/// </summary>
internal static class PubTarCompatibility
{
    public static async Task<ArtifactUpload> PrepareAsync(Stream decompressed, NativeRegistryOptions limits, CancellationToken ct)
    {
        var copy = await ArtifactUpload.ReadAsync(decompressed, limits.MaxExpandedArchiveBytes, ct);
        try
        {
            var stream = copy.Stream;
            var header = new byte[512];
            var entries = 0;
            while (stream.Position < stream.Length)
            {
                var start = stream.Position;
                if (stream.Length - start < 512) throw new InvalidDataException("Truncated tar header.");
                await stream.ReadExactlyAsync(header, ct);
                if (header.All(b => b == 0))
                {
                    // Reject hidden trailing entries after the end-of-archive marker.
                    int count;
                    while ((count = await stream.ReadAsync(header, ct)) != 0)
                        if (header.AsSpan(0, count).ContainsAnyExcept((byte)0))
                            throw new InvalidDataException("Data follows the tar end marker.");
                    break;
                }
                if (++entries > limits.MaxArchiveEntries) throw new InvalidDataException("Too many tar headers.");
                var expected = Number(header.AsSpan(148, 8));
                var checksum = header.Select(b => (long)b).Sum() - header.AsSpan(148, 8).ToArray().Sum(b => (long)b) + 8 * 32;
                if (expected != checksum) throw new InvalidDataException("Invalid tar header checksum.");
                var length = Number(header.AsSpan(124, 12));
                if (length < 0 || length > limits.MaxExpandedArchiveBytes) throw new InvalidDataException("Invalid tar entry size.");
                var next = start + 512 + ((length + 511) / 512) * 512;
                if (next > stream.Length) throw new InvalidDataException("Truncated tar entry.");
                if (header.AsSpan(257, 8).SequenceEqual("ustar\00 "u8))
                {
                    header[264] = (byte)'0';
                    header.AsSpan(148, 8).Fill((byte)' ');
                    var adjusted = header.Sum(b => (long)b);
                    Encoding.ASCII.GetBytes(Convert.ToString(adjusted, 8).PadLeft(6, '0')).CopyTo(header, 148);
                    header[154] = 0;
                    header[155] = (byte)' ';
                    stream.Position = start;
                    await stream.WriteAsync(header, ct);
                }
                stream.Position = next;
            }
            stream.Position = 0;
            return copy;
        }
        catch
        {
            await copy.DisposeAsync();
            throw;
        }
    }

    private static long Number(ReadOnlySpan<byte> value)
    {
        long result = 0;
        try
        {
            if ((value[0] & 0x80) != 0)
            {
                if ((value[0] & 0x40) != 0) throw new InvalidDataException("Negative tar number.");
                result = value[0] & 0x3f;
                foreach (var b in value[1..]) result = checked(result * 256 + b);
            }
            else
            {
                var start = 0;
                var end = value.Length;
                while (start < end && value[start] is 0 or (byte)' ') start++;
                while (end > start && value[end - 1] is 0 or (byte)' ') end--;
                foreach (var b in value[start..end])
                {
                    if (b < '0' || b > '7') throw new InvalidDataException("Invalid octal tar number.");
                    result = checked(result * 8 + b - '0');
                }
            }
        }
        catch (OverflowException) { throw new InvalidDataException("Tar number is too large."); }
        return result;
    }
}
