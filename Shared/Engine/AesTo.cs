using System.Security.Cryptography;
using System.Text;

namespace Shared.Engine
{
    public static class AesTo
    {
        static byte[] aesKey, aesIV;

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
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = aesKey;
                    aes.IV = aesIV;

                    using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                    {
                        using (var ms = new MemoryStream())
                        {
                            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                            {
                                try
                                {
                                    using (var sw = new StreamWriter(cs, Encoding.UTF8))
                                        sw.Write(plainText ?? string.Empty);

                                    return Convert.ToBase64String(ms.ToArray());
                                }
                                catch { return null; }
                            }
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public static string Decrypt(string cipherText)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = aesKey;
                    aes.IV = aesIV;

                    using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                    {
                        using (var ms = new MemoryStream(Convert.FromBase64String(cipherText)))
                        {
                            using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                            {
                                try
                                {
                                    using (var sr = new StreamReader(cs, Encoding.UTF8))
                                        return sr.ReadToEnd();
                                }
                                catch { return null; }
                            }
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}