using Microsoft.AspNetCore.Mvc;

namespace JacRed.Controllers
{
    [Route("anifilm/[action]")]
    public class AnifilmController : JacBaseController
    {
        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query)
        {
            if (!jackett.Anifilm.enable || jackett.Anifilm.showdown)
                return Task.FromResult(false);

            return Joinparse(torrents, () => parsePage(host, query));
        }
        #endregion


        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string url)
        {
            if (!jackett.Anifilm.enable)
                return Content("disable");

            var proxyManager = new ProxyManager("anifilm", jackett.Anifilm);

            var fullNews = await Http.Get($"{jackett.Anifilm.host}/{url}", proxy: proxyManager.Get());
            if (fullNews == null)
                return Content("error");

            string tid = null;
            string[] releasetorrents = fullNews.Split("<li class=\"release__torrents-item\">");

            string _rnews = releasetorrents.FirstOrDefault(i => i.Contains("href=\"/releases/download-torrent/") && i.Contains(" 1080p "));
            if (!string.IsNullOrWhiteSpace(_rnews))
                tid = Regex.Match(_rnews, "href=\"/(releases/download-torrent/[0-9]+)\">скачать</a>").Groups[1].Value;

            if (string.IsNullOrWhiteSpace(tid))
                tid = Regex.Match(fullNews, "href=\"/(releases/download-torrent/[0-9]+)\">скачать</a>").Groups[1].Value;

            if (!string.IsNullOrWhiteSpace(tid))
            {
                var _t = await Http.Download($"{jackett.Anifilm.host}/{tid}", referer: $"{jackett.Anifilm.host}/", proxy: proxyManager.Get());
                if (_t != null && BencodeTo.Magnet(_t) != null)
                    return File(_t, "application/x-bittorrent");
            }

            proxyManager.Refresh();
            return Content("error");
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query)
        {
            #region html
            var proxyManager = new ProxyManager("anifilm", jackett.Anifilm);

            string html = await Http.Get($"{jackett.Anifilm.host}/releases?title={HttpUtility.UrlEncode(query)}", timeoutSeconds: jackett.timeoutSeconds, proxy: proxyManager.Get());

            if (html == null || !html.Contains("id=\"ui-components\""))
            {
                consoleErrorLog("anifilm");
                proxyManager.Refresh();
                return null;
            }
            #endregion

            var torrents = new List<TorrentDetails>();

            if (html.Contains("class=\"releases__item\""))
            {
                foreach (string row in html.Split("class=\"releases__item\"").Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(row))
                        continue;

                    #region Локальный метод - Match
                    string Match(string pattern, int index = 1)
                    {
                        string res = HttpUtility.HtmlDecode(new Regex(pattern, RegexOptions.IgnoreCase).Match(row).Groups[index].Value.Trim());
                        res = Regex.Replace(res, "[\n\r\t ]+", " ");
                        return res.Trim();
                    }
                    #endregion

                    #region Данные раздачи
                    string url = Match("<a href=\"/(releases/[^\"]+)\"");
                    string name = Match("<a class=\"releases__title-russian\" [^>]+>([^<]+)</a>");
                    string originalname = Match("<span class=\"releases__title-original\">([^<]+)</span>");
                    string episodes = Match("([0-9]+(-[0-9]+)?) из [0-9]+ эп.,");

                    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(originalname))
                        continue;

                    int.TryParse(Match("<a href=\"/releases/releases/[^\"]+\">([0-9]{4})</a> г\\."), out int relased);

                    string title = $"{name} / {originalname}";

                    if (!string.IsNullOrWhiteSpace(episodes))
                        title += $" ({episodes})";

                    var createTime = DateTime.Now.AddYears(-1);

                    if (relased > 0)
                    {
                        title += $" [{relased}]";
                        createTime = tParse.ParseCreateTime($"01.01.{relased}", "dd.MM.yyyy");
                    }
                    #endregion

                    torrents.Add(new TorrentDetails()
                    {
                        types = new string[] { "anime" },
                        url = $"{jackett.Anifilm.host}/{url}",
                        title = title,
                        sid = 1,
                        createTime = createTime,
                        parselink = $"{host}/anifilm/parsemagnet?url={HttpUtility.UrlEncode(url)}",
                        name = name,
                        originalname = originalname,
                        relased = relased
                    });
                }
            }
            else
            {
                string url = Regex.Match(html, "property=\"og:url\" content=\"https?://[^/]+/([^\"]+)\"").Groups[1].Value;
                string name = Regex.Match(html, "itemprop=\"name\">([^<]+)").Groups[1].Value;
                string alternative = Regex.Match(html, "itemprop=\"alternativeHeadline\">([^<]+)").Groups[1].Value;

                if (!string.IsNullOrEmpty(name))
                {
                    torrents.Add(new TorrentDetails()
                    {
                        types = new string[] { "anime" },
                        url = $"{jackett.Anifilm.host}/{url}",
                        title = name + (!string.IsNullOrEmpty(alternative) ? $" / {alternative}" : ""),
                        sid = 1,
                        parselink = $"{host}/anifilm/parsemagnet?url={HttpUtility.UrlEncode(url)}"
                    });
                }
            }

            return torrents;
        }
        #endregion
    }
}
