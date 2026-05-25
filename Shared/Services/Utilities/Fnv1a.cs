using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Unicode;

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
    private const ulong _prime = 1099511628211UL;
    private const ulong _offsetH1 = 14695981039346656037UL;
    private const ulong _offsetH2 = 1099511628211UL ^ 0x9E3779B97F4A7C15UL;

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
        RandomNumberGenerator.Fill(bytes);

        var hash = Empty;
        Append(ref hash, bytes);
        return hash;
    }
    #endregion

    #region Hash
    public static Fnv1aHash Hash(ReadOnlySpan<char> value)
    {
        var hash = Empty;
        Append(ref hash, value);
        return hash;
    }
    #endregion

    #region Append
    public static void Append(ref Fnv1aHash hash, ReadOnlySpan<char> value)
    {
        Span<byte> buffer = stackalloc byte[512];

        while (!value.IsEmpty)
        {
            var status = Utf8.FromUtf16(
                value,
                buffer,
                out int charsRead,
                out int bytesWritten,
                replaceInvalidSequences: true,
                isFinalBlock: true);

            if (bytesWritten > 0)
                Append(ref hash, buffer[..bytesWritten]);

            value = value[charsRead..];

            if (status == OperationStatus.Done)
                return;

            if (status == OperationStatus.DestinationTooSmall)
                continue;

            // UTF-8 conversion failed, хз как мы сюда попали, но просто не хэшируем эту часть строки
            return;
        }
    }

    public static void Append(ref Fnv1aHash hash, char value)
    {
        Span<char> chars = stackalloc char[1];
        Span<byte> buffer = stackalloc byte[4];

        chars[0] = value;

        var status = Utf8.FromUtf16(
            chars,
            buffer,
            out _,
            out int bytesWritten,
            replaceInvalidSequences: true,
            isFinalBlock: true);

        if (status == OperationStatus.Done)
            Append(ref hash, buffer[..bytesWritten]);
    }

    public static void Append(ref Fnv1aHash hash, ReadOnlySpan<byte> value)
    {
        ulong h1 = hash.H1;
        ulong h2 = hash.H2;

        foreach (byte b in value)
        {
            h1 ^= b;
            h1 *= _prime;

            h2 ^= b;
            h2 *= _prime;
        }

        hash.H1 = h1;
        hash.H2 = h2;
    }

    public static void Append(ref Fnv1aHash hash, IReadOnlyList<byte> value)
    {
        ulong h1 = hash.H1;
        ulong h2 = hash.H2;

        foreach (byte b in value)
        {
            h1 ^= b;
            h1 *= _prime;

            h2 ^= b;
            h2 *= _prime;
        }

        hash.H1 = h1;
        hash.H2 = h2;
    }
    #endregion

    #region Base64Url
    public static string Base64Url(ReadOnlySpan<char> value)
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