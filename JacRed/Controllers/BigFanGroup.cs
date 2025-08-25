using Microsoft.AspNetCore.Mvc;

namespace JacRed.Controllers
{
    [Route("bigfangroup/[action]")]
    public class BigFanGroup : JacBaseController
    {
        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query, string[] cats)
        {
            if (!jackett.BigFanGroup.enable || jackett.BigFanGroup.showdown)
                return Task.FromResult(false);

            return Joinparse(torrents, () => parsePage(host, query, cats));
        }
        #endregion


        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string id)
        {
            if (!jackett.BigFanGroup.enable)
                return Content("disable");

            var proxyManager = new ProxyManager("bigfangroup", jackett.BigFanGroup);

            var _t = await Http.Download($"{jackett.BigFanGroup.host}/download.php?id={id}", proxy: proxyManager.Get(), referer: jackett.BigFanGroup.host);
            if (_t != null && BencodeTo.Magnet(_t) != null)
                return File(_t, "application/x-bittorrent");

            return Content("error");
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query, string[] cats)
        {
            var torrents = new List<TorrentDetails>();
            var proxyManager = new ProxyManager("bigfangroup", jackett.BigFanGroup);

            #region Кеш html
            string html = await Http.Get($"{jackett.BigFanGroup.host}/browse.php?search=" + HttpUtility.UrlEncode(query), proxy: proxyManager.Get(), timeoutSeconds: jackett.timeoutSeconds);

            if (html == null || !html.Contains("id=\"searchinput\""))
            {
                consoleErrorLog("bigfangroup");
                return null;
            }
            #endregion

            var doc = new HtmlDocument();
            doc.LoadHtml(html.Replace("&nbsp;", " "));

            var nodes = doc.DocumentNode.SelectNodes("//tbody//tr");
            if (nodes == null || nodes.Count == 0)
                return null;

            foreach (var row in nodes)
            {
                var hc = new HtmlCommon(row);

                #region Данные раздачи
                string title = hc.NodeValue(".//a//b");

                DateTime createTime = tParse.ParseCreateTime(hc.NodeValue(".//img[@src='pic/time.png']", "title").Split(" в")[0], "dd.MM.yyyy");
                string viewtopic = hc.Match("href=\"details.php\\?id=([0-9]+)");
                string tracker = hc.Match("href=\"browse.php\\?cat=([0-9]+)");
                string sid = hc.NodeValue(".//font[@color='#000000']");
                string pir = hc.Match("todlers=[0-9]+\">([0-9]+)</a>");
                string sizeName = hc.NodeValue(".//td[contains(text(), 'GB') or contains(text(), 'MB')]");

                if (string.IsNullOrEmpty(viewtopic) || string.IsNullOrEmpty(tracker) || string.IsNullOrEmpty(title) || title.Contains(" | КПК"))
                    continue;
                #endregion

                #region types
                string[] types = null;
                switch (tracker)
                {
                    case "13":
                    case "52":
                    case "33":
                    case "48":
                    case "21":
                    case "39":
                    case "18":
                    case "24":
                    case "36":
                    case "53":
                    case "19":
                    case "31":
                    case "29":
                    case "27":
                    case "22":
                    case "26":
                    case "23":
                    case "30":
                        types = new string[] { "movie" };
                        break;
                    case "12":
                    case "20":
                    case "47":
                        types = new string[] { "multfilm" };
                        types = new string[] { "multserial" };
                        break;
                    case "11":
                        types = new string[] { "serial" };
                        break;
                    case "49":
                    case "32":
                    case "28":
                        types = new string[] { "docuserial", "documovie" };
                        break;
                    case "25":
                        types = new string[] { "tvshow" };
                        break;
                }

                if (cats != null)
                {
                    if (types == null)
                        continue;

                    bool isok = false;
                    foreach (string cat in cats)
                    {
                        if (types.Contains(cat))
                            isok = true;
                    }

                    if (!isok)
                        continue;
                }
                #endregion

                torrents.Add(new TorrentDetails()
                {
                    types = types,
                    url = $"{jackett.BigFanGroup.host}/forum/viewtopic.php?t={viewtopic}",
                    title = title,
                    sid = HtmlCommon.Integer(sid),
                    pir = HtmlCommon.Integer(pir),
                    sizeName = sizeName,
                    createTime = createTime,
                    parselink = $"{host}/bigfangroup/parsemagnet?id={viewtopic}"
                });
            }

            return torrents;
        }
        #endregion
    }
}
