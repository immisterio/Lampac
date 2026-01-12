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
                return plainText;

            try
            {
                var state = tls.Value;
                var aes = state.Aes;

                int writtenPlain = Encoding.UTF8.GetBytes(plainText, 0, plainText.Length, state.encryptPlain, 0);

                int blockSize = aes.BlockSize / 8; // 16
                int paddedLen = ((writtenPlain / blockSize) + 1) * blockSize;

                if (paddedLen > state.encryptCipherBuf.Length)
                    return plainText;

                // ВАЖНО: iv вторым параметром, destination третьим
                int cipherLen = aes.EncryptCbc(
                    state.encryptPlain.AsSpan(0, writtenPlain),
                    aesIV,                                // iv (16 байт)
                    state.encryptCipherBuf.AsSpan(0, paddedLen), // destination
                    PaddingMode.PKCS7);

                if (!Convert.TryToBase64Chars(state.encryptCipherBuf.AsSpan(0, cipherLen), state.encryptBase64Chars, out int charsWritten))
                    return plainText;

                return new string(state.encryptBase64Chars, 0, charsWritten);
            }
            catch
            {
                return plainText;
            }
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrWhiteSpace(cipherText))
                return null;

            try
            {
                var state = tls.Value;
                var aes = state.Aes;

                if (!Convert.TryFromBase64String(cipherText, state.decryptCipherBuf, out int cipherLen))
                    return null;

                // ВАЖНО: iv вторым параметром, destination третьим
                int plainLen = aes.DecryptCbc(
                    state.decryptCipherBuf.AsSpan(0, cipherLen),
                    aesIV,                              // iv (16 байт)
                    state.decryptPlain.AsSpan(0, cipherLen),   // destination
                    PaddingMode.PKCS7);

                return Encoding.UTF8.GetString(state.decryptPlain, 0, plainLen);
            }
            catch
            {
                return null;
            }
        }


        private sealed class ThreadState
        {
            public readonly Aes Aes;

            public char[] encryptBase64Chars = new char[PoolInvk.rentLargeChunk];
            public byte[] encryptCipherBuf = new byte[PoolInvk.rentLargeChunk];
            public byte[] encryptPlain = new byte[PoolInvk.rentLargeChunk];

            public byte[] decryptCipherBuf = new byte[PoolInvk.rentLargeChunk];
            public byte[] decryptPlain = new byte[PoolInvk.rentLargeChunk];

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