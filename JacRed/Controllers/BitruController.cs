using Microsoft.AspNetCore.Mvc;

namespace JacRed.Controllers
{
    [Route("bitru/[action]")]
    public class BitruController : JacBaseController
    {
        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query, string[] cats)
        {
            if (!jackett.Bitru.enable || jackett.Bitru.showdown)
                return Task.FromResult(false);

            return Joinparse(torrents, () => parsePage(host, query, cats));
        }
        #endregion


        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string id, bool usecache)
        {
            if (!jackett.Bitru.enable)
                return Content("disable");

            var proxyManager = new ProxyManager("bitru", jackett.Bitru);

            byte[] _t = await Http.Download($"{jackett.Bitru.host}/download.php?id={id}", referer: $"{jackett.Bitru}/details.php?id={id}", proxy: proxyManager.Get());
            if (_t != null && BencodeTo.Magnet(_t) != null)
                return File(_t, "application/x-bittorrent");

            proxyManager.Refresh();
            return Content("error");
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query, string[] cats)
        {
            #region html
            var proxyManager = new ProxyManager("bitru", jackett.Bitru);

            string html = await Http.Get($"{jackett.Bitru.host}/browse.php?s={HttpUtility.HtmlEncode(query)}&sort=&tmp=&cat=&subcat=&year=&country=&sound=&soundtrack=&subtitles=#content", proxy: proxyManager.Get(), timeoutSeconds: jackett.timeoutSeconds);

            if (html == null || !html.Contains("id=\"logo\""))
            {
                consoleErrorLog("bitru");
                proxyManager.Refresh();
                return null;
            }
            #endregion

            var torrents = new List<TorrentDetails>();

            foreach (string row in html.Split("<div class=\"b-title\"").Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row) || row.Contains(">Аниме</a>"))
                    continue;

                #region Локальный метод - Match
                string Match(string pattern, int index = 1)
                {
                    string res = HttpUtility.HtmlDecode(new Regex(pattern, RegexOptions.IgnoreCase).Match(row).Groups[index].Value.Trim());
                    res = Regex.Replace(res, "[\n\r\t ]+", " ");
                    return res.Trim();
                }
                #endregion

                #region Дата создания
                DateTime createTime = default;

                if (row.Contains("<span>Сегодня"))
                {
                    createTime = DateTime.Today;
                }
                else if (row.Contains("<span>Вчера"))
                {
                    createTime = DateTime.Today.AddDays(-1);
                }
                else
                {
                    createTime = tParse.ParseCreateTime(Match("<div class=\"ellips\">(<i [^>]+></i>)?<span>([0-9]{2} [^ ]+ [0-9]{4}) в [0-9]{2}:[0-9]{2} от <a", 2), "dd.MM.yyyy");
                }
                #endregion

                #region Данные раздачи
                string url = Match("href=\"(details.php\\?id=[0-9]+)\"");
                string newsid = Match("href=\"details.php\\?id=([0-9]+)\"");
                string cat = Match("<a href=\"browse.php\\?tmp=(movie|serial)&");

                string title = Match("<div class=\"it-title\">([^<]+)</div>");
                string _sid = Match("<span class=\"b-seeders\">([0-9]+)");
                string _pir = Match("<span class=\"b-leechers\">([0-9]+)");
                string sizeName = Match("title=\"Размер\">([^<]+)</td>");

                if (string.IsNullOrWhiteSpace(cat) || string.IsNullOrWhiteSpace(newsid) || string.IsNullOrWhiteSpace(title))
                    continue;

                if (!title.ToLower().Contains(query.ToLower()))
                    continue;
                #endregion

                #region types
                string[] types = null;
                switch (cat)
                {
                    case "movie":
                        types = new string[] { "movie" };
                        break;
                    case "serial":
                        types = new string[] { "serial" };
                        break;
                }

                if (cats != null)
                {
                    if (types == null)
                        continue;

                    bool isok = false;
                    foreach (string c in cats)
                    {
                        if (types.Contains(c))
                            isok = true;
                    }

                    if (!isok)
                        continue;
                }
                #endregion

                int.TryParse(_sid, out int sid);
                int.TryParse(_pir, out int pir);

                torrents.Add(new TorrentDetails()
                {
                    types = types,
                    url = $"{jackett.Bitru.host}/{url}",
                    title = title,
                    sid = sid,
                    pir = pir,
                    sizeName = sizeName,
                    createTime = createTime,
                    parselink = $"{host}/bitru/parsemagnet?id={newsid}"
                });
            }

            return torrents;
        }
        #endregion
    }
}
