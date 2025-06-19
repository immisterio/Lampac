using Lampac.Engine.CORE;
using System.Security.Cryptography;
using System.Text;

namespace Shared.Engine.CORE
{
    public static class AesTo
    {
        static byte[] key;
        static byte[] iv;

        static AesTo()
        {
            if (File.Exists("cache/aeskey"))
            {
                var i = File.ReadAllText("cache/aeskey").Split("/");
                key = Encoding.UTF8.GetBytes(i[0]);
                iv = Encoding.UTF8.GetBytes(i[1]);
            }
            else
            {
                string k = CrypTo.unic(16);
                string v = CrypTo.unic(16);
                File.WriteAllText("cache/aeskey", $"{k}/{v}");

                key = Encoding.UTF8.GetBytes(k);
                iv = Encoding.UTF8.GetBytes(v);
            }
        }


        public static string Encrypt(in string plainText)
        {
            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        {
                            using (StreamWriter sw = new StreamWriter(cs))
                                sw.Write(plainText);

                            return Convert.ToBase64String(ms.ToArray());
                        }
                    }
                }
            }
            catch { return null; }
        }

        public static string Decrypt(in string cipherText)
        {
            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(cipherText)))
                    {
                        using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader sr = new StreamReader(cs))
                            {
                                return sr.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch { return null; }
        }
    }
}
