using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Web;
using System.Linq;
using System.Collections.Generic;
using Lampac.Engine.CORE;
using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using Online;
using Shared.Model.Online.PiTor;
using Shared.Model.Templates;

namespace Lampac.Controllers.LITE
{
    public class PiTor : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/pidor")]
        async public Task<ActionResult> Index(string title, string original_title, int year, string qtype)
        {
            var init = AppInit.conf.PidTor;
            if (!init.enable)
                return OnError();

            #region Кеш запроса
            string memKey = $"pidor:{title}:{original_title}:{year}";
            if (!memoryCache.TryGetValue(memKey, out List<(string name, string voice, string magnet, int sid, string tr, string quality, string mediainfo)> torrents))
            {
                var root = await HttpClient.Get<RootObject>($"{init.redapi}/api/v2.0/indexers/all/results?title={HttpUtility.UrlEncode(title)}&title_original={HttpUtility.UrlEncode(original_title)}&year={year}&is_serial=1", timeoutSeconds: 8);
                if (root == null)
                    return Content(string.Empty, "text/html; charset=utf-8");

                torrents = new List<(string name, string voice, string magnet, int sid, string tr, string quality, string mediainfo)>();
                var results = root?.Results;
                if (results != null && results.Count > 0)
                {
                    foreach (var torrent in results)
                    {
                        string magnet = torrent.MagnetUri;
                        string name = torrent.Title;

                        if (string.IsNullOrWhiteSpace(magnet) || !magnet.Contains("&tr=") || string.IsNullOrWhiteSpace(name))
                            continue;

                        string tracker = torrent.Tracker;
                        if (tracker == "selezen")
                            continue;

                        if (Regex.IsMatch(name.ToLower(), "(4k|uhd)( |\\]|,|$)") || name.Contains("2160p") || name.Contains("1080p"))
                        {
                            int sid = torrent.Seeders;
                            long? size = torrent.Size;

                            if (sid >= init.min_sid)
                            {
                                string mediainfo = torrent.info?.sizeName ?? string.Empty;
                                string itemtitle = string.Empty;

                                #region Перевод
                                string voicename = string.Empty;

                                var voices = torrent.info?.voices;
                                if (voices != null && voices.Count > 0)
                                {
                                    itemtitle = string.Join(", ", voices);
                                    voicename = itemtitle;
                                }
                                #endregion

                                if (string.IsNullOrWhiteSpace(itemtitle))
                                    continue;

                                #region HDR / HEVC / Dolby Vision
                                if (Regex.IsMatch(name, "HDR10", RegexOptions.IgnoreCase) || Regex.IsMatch(name, "10-?bit", RegexOptions.IgnoreCase))
                                    mediainfo += " HDR10 ";
                                else if (Regex.IsMatch(name, "HDR", RegexOptions.IgnoreCase))
                                    mediainfo += " HDR";
                                else
                                {
                                    //itemtitle += "SDR ";
                                    continue;
                                }

                                if (Regex.IsMatch(name, "HEVC", RegexOptions.IgnoreCase) || Regex.IsMatch(name, "H.265", RegexOptions.IgnoreCase))
                                    mediainfo += " / H.265";

                                if (Regex.IsMatch(name, "Dolby Vision", RegexOptions.IgnoreCase))
                                {
                                    //itemtitle += " / Dolby Vision";
                                    continue;
                                }
                                #endregion

                                #region tr arg
                                string tr = string.Empty;
                                var match = Regex.Match(magnet, "(&|\\?)tr=([^&\\?]+)");
                                while (match.Success)
                                {
                                    string t = match.Groups[2].Value.Trim().ToLower();
                                    if (!string.IsNullOrWhiteSpace(t))
                                        tr += t.Contains("/") || t.Contains(":") ? $"&tr={HttpUtility.UrlEncode(t)}" : $"&tr={t}";

                                    match = match.NextMatch();
                                }

                                if (string.IsNullOrWhiteSpace(tr))
                                    continue;
                                #endregion

                                torrents.Add((itemtitle, voicename, magnet, sid, tr.Remove(0, 1), (name.Contains("2160p") ? "2160p" : "1080p"), mediainfo));
                            }
                        }
                    }
                }

                memoryCache.Set(memKey, torrents, DateTime.Now.AddMinutes(5));
            }

            if (torrents.Count == 0)
                return Content(string.Empty);
            #endregion

            var mtpl = new MovieTpl(title, original_title);

            foreach (var torrent in torrents.OrderByDescending(i => i.voice.Contains("Дубляж")).ThenByDescending(i => !string.IsNullOrWhiteSpace(i.voice)).ThenByDescending(i => i.sid))
            {
                if (!string.IsNullOrWhiteSpace(qtype) && !torrent.name.Contains(qtype))
                    continue;

                string hashmagnet = Regex.Match(torrent.magnet, "magnet:\\?xt=urn:btih:([a-zA-Z0-9]+)").Groups[1].Value.ToLower();
                if (string.IsNullOrWhiteSpace(hashmagnet))
                    continue;

                mtpl.Append(torrent.name, $"{host}/lite/pidor/s{hashmagnet}?{torrent.tr}", voice_name: torrent.quality + " / " +torrent.mediainfo, quality: torrent.quality.Replace("p", ""));
            }

            return Content(mtpl.ToHtml(), "text/html; charset=utf-8");
        }

        [HttpGet]
        [Route("lite/pidor/s{id}")]
        public ActionResult Stream(string id)
        {
            var init = AppInit.conf.PidTor;
            if (!init.enable)
                return OnError();

            string magnet = $"magnet:?xt=urn:btih:{id}&" + HttpContext.Request.QueryString.Value.Remove(0, 1);
            if (init.torrs == null || init.torrs.Length == 0)
                return Redirect($"{host}:8090/ts/stream?link={HttpUtility.UrlEncode(magnet)}&index=1&play");

            return Redirect($"{init.torrs[Random.Shared.Next(0, init.torrs.Length)]}/stream?link={HttpUtility.UrlEncode(magnet)}&index=1&play");
        }
    }
}
