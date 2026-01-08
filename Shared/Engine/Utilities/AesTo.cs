using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Shared.Engine
{
    public static class AesTo
    {
        static byte[] aesKey, aesIV;
        static readonly ThreadLocal<ThreadState> tls = new(() => new ThreadState());

        static AesTo()
        {
            if (File.Exists("cache/aeskey"))
            {
                var i = File.ReadAllText("cache/aeskey").Split("/");
                aesKey = Encoding.UTF8.GetBytes(i[0]);
                aesIV = Encoding.UTF8.GetBytes(i[1]);
            }
            else
            {
                string k = CrypTo.unic(16);
                string v = CrypTo.unic(16);
                File.WriteAllText("cache/aeskey", $"{k}/{v}");

                aesKey = Encoding.UTF8.GetBytes(k);
                aesIV = Encoding.UTF8.GetBytes(v);
            }
        }


        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                return null;

            byte[] cipherBuf = null;

            try
            {
                var state = tls.Value!;
                var aes = state.Aes;

                int plainByteCount = Encoding.UTF8.GetByteCount(plainText);
                EnsureSize(ref state.Plain, plainByteCount);

                int writtenPlain = Encoding.UTF8.GetBytes(plainText, 0, plainText.Length, state.Plain, 0);

                int blockSize = aes.BlockSize / 8; // 16
                int paddedLen = ((writtenPlain / blockSize) + 1) * blockSize;

                cipherBuf = ArrayPool<byte>.Shared.Rent(paddedLen);

                // ВАЖНО: iv вторым параметром, destination третьим
                int cipherLen = aes.EncryptCbc(
                    state.Plain.AsSpan(0, writtenPlain),
                    aesIV,                               // iv (16 байт)
                    cipherBuf.AsSpan(0, paddedLen),      // destination
                    PaddingMode.PKCS7);

                int base64Len = GetBase64Length(cipherLen);
                EnsureSize(ref state.Base64Chars, base64Len);

                if (!Convert.TryToBase64Chars(cipherBuf.AsSpan(0, cipherLen), state.Base64Chars, out int charsWritten))
                    return null;

                return new string(state.Base64Chars, 0, charsWritten);
            }
            catch
            {
                return plainText;
            }
            finally
            {
                if (cipherBuf != null)
                    ArrayPool<byte>.Shared.Return(cipherBuf);
            }
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrWhiteSpace(cipherText))
                return null;

            byte[] cipherBuf = null;

            try
            {
                var state = tls.Value;
                var aes = state.Aes;

                int maxCipherBytes = GetMaxDecodedLength(cipherText.Length);
                cipherBuf = ArrayPool<byte>.Shared.Rent(maxCipherBytes);

                if (!Convert.TryFromBase64String(cipherText, cipherBuf, out int cipherLen))
                    return null;

                EnsureSize(ref state.Plain, cipherLen);

                // ВАЖНО: iv вторым параметром, destination третьим
                int plainLen = aes.DecryptCbc(
                    cipherBuf.AsSpan(0, cipherLen),
                    aesIV,                              // iv (16 байт)
                    state.Plain.AsSpan(0, cipherLen),   // destination
                    PaddingMode.PKCS7);

                return Encoding.UTF8.GetString(state.Plain, 0, plainLen);
            }
            catch
            {
                return cipherText;
            }
            finally
            {
                if (cipherBuf != null)
                    ArrayPool<byte>.Shared.Return(cipherBuf);
            }
        }


        static void EnsureSize(ref byte[] buffer, int required)
        {
            if (buffer.Length >= required) return;

            // Растим с запасом, чтобы реже реаллоцировать.
            int newSize = NextPowerOfTwo(required);
            buffer = new byte[newSize];
        }

        static void EnsureSize(ref char[] buffer, int required)
        {
            if (buffer.Length >= required) return;

            int newSize = NextPowerOfTwo(required);
            buffer = new char[newSize];
        }

        static int NextPowerOfTwo(int x)
        {
            if (x <= 0) return 1;
            x--;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }

        static int GetBase64Length(int byteCount)
            => checked(((byteCount + 2) / 3) * 4);

        static int GetMaxDecodedLength(int base64CharCount)
        {
            // Для корректного base64: максимум (len/4)*3
            // Добавим небольшой запас.
            return checked((base64CharCount / 4) * 3 + 3);
        }


        private sealed class ThreadState
        {
            public readonly Aes Aes;

            // Буферы под UTF8 plaintext и под cipher bytes
            public byte[] Plain = Array.Empty<byte>();
            public byte[] Cipher = Array.Empty<byte>();

            // Буфер под Base64 chars (Convert.TryToBase64Chars пишет в char[])
            public char[] Base64Chars = Array.Empty<char>();

            public ThreadState()
            {
                Aes = Aes.Create();
                Aes.Mode = CipherMode.CBC;
                Aes.Padding = PaddingMode.PKCS7;

                Aes.Key = aesKey;
                Aes.IV = aesIV;
            }
        }
    }
}