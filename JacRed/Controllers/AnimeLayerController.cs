﻿using JacRed.Engine;
using JacRed.Models;
using Lampac.Engine.CORE;
using Lampac.Engine.Parse;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared;
using Shared.Engine.CORE;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Lampac.Controllers.JAC
{
    [Route("animelayer/[action]")]
    public class AnimeLayerController : JacBaseController
    {
        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query)
        {
            if (!jackett.Animelayer.enable || string.IsNullOrEmpty(jackett.Animelayer.cookie ?? jackett.Animelayer.login.u) || jackett.Animelayer.showdown)
                return Task.FromResult(false);

            return Joinparse(torrents, () => parsePage(host, query));
        }
        #endregion


        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string url)
        {
            if (!jackett.Animelayer.enable)
                return Content("disable");

            string cookie = await getCookie();
            if (string.IsNullOrEmpty(cookie))
                return Content("cookie == null");

            var proxyManager = new ProxyManager("animelayer", jackett.Animelayer);

            byte[] _t = await HttpClient.Download($"{url}download/", proxy: proxyManager.Get(), cookie: cookie, referer: jackett.Animelayer.host);
            if (_t != null && BencodeTo.Magnet(_t) != null)
                return File(_t, "application/x-bittorrent");

            return Content("error");
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query)
        {
            #region Авторизация
            string cookie = await getCookie();
            if (string.IsNullOrEmpty(cookie))
            {
                consoleErrorLog("animelayer");
                return null;
            }
            #endregion

            var torrents = new List<TorrentDetails>();
            var proxyManager = new ProxyManager("animelayer", jackett.Animelayer);

            #region html
            string html = await HttpClient.Get($"{jackett.Animelayer.host}/torrents/anime/?q={HttpUtility.UrlEncode(query)}", proxy: proxyManager.Get(), cookie: cookie, timeoutSeconds: jackett.timeoutSeconds);

            if (html != null && html.Contains("id=\"wrapper\""))
            {
                if (!html.Contains($">{jackett.Animelayer.login.u}<"))
                {
                    consoleErrorLog("animelayer");
                    return null;
                }
            }
            else if (html == null)
            {
                consoleErrorLog("animelayer");
                return null;
            }
            #endregion

            foreach (string row in html.Split("class=\"torrent-item torrent-item-medium panel\"").Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row))
                    continue;

                #region Локальный метод - Match
                string Match(string pattern, int index = 1)
                {
                    string res = new Regex(pattern, RegexOptions.IgnoreCase).Match(row).Groups[index].Value.Trim();
                    res = Regex.Replace(res, "[\n\r\t ]+", " ");
                    return res.Trim();
                }
                #endregion

                #region Дата создания
                DateTime createTime = default;

                if (Regex.IsMatch(row, "(Добавл|Обновл)[^<]+</span>(&nbsp;)?[0-9]+ [^ ]+ [0-9]{4}"))
                {
                    createTime = tParse.ParseCreateTime(Match(">(Добавл|Обновл)[^<]+</span>(&nbsp;)?([0-9]+ [^ ]+ [0-9]{4})", 3), "dd.MM.yyyy");
                }
                else
                {
                    string date = Match("(Добавл|Обновл)[^<]+</span>([^\n]+) в", 2);
                    if (!string.IsNullOrWhiteSpace(date))
                        createTime = tParse.ParseCreateTime($"{date} {DateTime.Today.Year}", "dd.MM.yyyy");
                }
                #endregion

                #region Данные раздачи
                var gurl = Regex.Match(row, "<a href=\"/(torrent/[a-z0-9]+)/?\">([^<]+)</a>").Groups;

                string url = gurl[1].Value;
                string title = gurl[2].Value;

                string _sid = Match("class=\"icon s-icons-upload\"></i>(&nbsp;)?([0-9]+)", 2);
                string _pir = Match("class=\"icon s-icons-download\"></i>(&nbsp;)?([0-9]+)", 2);
                string sizeName = Match("<i class=\"icon s-icons-download\"></i>[^<]+<span class=\"gray\">[^<]+</span>[\n\r\t ]+([^\n\r<]+)").Trim();

                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(title))
                    continue;

                if (Regex.IsMatch(row, "Разрешение: ?</strong>1920x1080"))
                    title += " [1080p]";
                else if (Regex.IsMatch(row, "Разрешение: ?</strong>1280x720"))
                    title += " [720p]";
                #endregion

                int.TryParse(_sid, out int sid);
                int.TryParse(_pir, out int pir);

                torrents.Add(new TorrentDetails()
                {
                    types = new string[] { "anime" },
                    url = $"{jackett.Animelayer.host}/{url}/",
                    title = title,
                    sid = sid,
                    pir = pir,
                    sizeName = sizeName,
                    createTime = createTime,
                    parselink = $"{host}/animelayer/parsemagnet?url={HttpUtility.UrlEncode(url)}"
                });
            }

            return torrents;
        }
        #endregion


        #region getCookie
        async static ValueTask<string> getCookie()
        {
            if (!string.IsNullOrEmpty(jackett.Animelayer.cookie))
                return jackett.Animelayer.cookie;

            string authKey = "Animelayer:TakeLogin()";
            if (Startup.memoryCache.TryGetValue(authKey, out string _cookie))
                return _cookie;

            if (Startup.memoryCache.TryGetValue($"{authKey}:error", out _))
                return null;

            Startup.memoryCache.Set($"{authKey}:error", 0, TimeSpan.FromSeconds(20));

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
                                    string cookie = $"layer_id={layer_id}; layer_hash={layer_hash}; PHPSESSID={PHPSESSID};";
                                    Startup.memoryCache.Set(authKey, cookie, DateTime.Today.AddDays(1));
                                    return cookie;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }
        #endregion
    }
}
