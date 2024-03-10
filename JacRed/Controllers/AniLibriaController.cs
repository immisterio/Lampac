using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Models.JAC.AniLibria;
using System.Collections.Concurrent;
using System.Web;
using Microsoft.Extensions.Caching.Memory;
using Lampac.Engine.Parse;
using Shared;
using Shared.Engine.CORE;
using JacRed.Engine;
using JacRed.Models;

namespace Lampac.Controllers.JAC
{
    [Route("anilibria/[action]")]
    public class AniLibriaController : JacBaseController
    {
        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string url, string code)
        {
            if (!jackett.Anilibria.enable)
                return Content("disable");

            string key = $"anilibria:parseMagnet:{url}";
            if (Startup.memoryCache.TryGetValue(key, out byte[] _m))
                return File(_m, "application/x-bittorrent");

            var proxyManager = new ProxyManager("anilibria", jackett.Anilibria);

            byte[] _t = await HttpClient.Download($"{jackett.Anilibria.host}/{url}", referer: $"{jackett.Anilibria.host}/release/{code}.html", timeoutSeconds: 10, proxy: proxyManager.Get());
            if (_t != null && BencodeTo.Magnet(_t) != null)
            {
                if (jackett.cache)
                {
                    TorrentCache.Write(key, _t);
                    Startup.memoryCache.Set(key, _t, DateTime.Now.AddMinutes(Math.Max(1, jackett.torrentCacheToMinutes)));
                }

                return File(_t, "application/x-bittorrent");
            }
            else if (TorrentCache.Read(key) is var tcache && tcache.cache)
            {
                return File(tcache.torrent, "application/x-bittorrent");
            }

            proxyManager.Refresh();
            return Content("error");
        }
        #endregion

        #region parsePage
        async public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query)
        {
            string memkey = $"anilibria:{query}";

            if (!jackett.Anilibria.enable || Startup.memoryCache.TryGetValue($"{memkey}:error", out _))
                return false;

            #region Кеш поиска
            if (!Startup.memoryCache.TryGetValue(memkey, out List<RootObject> roots))
            {
                var proxyManager = new ProxyManager("anilibria", jackett.Anilibria);

                roots = await HttpClient.Get<List<RootObject>>("https://api.anilibria.tv/v2/searchTitles?search=" + HttpUtility.UrlEncode(query), timeoutSeconds: jackett.timeoutSeconds, proxy: proxyManager.Get(), IgnoreDeserializeObject: true);
                if (roots == null || roots.Count == 0)
                {
                    if (jackett.emptycache && jackett.cache)
                        Startup.memoryCache.Set($"{memkey}:error", 0, DateTime.Now.AddMinutes(Math.Max(1, jackett.cacheToMinutes)));

                    proxyManager.Refresh();
                    return false;
                }

                if (jackett.cache)
                    Startup.memoryCache.Set(memkey, roots, DateTime.Now.AddMinutes(Math.Max(1, jackett.cacheToMinutes)));
            }
            #endregion

            foreach (var root in roots)
            {
                DateTime createTime = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(root.last_change > root.updated ? root.last_change : root.updated);

                foreach (var torrent in root.torrents.list)
                {
                    if (string.IsNullOrWhiteSpace(root.code) || 480 >= torrent.quality.resolution && string.IsNullOrWhiteSpace(torrent.quality.encoder) && string.IsNullOrWhiteSpace(torrent.url))
                        continue;

                    torrents.Add(new TorrentDetails()
                    {
                        trackerName = "anilibria",
                        types = new string[] { "anime" },
                        url = $"{jackett.Anilibria.host}/release/{root.code}.html",
                        title = $"{root.names.ru} / {root.names.en} {root.season.year} (s{root.season.code}, e{torrent.series.@string}) [{torrent.quality.@string}]",
                        sid = torrent.seeders,
                        pir = torrent.leechers,
                        createTime = createTime,
                        parselink = $"{host}/anilibria/parsemagnet?url={HttpUtility.UrlEncode(torrent.url)}&code={root.code}",
                        sizeName = tParse.BytesToString(torrent.total_size),
                        name = root.names.ru,
                        originalname = root.names.en,
                        relased = root.season.year
                    });
                }
            }


            return true;
        }
        #endregion
    }
}
