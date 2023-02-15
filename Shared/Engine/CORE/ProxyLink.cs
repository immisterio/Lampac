using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Lampac.Engine.CORE
{
    public static class ProxyLink
    {
        static ConcurrentDictionary<string, (DateTime upd, string reqip, string uri)> links = new ConcurrentDictionary<string, (DateTime upd, string reqip, string uri)>();

        public static string Encrypt(string uri, string reqip)
        {
            if (!AppInit.conf.serverproxy.encrypt)
                return uri;

            string hash = CrypTo.md5(uri + reqip);
            if (string.IsNullOrWhiteSpace(hash))
                return string.Empty;

            if (uri.Contains(".m3u"))
                hash += ".m3u8";
            else if (uri.Contains(".ts"))
                hash += ".ts";
            else if (uri.Contains(".mp4"))
                hash += ".mp4";
            else if (uri.Contains(".mkv"))
                hash += ".mkv";
            else if (uri.Contains(".jpg") || uri.Contains(".jpeg") || uri.Contains(".png") || uri.Contains(".webp"))
                hash += ".jpg";

            if (!links.ContainsKey(hash))
                links.AddOrUpdate(hash, (DateTime.Now, reqip, uri) , (d,u) => (DateTime.Now, reqip, uri));

            return hash;
        }

        public static string Decrypt(string hash, string reqip)
        {
            if (!AppInit.conf.serverproxy.encrypt)
                return hash;

            if (links.TryGetValue(hash, out (DateTime upd, string reqip, string uri) val))
            {
                if (reqip == null || reqip == val.reqip)
                    return val.uri;
            }

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
