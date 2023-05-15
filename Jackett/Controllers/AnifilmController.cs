using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Shared;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Engine.Parse;
using Lampac.Models.JAC;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.CORE;

namespace Lampac.Controllers.CRON
{
    [Route("anifilm/[action]")]
    public class AnifilmController : BaseController
    {
        #region parseMagnet
        static string TorrentFileMemKey(string tid) => $"anifilm:parseMagnet:{tid}";

        async public Task<ActionResult> parseMagnet(string tid, bool usecache)
        {
            if (!AppInit.conf.Anifilm.enable)
                return Content("disable");

            string key = TorrentFileMemKey(tid);
            if (Startup.memoryCache.TryGetValue(key, out byte[] _t))
                return File(_t, "application/x-bittorrent");

            if (usecache || Startup.memoryCache.TryGetValue($"{key}:error", out _))
            {
                if (await TorrentCache.Read(key) is var tc && tc.cache)
                    return File(tc.torrent, "application/x-bittorrent");

                return Content("error");
            }

            var proxyManager = new ProxyManager("anifilm", AppInit.conf.Anifilm);

            _t = await HttpClient.Download($"{AppInit.conf.Anifilm.host}/{tid}", referer: $"{AppInit.conf.Anifilm.host}/", timeoutSeconds: 10, proxy: proxyManager.Get());
            if (_t != null && BencodeTo.Magnet(_t) != null)
            {
                await TorrentCache.Write(key, _t);
                Startup.memoryCache.Set(key, _t, DateTime.Now.AddMinutes(Math.Max(1, AppInit.conf.jac.torrentCacheToMinutes)));
                return File(_t, "application/x-bittorrent");
            }
            else if (AppInit.conf.jac.emptycache)
                Startup.memoryCache.Set($"{key}:error", 0, DateTime.Now.AddMinutes(Math.Max(1, AppInit.conf.jac.torrentCacheToMinutes)));

            if (await TorrentCache.Read(key) is var tcache && tcache.cache)
                return File(tcache.torrent, "application/x-bittorrent");

            proxyManager.Refresh();
            return Content("error");
        }
        #endregion

        #region parsePage
        async public static Task<bool> parsePage(string host, ConcurrentBag<TorrentDetails> torrents, string query)
        {
            string memkey = $"anifilm:{query}";

            if (!AppInit.conf.Anifilm.enable || Startup.memoryCache.TryGetValue($"{memkey}:error", out _))
                return false;

            #region Кеш html
            string cachekey = $"anifilm:{query}";
            var cread = await HtmlCache.Read(cachekey);
            bool validrq = cread.cache;

            if (cread.emptycache)
                return false;

            if (!cread.cache)
            {
                var proxyManager = new ProxyManager("anifilm", AppInit.conf.Anifilm);

                string html = await HttpClient.Get($"{AppInit.conf.Anifilm.host}/releases?title={HttpUtility.UrlEncode(query)}", timeoutSeconds: AppInit.conf.jac.timeoutSeconds, proxy: proxyManager.Get());

                if (html != null && html.Contains("id=\"ui-components\""))
                {
                    cread.html = html;
                    await HtmlCache.Write(cachekey, html);
                    validrq = true;
                }

                if (cread.html == null)
                {
                    proxyManager.Refresh();
                    HtmlCache.EmptyCache(cachekey);
                    return false;
                }
            }
            #endregion

            #region Локальный метод - Match
            string Match(string pattern, int index = 1)
            {
                string res = HttpUtility.HtmlDecode(new Regex(pattern, RegexOptions.IgnoreCase).Match(cread.html).Groups[index].Value.Trim());
                res = Regex.Replace(res, "[\n\r\t ]+", " ");
                return res.Trim();
            }
            #endregion

            #region Данные раздачи
            string url = Match("<a href=\"/(releases/[^\"]+)\"");
            string name = Match("class=\"release__title-primary\" itemprop=\"name\">([^<]+)</h1>");
            string originalname = Match("itemprop=\"alternativeHeadline\">([^<]+)</span>");
            string episodes = Match("([0-9]+(-[0-9]+)?) из [0-9]+ эп.,");

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(originalname))
                return false;

            if (!int.TryParse(Match("<a href=\"/releases/releases/[^\"]+\">([0-9]{4})</a> г\\."), out int relased) || relased == 0)
                return false;

            url = $"{AppInit.conf.Anifilm.host}/{url}";
            string title = $"{name} / {originalname}";

            if (!string.IsNullOrWhiteSpace(episodes))
                title += $" ({episodes})";
            #endregion

            string tid = null;
            string[] releasetorrents = cread.html.Split("<li class=\"release__torrents-item\">");

            string _rnews = releasetorrents.FirstOrDefault(i => i.Contains("href=\"/releases/download-torrent/") && i.Contains(" 1080p "));
            if (!string.IsNullOrWhiteSpace(_rnews))
            {
                tid = Regex.Match(_rnews, "href=\"/(releases/download-torrent/[0-9]+)\">скачать</a>").Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(tid))
                    title += " [1080p]";
            }

            if (string.IsNullOrWhiteSpace(tid))
            {
                tid = Regex.Match(cread.html, "href=\"/(releases/download-torrent/[0-9]+)\">скачать</a>").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(tid))
                    return false;
            }

            if (!validrq && !TorrentCache.Exists(TorrentFileMemKey(tid)))
                return false;

            torrents.Add(new TorrentDetails()
            {
                trackerName = "anifilm",
                types = new string[] { "anime" },
                url = url,
                title = title,
                sid = 1,
                parselink = $"{host}/anifilm/parsemagnet?tid={HttpUtility.UrlEncode(tid)}" + (!validrq ? "&usecache=true" : ""),
                name = name,
                originalname = originalname,
                relased = relased
            });

            return true;
        }
        #endregion
    }
}
