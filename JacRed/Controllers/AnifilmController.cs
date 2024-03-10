using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Shared;
using Lampac.Engine.CORE;
using Lampac.Engine.Parse;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.CORE;
using JacRed.Engine;
using JacRed.Models;
using System.Collections.Generic;

namespace Lampac.Controllers.JAC
{
    [Route("anifilm/[action]")]
    public class AnifilmController : JacBaseController
    {
        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string url)
        {
            if (!jackett.Anifilm.enable)
                return Content("disable");

            string key = $"anifilm:parseMagnet:{url}";
            if (Startup.memoryCache.TryGetValue(key, out byte[] _t))
                return File(_t, "application/x-bittorrent");

            if (Startup.memoryCache.TryGetValue($"{key}:error", out _))
            {
                if (TorrentCache.Read(key) is var tc && tc.cache)
                    return File(tc.torrent, "application/x-bittorrent");

                return Content("error");
            }

            var proxyManager = new ProxyManager("anifilm", jackett.Anifilm);

            var fullNews = await HttpClient.Get(url, timeoutSeconds: 8, proxy: proxyManager.Get());
            if (fullNews == null)
                return Content("error");

            {
                string tid = null;
                string[] releasetorrents = fullNews.Split("<li class=\"release__torrents-item\">");

                string _rnews = releasetorrents.FirstOrDefault(i => i.Contains("href=\"/releases/download-torrent/") && i.Contains(" 1080p "));
                if (!string.IsNullOrWhiteSpace(_rnews))
                    tid = Regex.Match(_rnews, "href=\"/(releases/download-torrent/[0-9]+)\">скачать</a>").Groups[1].Value;

                if (string.IsNullOrWhiteSpace(tid))
                    tid = Regex.Match(fullNews, "href=\"/(releases/download-torrent/[0-9]+)\">скачать</a>").Groups[1].Value;

                if (!string.IsNullOrWhiteSpace(tid))
                {
                    _t = await HttpClient.Download($"{jackett.Anifilm.host}/{tid}", referer: $"{jackett.Anifilm.host}/", timeoutSeconds: 10, proxy: proxyManager.Get());
                    if (_t != null && BencodeTo.Magnet(_t) != null)
                    {
                        if (jackett.cache)
                        {
                            TorrentCache.Write(key, _t);
                            Startup.memoryCache.Set(key, _t, DateTime.Now.AddMinutes(Math.Max(1, jackett.torrentCacheToMinutes)));
                        }

                        return File(_t, "application/x-bittorrent");
                    }
                    else if (jackett.emptycache && jackett.cache)
                        Startup.memoryCache.Set($"{key}:error", 0, DateTime.Now.AddMinutes(1));
                }
            }

            if (TorrentCache.Read(key) is var tcache && tcache.cache)
                return File(tcache.torrent, "application/x-bittorrent");

            proxyManager.Refresh();
            return Content("error");
        }
        #endregion


        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query)
        {
            if (!jackett.Anifilm.enable)
                return Task.FromResult(false);

            return JackettCache.Invoke($"anifilm:{query}", torrents, () => parsePage(host, query));
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query)
        {
            var torrents = new List<TorrentDetails>();

            #region html
            var proxyManager = new ProxyManager("anifilm", jackett.Anifilm);

            string html = await HttpClient.Get($"{jackett.Anifilm.host}/releases?title={HttpUtility.UrlEncode(query)}", timeoutSeconds: jackett.timeoutSeconds, proxy: proxyManager.Get());

            if (html == null || !html.Contains("id=\"ui-components\""))
            {
                proxyManager.Refresh();
                return null;
            }
            #endregion

            foreach (string row in html.Split("class=\"releases__item\"").Skip(1))
            {
                #region Локальный метод - Match
                string Match(string pattern, int index = 1)
                {
                    string res = HttpUtility.HtmlDecode(new Regex(pattern, RegexOptions.IgnoreCase).Match(row).Groups[index].Value.Trim());
                    res = Regex.Replace(res, "[\n\r\t ]+", " ");
                    return res.Trim();
                }
                #endregion

                if (string.IsNullOrWhiteSpace(row))
                    continue;

                #region Данные раздачи
                string url = Match("<a href=\"/(releases/[^\"]+)\"");
                string name = Match("<a class=\"releases__title-russian\" [^>]+>([^<]+)</a>");
                string originalname = Match("<span class=\"releases__title-original\">([^<]+)</span>");
                string episodes = Match("([0-9]+(-[0-9]+)?) из [0-9]+ эп.,");

                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(originalname))
                    continue;

                if (!int.TryParse(Match("<a href=\"/releases/releases/[^\"]+\">([0-9]{4})</a> г\\."), out int relased) || relased == 0)
                    continue;

                url = $"{jackett.Anifilm.host}/{url}";
                string title = $"{name} / {originalname}";

                if (!string.IsNullOrWhiteSpace(episodes))
                    title += $" ({episodes})";
                #endregion

                torrents.Add(new TorrentDetails()
                {
                    trackerName = "anifilm",
                    types = new string[] { "anime" },
                    url = url,
                    title = title,
                    sid = 1,
                    parselink = $"{host}/anifilm/parsemagnet?url={HttpUtility.UrlEncode(url)}",
                    name = name,
                    originalname = originalname,
                    relased = relased
                });
            }

            return torrents;
        }
        #endregion
    }
}
