using System;
using System.Security.Cryptography;
using System.Text;

namespace Lampac.Engine.CORE
{
    public class CrypTo
    {
        public static string md5(string text)
        {
            if (text == null)
                return string.Empty;

            using (var md5 = MD5.Create())
            {
                var result = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
                return BitConverter.ToString(result).Replace("-", "").ToLower();
            }
        }
    }
}
