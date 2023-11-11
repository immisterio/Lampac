using Jackett;
using Lampac.Models.AppConf;
using System.IO;
using System.Threading.Tasks;

namespace Lampac.Engine.CORE
{
    public static class TorrentCache
    {
        static JacConf jac => ModInit.conf.Jackett;


        #region Exists
        public static bool Exists(string key)
        {
            if (!ModInit.conf.Jackett.cache)
                return false;

            return File.Exists(getFolder(key)) || File.Exists(getFolder($"{key}:magnet"));
        }
        #endregion

        #region Read
        async public static ValueTask<(bool cache, byte[] torrent)> Read(string key)
        {
            if (!jac.cache)
                return default;

            try
            {
                string pathfile = getFolder(key);
                if (File.Exists(pathfile))
                    return (true, await File.ReadAllBytesAsync(pathfile));
            }
            catch { }

            return default;
        }

        async public static ValueTask<(bool cache, string torrent)> ReadMagnet(string key)
        {
            if (!jac.cache)
                return default;

            try
            {
                string pathfile = getFolder($"{key}:magnet");
                if (File.Exists(pathfile))
                    return (true, await File.ReadAllTextAsync(pathfile));
            }
            catch { }

            return default;
        }
        #endregion

        #region Write
        async public static ValueTask Write(string key, byte[] torrent)
        {
            if (!jac.cache)
                return;

            try
            {
                if (jac.torrentCacheToMinutes > 0)
                    await File.WriteAllBytesAsync(getFolder(key), torrent);
            }
            catch { }
        }

        async public static ValueTask Write(string key, string torrent)
        {
            if (!jac.cache)
                return;

            try
            {
                if (jac.torrentCacheToMinutes > 0)
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
