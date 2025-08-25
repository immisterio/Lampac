using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace JacRed.Controllers
{
    [Route("megapeer/[action]")]
    public class MegapeerController : JacBaseController
    {
        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query, string cat)
        {
            if (!jackett.Megapeer.enable || jackett.Megapeer.showdown)
                return Task.FromResult(false);

            return Joinparse(torrents, () => parsePage(host, query, cat));
        }
        #endregion


        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string id)
        {
            if (!jackett.Megapeer.enable)
                return Content("disable");

            var proxyManager = new ProxyManager("megapeer", jackett.Megapeer);

            byte[] _t = await Http.Download($"{jackett.Megapeer.host}/download/{id}", referer: jackett.Megapeer.host, proxy: proxyManager.Get());
            if (_t != null && BencodeTo.Magnet(_t) != null)
                return File(_t, "application/x-bittorrent");

            proxyManager.Refresh();
            return Content("error");
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query, string cat)
        {
            #region html
            var proxyManager = new ProxyManager("megapeer", jackett.Megapeer);

            string html = await Http.Get($"{jackett.Megapeer.host}/browse.php?search={HttpUtility.UrlEncode(query, Encoding.GetEncoding(1251))}&cat={cat}", encoding: Encoding.GetEncoding(1251), proxy: proxyManager.Get(), timeoutSeconds: jackett.timeoutSeconds, headers: HeadersModel.Init(
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("referer", $"{jackett.Megapeer.host}"),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-mode", "navigate"),
                ("sec-fetch-site", "same-origin"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1")
            ));

            if (html == null || !html.Contains("id=\"logo\"") || html.Contains("<H1>Раздачи за последние"))
            {
                consoleErrorLog("megapeer");
                proxyManager.Refresh();
                return null;
            }
            #endregion

            var doc = new HtmlDocument();
            doc.LoadHtml(html.Replace("&nbsp;", " "));

            var nodes = doc.DocumentNode.SelectNodes("//tr[@class='table_fon']");
            if (nodes == null || nodes.Count == 0)
                return null;

            var torrents = new List<TorrentDetails>();

            foreach (var row in nodes)
            {
                var hc = new HtmlCommon(row);

                string url = hc.Match("href=\"/(torrent/[^\"]+)\"");
                string title = hc.NodeValue(".//a[@class='url']");
                title = Regex.Replace(title, "<[^>]+>", "");

                string sizeName = hc.NodeValue(".//td[@align='right' and contains(text(), 'GB') or contains(text(), 'MB')]");
                string downloadid = hc.Match("href=\"/?download/([0-9]+)\"");
                string createTime = hc.NodeValue(".//td");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(downloadid))
                    continue;

                torrents.Add(new TorrentDetails()
                {
                    url = $"{jackett.Megapeer.host}/{url}",
                    title = title,
                    sid = HtmlCommon.Integer(hc.NodeValue(".//font[@color='#008000']")),
                    pir = HtmlCommon.Integer(hc.NodeValue(".//font[@color='#8b0000']")),
                    sizeName = sizeName,
                    parselink = $"{host}/megapeer/parsemagnet?id={downloadid}",
                    createTime = tParse.ParseCreateTime(createTime, "dd.MM.yy")
                });
            }

            return torrents;
        }
        #endregion
    }
}
