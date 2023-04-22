using Lampac.Models.LITE.VideoCDN;
using Lampac.Models.LITE;
using System.Text.RegularExpressions;
using System.Web;
using System.Text.Json;
using Shared.Model.Online.VideoCDN;

namespace Shared.Engine.Online
{
    public class VideoCDNInvoke
    {
        #region VideoCDNInvoke
        string? host;
        string apihost;
        Func<string, string, ValueTask<string?>> onget;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public VideoCDNInvoke(string? host, string apihost, Func<string, string, ValueTask<string?>> onget, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
        }
        #endregion

        #region Embed
        public async ValueTask<List<EmbedModel>?> Embed(long kinopoisk_id, string? imdb_id)
        {
            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return null;

            string args = kinopoisk_id > 0 ? $"kp_id={kinopoisk_id}&imdb_id={imdb_id}" : $"imdb_id={imdb_id}";
            string? content = await onget.Invoke($"{apihost}?{args}", "https://kinogo.biz/53104-avatar-2-2022.html");
            if (content == null)
                return null;

            var cache = new List<EmbedModel>();

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
                            cache.Add(new EmbedModel() { translation_id = translation_id, translation = translation, code = code });
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

                string? translation_id = null;
                string translation = "По умолчанию";

                if (!string.IsNullOrWhiteSpace(code))
                    cache.Add(new EmbedModel() { translation_id = translation_id, translation = translation, code = code });
                #endregion
            }

            if (cache.Count == 0)
                return null;

            return cache;
        }
        #endregion

        #region Html
        public string Html(List<EmbedModel> cache, string? imdb_id, long kinopoisk_id, string? title, string? original_title, int t, int s, int sid)
        {
            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            var serialmedia = new List<ApiModel>();

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
                        link = onstreamfile.Invoke(link);

                        streams.Add((link, $"{quality}p"));
                        streansquality += $"\"{quality}p\":\"" + link + "\",";
                    }

                    //if (streams.Count > 0 && streams[0].quality == "720p")
                    //    streansquality = $"\"1080p\":\"" + streams[0].link.Replace("/720.mp4", "/1080.mp4") + "\"," + streansquality;

                    streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + (title ?? original_title ?? voice.translation) + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + voice.translation?.Replace("Перевод", "По умолчанию") + "</div></div>";
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
                    List<Manifest>? seasons;

                    #region Получаем json
                    try
                    {
                        //Console.WriteLine(voice.code);
                        //Console.WriteLine("\n\n");
                        if (voice.code.Contains("\"folder\":"))
                        {
                            seasons = JsonSerializer.Deserialize<List<Manifest>>(voice.code);
                        }
                        else
                        {
                            seasons = new List<Manifest>()
                            {
                                new Manifest()
                                {
                                    comment = null,
                                    folder = JsonSerializer.Deserialize<List<Folder>>(voice.code)
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
                            file = onstreamfile.Invoke(file);

                            streams.Add((file, $"{quality}p"));
                        }

                        return streams;
                    }
                    #endregion

                    foreach (var season in seasons)
                    {
                        if (season.folder == null)
                            continue;

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
                    string link = host + $"lite/vcdn?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={i}";
                    html += "<div class=\"videos__button selector " + (t == i ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + voice.title + "</div>";
                }

                html += "</div>";
                #endregion

                html += "<div class=\"videos__line\">";

                if (serialmedia?[0]?.submenu?[0].type == "season" && sid == -1)
                {
                    if (serialmedia.Count > t)
                    {
                        firstjson = true;
                        for (int i = 0; i < serialmedia[t]?.submenu?.Count; i++)
                        {
                            if (serialmedia[t]?.submenu?[i] is ApiModel season)
                            {
                                string link = host + $"lite/vcdn?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={t}&s={Regex.Match(season.title, "^([0-9]+)").Groups[1].Value}&sid={i}";

                                html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + season.title + "</div></div></div>";
                                firstjson = false;
                            }
                        }
                    }
                }
                else
                {
                    firstjson = true;
                    var episodes = sid == -1 ? serialmedia?[t].submenu : serialmedia?[t].submenu?[sid].submenu;

                    if (episodes != null)
                    {
                        foreach (var episode in episodes)
                        {
                            string? sname = title ?? original_title;
                            string episodetitle = Regex.Replace(episode.title ?? "", "<[^>]+>", "");
                            if (!string.IsNullOrWhiteSpace(episodetitle))
                                sname += $" ({episodetitle})";

                            string ename = Regex.Match(episode.title ?? "", "<i>([^<]+)</i>").Groups[1].Value;
                            string eid = Regex.Match(episode.title ?? "", "^([0-9]+)").Groups[1].Value;

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

                            html += "<div class=\"videos__item videos__episode selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + eid + "\" data-json='{\"method\":\"play\",\"url\":\"" + episode.stream_url + "\",\"title\":\"" + sname + "\", " + streamsquality + "}'><div class=\"videos__item-imgbox videos__episode-imgbox\"><div class=\"videos__episode-number\">" + episode?.title?.Split("<")[0].Trim() + "</div></div><div class=\"videos__item-title videos__episode-title\">" + ename + "</div></div>";
                            firstjson = false;
                        }
                    }
                }
            }
            #endregion

            return html + "</div>";
        }
        #endregion
    }
}
