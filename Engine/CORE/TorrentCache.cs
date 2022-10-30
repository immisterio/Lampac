using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Lampac.Engine.CORE
{
    public static class TorrentCache
    {
        #region Read
        public static bool Read(string key, out byte[] torrent)
        {
            try
            {
                string pathfile = getFolder(key);
                if (File.Exists(pathfile))
                {
                    torrent = File.ReadAllBytes(pathfile);
                    return true;
                }
            }
            catch { }

            torrent = null;
            return false;
        }

        public static bool Read(string key, out string torrent)
        {
            try
            {
                string pathfile = getFolder($"{key}:magnet");
                if (File.Exists(pathfile))
                {
                    torrent = File.ReadAllText(pathfile);
                    return true;
                }
            }
            catch { }

            torrent = null;
            return false;
        }
        #endregion

        #region Write
        public static void Write(string key, byte[] torrent)
        {
            try
            {
                File.WriteAllBytes(getFolder(key), torrent);
            }
            catch { }
        }

        public static void Write(string key, string torrent)
        {
            try
            {
                File.WriteAllText(getFolder($"{key}:magnet"), torrent);
            }
            catch { }
        }
        #endregion

        #region getFolder
        static string getFolder(string key)
        {
            return $"cache/torrent/{CrypTo.md5(key)}";
        }
        #endregion
    }
}
