using System.Security.Cryptography;
using System.Text;

namespace Shared.Services;

public static class CryptoKit
{
    public static string RandomKey()
    {
        Span<byte> key = stackalloc byte[32]; // 256-bit
        RandomNumberGenerator.Fill(key);

        int base64Length = ((key.Length + 2) / 3) * 4;

        return string.Create(base64Length, key, static (span, key) =>
        {
            Convert.TryToBase64Chars(key, span, out _);
        });
    }

    public static bool TestKey(string keyBase64)
    {
        try
        {
            // Максимально возможный размер декодированных данных
            int maxKeyLen = (keyBase64.Length * 3) / 4;
            Span<byte> key = maxKeyLen <= 64 // AES ключ максимум 32 байта
                ? stackalloc byte[maxKeyLen]
                : new byte[maxKeyLen]; // fallback, если вдруг ключ неадекватного размера

            if (!Convert.TryFromBase64String(keyBase64, key, out int keyLen))
                return false;

            Span<byte> nonce = stackalloc byte[12];
            RandomNumberGenerator.Fill(nonce);

            Span<byte> plaintext = stackalloc byte[4];
            Encoding.UTF8.GetBytes("test", plaintext);

            Span<byte> ciphertext = stackalloc byte[plaintext.Length];
            Span<byte> tag = stackalloc byte[16];

            using (var aes = new AesGcm(key.Slice(0, keyLen), 16))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag);

                Span<byte> decrypted = stackalloc byte[ciphertext.Length];
                aes.Decrypt(nonce, ciphertext, tag, decrypted);

                return decrypted.SequenceEqual("test"u8);
            }
        }
        catch
        {
            return false;
        }
    }

    public static bool Write(string keyBase64, ReadOnlySpan<char> json, string filePath)
    {
        try
        {
            // Максимально возможный размер декодированных данных
            int maxKeyLen = (keyBase64.Length * 3) / 4;
            Span<byte> key = maxKeyLen <= 64 // AES ключ максимум 32 байта
                ? stackalloc byte[maxKeyLen]
                : new byte[maxKeyLen]; // fallback, если вдруг ключ неадекватного размера

            if (!Convert.TryFromBase64String(keyBase64, key, out int keyLen))
                return false;

            int plainLen = Encoding.UTF8.GetByteCount(json);

            using (var plain = new BufferBytePool(plainLen))
            {
                using (var cipher = new BufferBytePool(plainLen))
                {
                    Span<byte> pt = plain.Span.Slice(0, plainLen);
                    Span<byte> ct = cipher.Span.Slice(0, plainLen);

                    int written = Encoding.UTF8.GetBytes(json, pt);
                    if (written != plainLen)
                        return false;

                    plain.Advance(written);
                    cipher.Advance(plainLen);

                    Span<byte> tag = stackalloc byte[16];
                    Span<byte> nonce = stackalloc byte[12];
                    RandomNumberGenerator.Fill(nonce);

                    using (var aes = new AesGcm(key.Slice(0, keyLen), 16))
                        aes.Encrypt(nonce, plain.WrittenSpan, ct, tag);

                    using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        fs.Write(nonce);
                        fs.Write(tag);
                        fs.Write(cipher.WrittenSpan);
                    }

                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "CatchId={CatchId}", "id_8q4btpsc");
            return false;
        }
    }

    public static string ReadFile(string keyBase64, string filePath)
    {
        try
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long len64 = fs.Length;

                if (len64 < 28)
                    return null;

                if (len64 > int.MaxValue)
                    return null;

                int len = (int)len64;
                using (var buff = new BufferBytePool(len))
                {
                    Span<byte> data = buff.Span.Slice(0, len);

                    int total = 0;
                    while (total < len)
                    {
                        int n = fs.Read(data.Slice(total));
                        if (n <= 0)
                            return null;

                        total += n;
                    }

                    return Read(keyBase64, data);
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "CatchId={CatchId}", "id_wvnmxztd");
            return null;
        }
    }

    public static string Read(string keyBase64, ReadOnlySpan<char> data)
    {
        try
        {
            using (var buff = new BufferBytePool(data.Length))
            {
                if (!Convert.TryFromBase64Chars(data, buff.Span, out int written))
                    return null;

                buff.Advance(written);

                return Read(keyBase64, buff.WrittenSpan);
            }
        }
        catch
        {
            return null;
        }
    }

    public static string Read(string keyBase64, byte[] data)
    {
        return Read(keyBase64, data.AsSpan());
    }

    public static string Read(string keyBase64, ReadOnlySpan<byte> data)
    {
        try
        {
            if (data.Length < 28)
                return null;

            int maxKeyLen = (keyBase64.Length * 3) / 4;
            Span<byte> key = maxKeyLen <= 64 // AES ключ максимум 32 байта
                ? stackalloc byte[maxKeyLen]
                : new byte[maxKeyLen]; // fallback, если вдруг ключ неадекватного размера

            if (!Convert.TryFromBase64String(keyBase64, key, out int keyLen))
                return null;

            key = key.Slice(0, keyLen);

            ReadOnlySpan<byte> nonce = data.Slice(0, 12);
            ReadOnlySpan<byte> tag = data.Slice(12, 16);
            ReadOnlySpan<byte> ciphertext = data.Slice(28);

            int plainLen = ciphertext.Length;
            using (var plain = new BufferBytePool(plainLen))
            {
                Span<byte> pt = plain.Span.Slice(0, plainLen);

                using (var aes = new AesGcm(key, 16))
                    aes.Decrypt(nonce, ciphertext, tag, pt);

                return Encoding.UTF8.GetString(pt);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "CatchId={CatchId}", "id_rcxio6vq");
            return null;
        }
    }
}
