using System.Buffers.Binary;
using System.Text;

namespace Shared.Services.Utilities;

public readonly record struct Fnv1aHash(ulong H1, ulong H2);

public static class Fnv1a
{
    [ThreadStatic]
    private static byte[] _byteBuffer;

    private const ulong _prime = 1099511628211UL;
    private const ulong _offsetH1 = 14695981039346656037UL;
    private const ulong _offsetH2 = 1099511628211UL ^ 0x9E3779B97F4A7C15UL;

    public static Fnv1aHash RandomHash()
    {
        Span<byte> bytes = stackalloc byte[16];
        Guid.NewGuid().TryWriteBytes(bytes);

        ulong h1 = _offsetH1;
        ulong h2 = _offsetH2;

        foreach (byte b in bytes)
        {
            h1 ^= b;
            h1 *= _prime;

            h2 ^= b;
            h2 *= _prime;
        }

        return new(h1, h2);
    }

    public static Fnv1aHash Hash(ReadOnlySpan<char> value)
    {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);

        BufferBytePool cipherBuf = null;

        if (maxBytes > 4096)
            cipherBuf = new BufferBytePool(maxBytes);
        else
            _byteBuffer ??= new byte[4096];

        try
        {
            Span<byte> cipher = cipherBuf != null
                ? cipherBuf.Span
                : _byteBuffer;

            int written = Encoding.UTF8.GetBytes(value, cipher);

            ulong h1 = _offsetH1;
            ulong h2 = _offsetH2;

            foreach (byte b in cipher[..written])
            {
                h1 ^= b;
                h1 *= _prime;

                h2 ^= b;
                h2 *= _prime;
            }

            return new(h1, h2);
        }
        finally
        {
            cipherBuf?.Dispose();
        }
    }

    public static string Base64Url(string value)
        => Base64Url(Hash(value));

    public static string Base64Url(in Fnv1aHash hash)
    {
        Span<byte> bytes = stackalloc byte[16];

        BinaryPrimitives.WriteUInt64BigEndian(bytes[..8], hash.H1);
        BinaryPrimitives.WriteUInt64BigEndian(bytes[8..], hash.H2);

        return System.Buffers.Text.Base64Url.EncodeToString(bytes);
    }
}
