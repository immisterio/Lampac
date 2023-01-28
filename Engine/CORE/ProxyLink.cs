using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Lampac.Engine.CORE
{
    public static class ProxyLink
    {
        static ConcurrentDictionary<string, (DateTime upd, string uri)> links = new ConcurrentDictionary<string, (DateTime upd, string uri)>();

        public static string Encrypt(string uri)
        {
            string hash = CrypTo.md5(uri);
            if (string.IsNullOrWhiteSpace(hash))
                return string.Empty;

            if (uri.Contains(".m3u"))
                hash += ".m3u8";
            else if (uri.Contains(".ts"))
                hash += ".ts";
            else if (uri.Contains(".mp4"))
                hash += ".mp4";

            if (!links.ContainsKey(hash))
                links.AddOrUpdate(hash, (DateTime.Now, uri) , (d,u) => (DateTime.Now, uri));

            return hash;
        }

        public static string Decrypt(string hash)
        {
            if (links.TryGetValue(hash, out (DateTime upd, string uri) val))
                return val.uri;

            return null;
        }


        async public static Task Cron()
        {
            while (true)
            {
                try
                {
                    foreach (var link in links)
                    {
                        if (DateTime.Now > link.Value.upd.AddHours(8))
                            links.TryRemove(link.Key, out _);
                    }
                }
                catch { }

                await Task.Delay(TimeSpan.FromMinutes(20));
            }
        }
    }
}
