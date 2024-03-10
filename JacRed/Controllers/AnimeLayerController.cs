using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using JacRed.Engine;
using JacRed.Models;
using Lampac.Engine.CORE;
using Lampac.Engine.Parse;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared;

namespace Lampac.Controllers.JAC
{
    [Route("animelayer/[action]")]
    public class AnimeLayerController : JacBaseController
    {
        #region Cookie / TakeLogin
        static string Cookie { get; set; }

        async static Task<bool> TakeLogin()
        {
            string authKey = "animelayer:TakeLogin()";
            if (Startup.memoryCache.TryGetValue(authKey, out _))
                return false;

            Startup.memoryCache.Set(authKey, 0, AppInit.conf.multiaccess ? TimeSpan.FromMinutes(2) : TimeSpan.FromSeconds(20));

            try
            {
                var clientHandler = new System.Net.Http.HttpClientHandler()
                {
                    AllowAutoRedirect = false
                };

                clientHandler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
                using (var client = new System.Net.Http.HttpClient(clientHandler))
                {
                    client.Timeout = TimeSpan.FromSeconds(jackett.timeoutSeconds);
                    client.MaxResponseContentBufferSize = 2000000; // 2MB
                    client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/75.0.3770.100 Safari/537.36");

                    var postParams = new Dictionary<string, string>
                    {
                        { "login", jackett.Animelayer.login.u },
                        { "password", jackett.Animelayer.login.p }
                    };

                    using (var postContent = new System.Net.Http.FormUrlEncodedContent(postParams))
                    {
                        using (var response = await client.PostAsync($"{jackett.Animelayer.host}/auth/login/", postContent))
                        {
                            if (response.Headers.TryGetValues("Set-Cookie", out var cook))
                            {
                                string layer_id = null, layer_hash = null, PHPSESSID = null;
                                foreach (string line in cook)
                                {
                                    if (string.IsNullOrWhiteSpace(line))
                                        continue;

                                    if (line.Contains("layer_id="))
                                        layer_id = new Regex("layer_id=([^;]+)(;|$)").Match(line).Groups[1].Value;

                                    if (line.Contains("layer_hash="))
                                        layer_hash = new Regex("layer_hash=([^;]+)(;|$)").Match(line).Groups[1].Value;

                                    if (line.Contains("PHPSESSID="))
                                        PHPSESSID = new Regex("PHPSESSID=([^;]+)(;|$)").Match(line).Groups[1].Value;
                                }

                                if (!string.IsNullOrWhiteSpace(layer_id) && !string.IsNullOrWhiteSpace(layer_hash) && !string.IsNullOrWhiteSpace(PHPSESSID))
                                {
                                    Cookie = $"layer_id={layer_id}; layer_hash={layer_hash}; PHPSESSID={PHPSESSID};";
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return false;
        }
        #endregion

        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string url)
        {
            if (!jackett.Animelayer.enable)
                return Content("disable");

            string key = $"animelayer:parseMagnet:{url}";
            if (Startup.memoryCache.TryGetValue(key, out byte[] _m))
                return File(_m, "application/x-bittorrent");

            if (Startup.memoryCache.TryGetValue($"{key}:error", out _))
            {
                if (TorrentCache.Read(key) is var tc && tc.cache)
                    return File(tc.torrent, "application/x-bittorrent");

                return Content("error");
            }

            byte[] _t = await HttpClient.Download($"{url}download/", cookie: Cookie, referer: jackett.Animelayer.host, timeoutSeconds: 10);
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

            if (TorrentCache.Read(key) is var tcache && tcache.cache)
                return File(tcache.torrent, "application/x-bittorrent");

            return Content("error");
        }
        #endregion


        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query)
        {
            if (!jackett.Animelayer.enable)
                return Task.FromResult(false);

            return JackettCache.Invoke($"animelayer:{query}", torrents, () => parsePage(host, query));
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query)
        {
            var torrents = new List<TorrentDetails>();

            #region Авторизация
            if (Cookie == null)
            {
                if (await TakeLogin() == false)
                    return null;
            }
            #endregion

            #region html
            bool firstrehtml = true;
            rehtml: string html = await HttpClient.Get($"{jackett.Animelayer.host}/torrents/anime/?q={HttpUtility.UrlEncode(query)}", cookie: Cookie, timeoutSeconds: jackett.timeoutSeconds);

            if (html != null && html.Contains("id=\"wrapper\""))
            {
                if (!html.Contains($">{jackett.Animelayer.login.u}<"))
                {
                    if (!firstrehtml || await TakeLogin() == false)
                        return null;

                    firstrehtml = false;
                    goto rehtml;
                }
            }

            if (html == null)
                return null;
            #endregion

            foreach (string row in html.Split("class=\"torrent-item torrent-item-medium panel\"").Skip(1))
            {
                #region Локальный метод - Match
                string Match(string pattern, int index = 1)
                {
                    string res = new Regex(pattern, RegexOptions.IgnoreCase).Match(row).Groups[index].Value.Trim();
                    res = Regex.Replace(res, "[\n\r\t ]+", " ");
                    return res.Trim();
                }
                #endregion

                if (string.IsNullOrWhiteSpace(row))
                    continue;

                #region Дата создания
                DateTime createTime = default;

                if (Regex.IsMatch(row, "(Добавл|Обновл)[^<]+</span>[0-9]+ [^ ]+ [0-9]{4}"))
                {
                    createTime = tParse.ParseCreateTime(Match(">(Добавл|Обновл)[^<]+</span>([0-9]+ [^ ]+ [0-9]{4})", 2), "dd.MM.yyyy");
                }
                else
                {
                    string date = Match("(Добавл|Обновл)[^<]+</span>([^\n]+) в", 2);
                    if (string.IsNullOrWhiteSpace(date))
                        continue;

                    createTime = tParse.ParseCreateTime($"{date} {DateTime.Today.Year}", "dd.MM.yyyy");
                }

                //if (createTime == default)
                //    continue;
                #endregion

                #region Данные раздачи
                var gurl = Regex.Match(row, "<a href=\"/(torrent/[a-z0-9]+)/?\">([^<]+)</a>").Groups;

                string url = gurl[1].Value;
                string title = gurl[2].Value;

                string _sid = Match("class=\"icon s-icons-upload\"></i>([0-9]+)");
                string _pir = Match("class=\"icon s-icons-download\"></i>([0-9]+)");
                string sizeName = Match("<i class=\"icon s-icons-download\"></i>[^<]+<span class=\"gray\">[^<]+</span>[\n\r\t ]+([^\n\r<]+)").Trim();

                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(title))
                    continue;

                if (Regex.IsMatch(row, "Разрешение: ?</strong>1920x1080"))
                    title += " [1080p]";
                else if (Regex.IsMatch(row, "Разрешение: ?</strong>1280x720"))
                    title += " [720p]";

                url = $"{jackett.Animelayer.host}/{url}/";
                #endregion

                #region name / originalname
                string name = null, originalname = null;

                // Shaman king (2021) / Король-шаман [ТВ] (1-7)
                var g = Regex.Match(title, "([^/\\[\\(]+)\\([0-9]{4}\\)[^/]+/([^/\\[\\(]+)").Groups;
                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                {
                    name = g[2].Value.Trim();
                    originalname = g[1].Value.Trim();
                }
                else
                {
                    // Shadows House / Дом теней (1—6)
                    g = Regex.Match(title, "^([^/\\[\\(]+)/([^/\\[\\(]+)").Groups;
                    if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                    {
                        name = g[2].Value.Trim();
                        originalname = g[1].Value.Trim();
                    }
                }
                #endregion

                // Год выхода
                if (!int.TryParse(Match("Год выхода: ?</strong>([0-9]{4})"), out int relased) || relased == 0)
                    continue;

                if (string.IsNullOrWhiteSpace(name))
                    name = Regex.Split(title, "(\\[|\\/|\\(|\\|)", RegexOptions.IgnoreCase)[0].Trim();

                if (!string.IsNullOrWhiteSpace(name))
                {
                    int.TryParse(_sid, out int sid);
                    int.TryParse(_pir, out int pir);

                    torrents.Add(new TorrentDetails()
                    {
                        trackerName = "animelayer",
                        types = new string[] { "anime" },
                        url = url,
                        title = title,
                        sid = sid,
                        pir = pir,
                        sizeName = sizeName,
                        createTime = createTime,
                        parselink = $"{host}/animelayer/parsemagnet?url={HttpUtility.UrlEncode(url)}",
                        name = name,
                        originalname = originalname,
                        relased = relased
                    });
                }
            }

            return torrents;
        }
        #endregion
    }
}
