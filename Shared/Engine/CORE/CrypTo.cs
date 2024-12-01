﻿using System;
using System.IO;
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

        public static byte[] md5binary(string text)
        {
            if (text == null)
                return null;

            using (var md5 = MD5.Create())
            {
                var result = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
                return result;
            }
        }

        public static string DecodeBase64(string base64Text)
        {
            if (string.IsNullOrEmpty(base64Text))
                return string.Empty;

            return Encoding.UTF8.GetString(Convert.FromBase64String(base64Text));
        }

        public static string Base64(string text)
        {
            if (text == null)
                return string.Empty;

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        }

        public static string Base64(byte[] text)
        {
            if (text == null)
                return string.Empty;

            return Convert.ToBase64String(text);
        }

        public static string SHA256(string text)
        {
            using (SHA256 sha256 = System.Security.Cryptography.SHA256.Create())
            {
                // Compute the hash of the given string
                byte[] hashValue = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));

                // Convert the byte array to string format
                return BitConverter.ToString(hashValue).Replace("-", "").ToLower();
            }
        }

        public static string AES256(string text, string secret_pw, string secret_iv)
        {
            using (Aes encryptor = Aes.Create())
            {
                encryptor.Mode = CipherMode.CBC;
                encryptor.KeySize = 256;
                encryptor.BlockSize = 128;
                encryptor.Padding = PaddingMode.PKCS7;

                // Set key and IV
                encryptor.Key = Encoding.UTF8.GetBytes(SHA256(secret_pw).Substring(0, 32));
                encryptor.IV = Encoding.UTF8.GetBytes(SHA256(secret_iv).Substring(0, 16));

                // Instantiate a new MemoryStream object to contain the encrypted bytes
                MemoryStream memoryStream = new MemoryStream();

                // Instantiate a new encryptor from our Aes object
                ICryptoTransform aesEncryptor = encryptor.CreateEncryptor();

                // Instantiate a new CryptoStream object to process the data and write it to the 
                // memory stream
                CryptoStream cryptoStream = new CryptoStream(memoryStream, aesEncryptor, CryptoStreamMode.Write);

                // Convert the plainText string into a byte array
                byte[] plainBytes = Encoding.UTF8.GetBytes(text);

                // Encrypt the input plaintext string
                cryptoStream.Write(plainBytes, 0, plainBytes.Length);

                // Complete the encryption process
                cryptoStream.FlushFinalBlock();

                // Convert the encrypted data from a MemoryStream to a byte array
                byte[] cipherBytes = memoryStream.ToArray();

                // Close both the MemoryStream and the CryptoStream
                memoryStream.Close();
                cryptoStream.Close();

                // Convert the encrypted byte array to a base64 encoded string
                return Convert.ToBase64String(cipherBytes, 0, cipherBytes.Length);
            }
        }

        static string ArrayList => "qwertyuioplkjhgfdsazxcvbnmQWERTYUIOPLKJHGFDSAZXCVBNM1234567890";
        static string ArrayListToNumber => "1234567890";
        public static string unic(int size = 8, bool IsNumberCode = false)
        {
            StringBuilder array = new StringBuilder();
            for (int i = 0; i < size; i++)
            {
                array.Append(IsNumberCode ? ArrayListToNumber[Random.Shared.Next(0, 9)] : ArrayList[Random.Shared.Next(0, 61)]);
            }

            return array.ToString();
        }
    }
}
