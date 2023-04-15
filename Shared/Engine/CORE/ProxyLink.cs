using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lampac.Engine.CORE
{
    public static class ProxyLink
    {
        static ConcurrentDictionary<string, (DateTime upd, string reqip, List<(string name, string val)> headers, string uri)> links = new ConcurrentDictionary<string, (DateTime upd, string reqip, List<(string name, string val)> headers, string uri)>();

        public static string Encrypt(string uri, string reqip, List<(string name, string val)> headers = null)
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
                links.AddOrUpdate(hash, (DateTime.Now, reqip, headers, uri) , (d,u) => (DateTime.Now, reqip, headers, uri));

            return hash;
        }

        public static (List<(string name, string val)> headers, string uri) Decrypt(string hash, string reqip)
        {
            if (!AppInit.conf.serverproxy.encrypt)
                return (null, hash);

            if (links.TryGetValue(hash, out (DateTime upd, string reqip, List <(string name, string val)> headers, string uri) val))
            {
                if (reqip == null || reqip == val.reqip)
                    return (val.headers, val.uri);
            }

            return (null, null);
        }


        async public static Task Cron()
        {
            await Task.Delay(TimeSpan.FromMinutes(1));

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
