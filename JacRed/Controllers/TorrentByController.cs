using Microsoft.AspNetCore.Mvc;

namespace JacRed.Controllers
{
    [Route("torrentby/[action]")]
    public class TorrentByController : JacBaseController
    {
        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query, string cat)
        {
            if (!jackett.TorrentBy.enable || jackett.TorrentBy.showdown)
                return Task.FromResult(false);

            return Joinparse(torrents, () => parsePage(host, query, cat));
        }
        #endregion


        #region parseMagnet
        async public Task<ActionResult> parseMagnet(int id, string magnet)
        {
            if (!jackett.TorrentBy.enable || jackett.TorrentBy.priority != "torrent")
                return Content("disable");

            var proxyManager = new ProxyManager("torrentby", jackett.TorrentBy);

            var _t = await Http.Download($"{jackett.TorrentBy.host}/d.php?id={id}", referer: jackett.TorrentBy.host, proxy: proxyManager.Get());
            if (_t != null && BencodeTo.Magnet(_t) != null)
                return File(_t, "application/x-bittorrent");

            proxyManager.Refresh();

            if (string.IsNullOrEmpty(magnet))
                return Content("empty");

            return Redirect(magnet);
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query, string cat)
        {
            #region html
            var proxyManager = new ProxyManager("torrentby", jackett.TorrentBy);

            string html = await Http.Get($"{jackett.TorrentBy.host}/search/?search={HttpUtility.UrlEncode(query)}&category={cat}", proxy: proxyManager.Get(), timeoutSeconds: jackett.timeoutSeconds);

            if (html == null || !html.Contains("id=\"find\""))
            {
                consoleErrorLog("torrentby");
                proxyManager.Refresh();
                return null;
            }
            #endregion

            var doc = new HtmlDocument();
            doc.LoadHtml(html.Replace("&nbsp;", " "));

            var nodes = doc.DocumentNode.SelectNodes("//tr[contains(@class, 'ttable_col')]");
            if (nodes == null || nodes.Count == 0)
                return null;

            var torrents = new List<TorrentDetails>();

            foreach (var row in nodes)
            {
                var hc = new HtmlCommon(row);

                #region Дата создания
                DateTime createTime = default;

                if (row.InnerHtml.Contains("Сегодня"))
                {
                    createTime = DateTime.Today;
                }
                else if (row.InnerHtml.Contains("Вчера"))
                {
                    createTime = DateTime.Today.AddDays(-1);
                }
                else
                {
                    string _createTime = hc.Match(">([0-9]{4}-[0-9]{2}-[0-9]{2})</td>").Replace("-", " ");
                    DateTime.TryParseExact(_createTime, "yyyy MM dd", new CultureInfo("ru-RU"), DateTimeStyles.None, out createTime);
                }
                #endregion

                string url = hc.NodeValue(".//a[@name='search_select']", "href");
                string viewtopic = Regex.Match(url, "^/([0-9]+)").Groups[1].Value;

                string title = hc.NodeValue(".//a[@name='search_select']");
                title = Regex.Replace(title, "<[^>]+>", "");

                string magnet = hc.Match("href=\"(magnet:\\?xt=[^\"]+)\"");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(magnet))
                    continue;

                torrents.Add(new TorrentDetails()
                {
                    url = $"{jackett.TorrentBy.host}/{url.Remove(0, 1)}",
                    title = title,
                    sid = HtmlCommon.Integer(hc.NodeValue(".//font[@color='green']")),
                    pir = HtmlCommon.Integer(hc.NodeValue(".//font[@color='red']")),
                    sizeName = hc.NodeValue(".//td[contains(text(), 'GB') or contains(text(), 'MB')]"),
                    magnet = jackett.TorrentBy.priority == "torrent" ? null : magnet,
                    parselink = jackett.TorrentBy.priority == "torrent" ? $"{host}/torrentby/parsemagnet?id={viewtopic}&magnet={HttpUtility.UrlEncode(magnet)}" : null,
                    createTime = createTime
                });
            }

            return torrents;
        }
        #endregion
    }
}
