using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Services.Utilities;

public struct Fnv1aHash
{
    public ulong H1;
    public ulong H2;

    public Fnv1aHash(ulong h1, ulong h2)
    {
        H1 = h1;
        H2 = h2;
    }
}

public static class Fnv1a
{
    #region static
    [ThreadStatic]
    private static byte[] _byteBuffer;
    private const int _bufferSize = 4096;

    private const ulong _prime = 1099511628211UL;
    private const ulong _offsetH1 = 14695981039346656037UL;
    private const ulong _offsetH2 = 1099511628211UL ^ 0x9E3779B97F4A7C15UL;
    #endregion

    #region Empty / IsEmpty
    public static Fnv1aHash Empty
        => new(_offsetH1, _offsetH2);

    public static bool IsEmpty(in Fnv1aHash hash)
        => hash.H1 == _offsetH1 && hash.H2 == _offsetH2;
    #endregion

    #region RandomHash
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
    #endregion

    #region Hash
    public static Fnv1aHash Hash(ReadOnlySpan<char> value)
    {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);

        BufferBytePool cipherBuf = null;

        if (maxBytes > _bufferSize)
            cipherBuf = new BufferBytePool(maxBytes);
        else
            _byteBuffer ??= new byte[_bufferSize];

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
    #endregion

    #region Append
    public static void Append(ref Fnv1aHash hash, ReadOnlySpan<char> value)
    {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);

        BufferBytePool cipherBuf = null;

        if (maxBytes > _bufferSize)
            cipherBuf = new BufferBytePool(maxBytes);
        else
            _byteBuffer ??= new byte[_bufferSize];

        try
        {
            Span<byte> cipher = cipherBuf != null
                ? cipherBuf.Span
                : _byteBuffer;

            int written = Encoding.UTF8.GetBytes(value, cipher);

            Append(ref hash, cipher[..written]);
        }
        finally
        {
            cipherBuf?.Dispose();
        }
    }

    public static void Append(ref Fnv1aHash hash, char value)
    {
        Span<byte> buffer = stackalloc byte[4];

        int written = Encoding.UTF8.GetBytes(
            MemoryMarshal.CreateReadOnlySpan(ref value, 1),
            buffer
        );

        Append(ref hash, buffer[..written]);
    }

    public static void Append(ref Fnv1aHash hash, ReadOnlySpan<byte> value)
    {
        foreach (byte b in value)
        {
            hash.H1 ^= b;
            hash.H1 *= _prime;

            hash.H2 ^= b;
            hash.H2 *= _prime;
        }
    }
    #endregion

    #region Base64Url
    public static string Base64Url(string value)
        => Base64Url(Hash(value));

    public static string Base64Url(in Fnv1aHash hash)
    {
        Span<byte> bytes = stackalloc byte[16];

        BinaryPrimitives.WriteUInt64BigEndian(bytes[..8], hash.H1);
        BinaryPrimitives.WriteUInt64BigEndian(bytes[8..], hash.H2);

        return System.Buffers.Text.Base64Url.EncodeToString(bytes);
    }
    #endregion
}
