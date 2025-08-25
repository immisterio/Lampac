using Microsoft.AspNetCore.Mvc;

namespace JacRed.Controllers
{
    [Route("rutor/[action]")]
    public class RutorController : JacBaseController
    {
        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query, string cat, bool isua = false, string parsecat = null)
        {
            if (!jackett.Rutor.enable || jackett.Rutor.showdown)
                return Task.FromResult(false);

            return Joinparse(torrents, () => parsePage(host, query, cat, isua, parsecat));
        }
        #endregion


        #region parseMagnet
        async public Task<ActionResult> parseMagnet(int id, string magnet)
        {
            if (!jackett.Rutor.enable || jackett.Rutor.priority != "torrent")
                return Content("disable");

            var proxyManager = new ProxyManager("rutor", jackett.Rutor);

            byte[] _t = await Http.Download($"{Regex.Replace(jackett.Rutor.host, "^(https?:)//", "$1//d.")}/download/{id}", referer: jackett.Rutor.host, proxy: proxyManager.Get());
            if (_t != null && BencodeTo.Magnet(_t) != null)
                return File(_t, "application/x-bittorrent");

            proxyManager.Refresh();

            if (string.IsNullOrEmpty(magnet))
                return Content("empty");

            return Redirect(magnet);
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query, string cat, bool isua, string parsecat)
        {
            // fix search
            query = query.Replace("\"", " ").Replace("'", " ").Replace("?", " ").Replace("&", " ");

            var proxyManager = new ProxyManager("rutor", jackett.Rutor);

            string html = await Http.Get($"{jackett.Rutor.host}/search" + (cat == "0" ? $"/{HttpUtility.UrlEncode(query)}" : $"/0/{cat}/000/0/{HttpUtility.UrlEncode(query)}"), proxy: proxyManager.Get(), timeoutSeconds: jackett.timeoutSeconds);

            if (html == null || !html.Contains("id=\"logo\""))
            {
                consoleErrorLog("rutor");
                proxyManager.Refresh();
                return null;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html.Replace("&nbsp;", " ").Replace(" ", " ")); // Меняем непонятный символ похожий на проблел, на обычный проблел

            var nodes = doc.DocumentNode.SelectNodes("//tr[@class='gai' or @class='tum']");
            if (nodes == null || nodes.Count == 0)
                return null;

            var torrents = new List<TorrentDetails>();

            foreach (var row in nodes)
            {
                var hc = new HtmlCommon(row);

                string url = hc.Match("href=\"/(torrent/[^\"]+)\"");
                string viewtopic = Regex.Match(url, "torrent/([0-9]+)").Groups[1].Value;

                string title = hc.NodeValue(".//a[contains(@href, '/torrent/')]");
                string sid = hc.NodeValue(".//span[@class='green']", removeChild: ".//img");
                string pir = hc.NodeValue(".//span[@class='red']");
                string sizeName = hc.NodeValue(".//td[@align='right' and contains(text(), 'GB') or contains(text(), 'MB')]");
                string createTime = hc.NodeValue(".//td");
                string magnet = hc.Match("href=\"(magnet:\\?xt=[^\"]+)\"");

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(magnet) || title.ToLower().Contains("трейлер"))
                    continue;

                if (isua && !title.Contains(" UKR"))
                    continue;

                torrents.Add(new TorrentDetails()
                {
                    url = $"{jackett.Rutor.host}/{url}",
                    title = title,
                    sid = HtmlCommon.Integer(sid),
                    pir = HtmlCommon.Integer(pir),
                    sizeName = sizeName,
                    magnet = jackett.Rutor.priority == "torrent" ? null : magnet,
                    parselink = jackett.Rutor.priority == "torrent" ? $"{host}/rutor/parsemagnet?id={viewtopic}&magnet={HttpUtility.UrlEncode(magnet)}" : null,
                    createTime = tParse.ParseCreateTime(createTime, "dd.MM.yy")
                });
            }

            return torrents;
        }
        #endregion
    }
}
