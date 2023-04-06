using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.LITE;
using Lampac.Models.LITE.VideoCDN;

namespace Lampac.Controllers.LITE
{
    public class VideoCDN : BaseController
    {
        [HttpGet]
        [Route("lite/vcdn")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int t, int s = -1, int sid = -1)
        {
            if (!AppInit.conf.VCDN.enable)
                return Content(string.Empty);

            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return Content(string.Empty);

            #region Кеш запроса
            string memKey = $"videocdn:view:{imdb_id}:{kinopoisk_id}";

            if (!memoryCache.TryGetValue(memKey, out List<(string translation_id, string translation, string code)> cache))
            {
                string host = AppInit.conf.VCDN.apihost;
                if (AppInit.conf.VCDN.corseu)
                    host = $"{AppInit.corseuhost}/{host}";

                string args = kinopoisk_id > 0 ? $"kp_id={kinopoisk_id}&imdb_id={imdb_id}" : $"imdb_id={imdb_id}";
                string content = await HttpClient.Get($"{host}?{args}", referer: "https://kinogo.biz/53104-avatar-2-2022.html", MaxResponseContentBufferSize: 20_000_000, timeoutSeconds: 8, useproxy: AppInit.conf.VCDN.useproxy);
                if (content == null)
                    return Content(string.Empty);

                cache = new List<(string, string, string)>();

                if (content.Contains("</option>"))
                {
                    #region Несколько озвучек
                    var match = new Regex("<option +value=\"([0-9]+)\"[^>]+>([^<]+)</option>").Match(Regex.Replace(content, "[\n\r\t]+", ""));
                    while (match.Success)
                    {
                        string translation_id = match.Groups[1].Value;
                        string translation = match.Groups[2].Value.Trim();

                        if (!string.IsNullOrWhiteSpace(translation))
                        {
                            string code = Regex.Match(content, "\"" + translation_id + "\":\"([^\"]+)\"").Groups[1].Value;
                            if (string.IsNullOrWhiteSpace(code))
                            {
                                code = Regex.Match(content, "\"" + translation_id + "\":(\\[\\{[^\n\r]+\\}\\])(,|\\}'>)").Groups[1].Value;
                                code = Regex.Split(code, ",\"[0-9]+\"")[0];
                            }

                            code = code.Replace("&quot;", "\"").Replace("\\\"", "\"").Replace("\\\\\\", "\\").Replace("\\\\", "\\");

                            if (!string.IsNullOrWhiteSpace(code))
                                cache.Add((translation_id, translation, code));
                        }

                        match = match.NextMatch();
                    }
                    #endregion
                }
                else
                {
                    #region Одна озвучка
                    string code = Regex.Match(content, "id=\"files\" value='([^\n\r]+)'>").Groups[1].Value;
                    code = code.Replace("&quot;", "\"").Replace("\\\"", "\"").Replace("\\\\\\", "\\").Replace("\\\\", "\\");

                    string translation_id = null;
                    string translation = "По умолчанию";

                    if (!string.IsNullOrWhiteSpace(code))
                        cache.Add((translation_id, translation, code));
                    #endregion
                }

                if (cache.Count == 0)
                    return Content(string.Empty);

                memoryCache.Set(memKey, cache, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }
            #endregion

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            List<ApiModel> serialmedia = new List<ApiModel>();

            #region Playlists
            foreach (var voice in cache)
            {
                if (!voice.code.Contains("\"comment\":"))
                {
                    #region Фильм
                    string streansquality = string.Empty;
                    List<(string link, string quality)> streams = new List<(string, string)>();

                    foreach (var quality in new List<string> { "1080", "720", "480", "360" })
                    {
                        string link = new Regex($"//([^/]+/([^/:]+:[0-9]+/)?(movies|animes)/[^\n\r\t, ]+/{quality})").Match(voice.code.Replace("\\", "")).Groups[1].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        link = $"http://{link}.mp4";
                        link = HostStreamProxy(AppInit.conf.VCDN.streamproxy, link);

                        streams.Add((link, $"{quality}p"));
                        streansquality += $"\"{quality}p\":\"" + link + "\",";
                    }

                    //if (streams.Count > 0 && streams[0].quality == "720p")
                    //    streansquality = $"\"1080p\":\"" + streams[0].link.Replace("/720.mp4", "/1080.mp4") + "\"," + streansquality;

                    streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + (title ?? original_title ?? voice.translation) + "\", "+ streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + voice.translation?.Replace("Перевод", "По умолчанию") + "</div></div>";
                    firstjson = false;
                    #endregion
                }
                else
                {
                    #region Перевод
                    var md = new ApiModel()
                    {
                        title = voice.translation?.Replace("Перевод", "По умолчанию"),
                        type = "voice",
                        submenu = new List<ApiModel>()
                    };
                    #endregion

                    #region Сериал
                    List<Manifest> seasons;

                    #region Получаем json
                    try
                    {
                        //Console.WriteLine(voice.code);
                        //Console.WriteLine("\n\n");
                        if (voice.code.Contains("\"folder\":"))
                        {
                            seasons = JsonConvert.DeserializeObject<List<Manifest>>(voice.code);
                        }
                        else
                        {
                            seasons = new List<Manifest>()
                            {
                                new Manifest()
                                {
                                    comment = null,
                                    folder = JsonConvert.DeserializeObject<List<Folder>>(voice.code)
                                }
                            };
                        }

                        if (seasons == null || seasons.Count == 0)
                        {
                            continue;
                            //return OnError("Не удалось получить список сезонов", memKey: memKey);
                        }
                    }
                    catch
                    {
                        // return OnError("Неправильный формат json", memKey: memKey);
                        continue;
                    }
                    #endregion

                    #region getStreamLink
                    List<(string link, string quality)> getStreamLink(string _data)
                    {
                        var streams = new List<(string link, string quality)>();
                        foreach (var quality in new List<string> { "1080", "720", "480", "360", "240" })
                        {
                            string file = new Regex($"//([^/]+/([^/:]+:[0-9]+/)?(tvseries|animetvseries|showtvseries)/[^\n\r\t, ]+/{quality})").Match(_data).Groups[1].Value;
                            if (string.IsNullOrEmpty(file))
                                continue;

                            file = $"http://{file}.mp4";
                            file = HostStreamProxy(AppInit.conf.VCDN.streamproxy, file);

                            streams.Add((file, $"{quality}p"));
                        }

                        return streams;
                    }
                    #endregion

                    foreach (var season in seasons)
                    {
                        var mdSeason = new ApiModel()
                        {
                            title = HttpUtility.HtmlDecode(season.comment)?.Replace("<br>", " "),
                            type = "season",
                            submenu = new List<ApiModel>()
                        };

                        foreach (var serie in season.folder)
                        {
                            List<(string link, string quality)> streams = getStreamLink(serie.file);
                            if (streams.Count == 0)
                                continue;

                            mdSeason.submenu.Add(new ApiModel()
                            {
                                title = HttpUtility.HtmlDecode(serie.comment).Replace("<br>", " "),
                                stream_url = streams[0].link,
                                streams = streams,
                                type = "episode"
                            });
                        }

                        if (mdSeason.submenu.Count == 0)
                            continue;

                        if (season.comment != null)
                        {
                            md.submenu.Add(mdSeason);
                        }
                        else
                        {
                            md.submenu.AddRange(mdSeason.submenu);
                        }
                    }
                    #endregion

                    serialmedia.Add(md);
                }
            }
            #endregion

            #region serialmedia
            if (serialmedia.Count > 0)
            {
                #region Озвучки
                for (int i = 0; i < serialmedia.Count; i++)
                {
                    var voice = serialmedia[i];
                    string link = $"{host}/lite/vcdn?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={i}";
                    html += "<div class=\"videos__button selector " + (t == i ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + voice.title + "</div>";
                }

                html += "</div>";
                #endregion

                html += "<div class=\"videos__line\">";

                if (serialmedia[0].submenu[0].type == "season" && sid == -1)
                {
                    firstjson = true;
                    for (int i = 0; i < serialmedia[t].submenu.Count; i++)
                    {
                        var season = serialmedia[t].submenu[i];
                        string link = $"{host}/lite/vcdn?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={t}&s={Regex.Match(season.title, "^([0-9]+)").Groups[1].Value}&sid={i}";

                        html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + season.title + "</div></div></div>";
                        firstjson = false;
                    }
                }
                else
                {
                    firstjson = true;
                    foreach (var episode in (sid == -1 ? serialmedia[t].submenu : serialmedia[t].submenu[sid].submenu))
                    {
                        string sname = (title ?? original_title);
                        string episodetitle = Regex.Replace(episode.title, "<[^>]+>", "");
                        if (!string.IsNullOrWhiteSpace(episodetitle))
                            sname += $" ({episodetitle})";

                        string ename = Regex.Match(episode.title, "<i>([^<]+)</i>").Groups[1].Value;
                        string eid = Regex.Match(episode.title, "^([0-9]+)").Groups[1].Value;

                        if (s == -1)
                            s = 1;

                        #region streamsquality
                        string streamsquality = string.Empty;
                        foreach (var stream in episode.streams)
                            streamsquality += $"\"{stream.quality}\":\"" + stream.link + "\",";

                        if (episode.streams.Count > 0 && episode.streams[0].quality == "720p")
                            streamsquality = $"\"1080p\":\"" + episode.streams[0].link.Replace("/720.mp4", "/1080.mp4") + "\"," + streamsquality;

                        streamsquality = "\"quality\": {" + Regex.Replace(streamsquality, ",$", "") + "}";
                        #endregion

                        html += "<div class=\"videos__item videos__episode selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + eid + "\" data-json='{\"method\":\"play\",\"url\":\"" + episode.stream_url + "\",\"title\":\"" + sname + "\", " + streamsquality + "}'><div class=\"videos__item-imgbox videos__episode-imgbox\"><div class=\"videos__episode-number\">" + episode.title.Split("<")[0].Trim() + "</div></div><div class=\"videos__item-title videos__episode-title\">" + ename + "</div></div>";
                        firstjson = false;
                    }
                }
            }
            #endregion

            return Content(html + "</div>", "text/html; charset=utf-8");
        }
    }
}
