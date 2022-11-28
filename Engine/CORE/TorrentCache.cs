using System.IO;
using System.Threading.Tasks;

namespace Lampac.Engine.CORE
{
    public static class TorrentCache
    {
        #region Exists
        public static bool Exists(string key)
        {
            return File.Exists(getFolder(key)) || File.Exists(getFolder($"{key}:magnet"));
        }
        #endregion

        #region Read
        async public static ValueTask<(bool cache, byte[] torrent)> Read(string key)
        {
            try
            {
                string pathfile = getFolder(key);
                if (File.Exists(pathfile))
                    return (true, await File.ReadAllBytesAsync(pathfile));
            }
            catch { }

            return (false, null);
        }

        async public static ValueTask<(bool cache, string torrent)> ReadMagnet(string key)
        {
            try
            {
                string pathfile = getFolder($"{key}:magnet");
                if (File.Exists(pathfile))
                    return (true, await File.ReadAllTextAsync(pathfile));
            }
            catch { }

            return (false, null);
        }
        #endregion

        #region Write
        async public static ValueTask Write(string key, byte[] torrent)
        {
            try
            {
                await File.WriteAllBytesAsync(getFolder(key), torrent);
            }
            catch { }
        }

        async public static ValueTask Write(string key, string torrent)
        {
            try
            {
                await File.WriteAllTextAsync(getFolder($"{key}:magnet"), torrent);
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
