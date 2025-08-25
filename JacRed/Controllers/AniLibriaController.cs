using Microsoft.AspNetCore.Mvc;
using JacRed.Models.AniLibria;

namespace JacRed.Controllers
{
    [Route("anilibria/[action]")]
    public class AniLibriaController : JacBaseController
    {
        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string url, string code)
        {
            if (!jackett.Anilibria.enable || jackett.Anilibria.showdown)
                return Content("disable");

            var proxyManager = new ProxyManager("anilibria", jackett.Anilibria);

            byte[] _t = await Http.Download($"{jackett.Anilibria.host}/{url}", referer: $"{jackett.Anilibria.host}/release/{code}.html", proxy: proxyManager.Get());
            if (_t != null && BencodeTo.Magnet(_t) != null)
                return File(_t, "application/x-bittorrent");

            proxyManager.Refresh();
            return Content("error");
        }
        #endregion

        #region parsePage
        async public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query)
        {
            if (!jackett.Anilibria.enable)
                return false;

            var proxyManager = new ProxyManager("anilibria", jackett.Anilibria);

            var roots = await Http.Get<List<RootObject>>("https://api.anilibria.tv/v2/searchTitles?search=" + HttpUtility.UrlEncode(query), timeoutSeconds: jackett.timeoutSeconds, proxy: proxyManager.Get(), IgnoreDeserializeObject: true);
            if (roots == null || roots.Count == 0)
            {
                consoleErrorLog("anilibria");
                proxyManager.Refresh();
                return false;
            }

            foreach (var root in roots)
            {
                DateTime createTime = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(root.last_change > root.updated ? root.last_change : root.updated);

                foreach (var torrent in root.torrents.list)
                {
                    if (string.IsNullOrWhiteSpace(root.code) || 480 >= torrent.quality.resolution && string.IsNullOrWhiteSpace(torrent.quality.encoder) && string.IsNullOrWhiteSpace(torrent.url))
                        continue;

                    torrents.Add(new TorrentDetails()
                    {
                        trackerName = "anilibria.tv",
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
