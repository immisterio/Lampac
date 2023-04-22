using Lampac.Models.LITE.Collaps;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class CollapsInvoke
    {
        #region CollapsInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string> onstreamfile;

        public CollapsInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string> onstreamfile)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
        }
        #endregion

        #region Embed
        public async ValueTask<string?> Embed(string? imdb_id, long kinopoisk_id)
        {
            if(kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return null;

            string uri = $"{apihost}/embed/imdb/{imdb_id}";
            if (kinopoisk_id > 0)
                uri = $"{apihost}/embed/kp/{kinopoisk_id}";

            string? content = await onget.Invoke(uri);
            if (string.IsNullOrWhiteSpace(content))
                return null;

            return content;
        }
        #endregion

        #region Html
        public string Html(string content, string? imdb_id, long kinopoisk_id, string? title, string? original_title, int s)
        {
            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (!content.Contains("seasons:"))
            {
                #region Фильм
                string hls = Regex.Match(content, "hls: +\"(https?://[^\"]+\\.m3u8)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(hls))
                    return string.Empty;

                string name = Regex.Match(content, "audio: +\\{\"names\":\\[\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(name))
                    name = "По умолчанию";

                #region subtitle
                string subtitles = string.Empty;

                try
                {
                    var subs = JsonSerializer.Deserialize<List<Cc>>(Regex.Match(content, "cc: +(\\[[^\n\r]+\\]),").Groups[1].Value);
                    if (subs != null)
                    {
                        foreach (var cc in subs)
                        {
                            if (!string.IsNullOrWhiteSpace(cc.url))
                            {
                                string suburl = onstreamfile.Invoke(cc.url);
                                subtitles += "{\"label\": \"" + cc.name + "\",\"url\": \"" + suburl + "\"},";
                            }
                        }
                    }
                }
                catch { }

                subtitles = Regex.Replace(subtitles, ",$", "");
                #endregion

                string voicename = Regex.Match(content, "audio: +\\{\"names\":\\[\"([^\\]]+)\\]").Groups[1].Value;
                voicename = voicename.Replace("\"", "").Replace("delete", "").Replace(",", ", ");
                voicename = Regex.Replace(voicename, "[, ]+$", "");

                hls = onstreamfile.Invoke(hls);
                html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + hls + "\",\"title\":\"" + (title ?? original_title) + "\", \"subtitles\": [" + subtitles + "], \"voice_name\":\"" + voicename + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + name + "</div></div>";
                #endregion
            }
            else
            {
                #region Сериал
                try
                {
                    var root = JsonSerializer.Deserialize<List<RootObject>>(Regex.Match(content, "seasons:([^\n\r]+)").Groups[1].Value);
                    if (root == null)
                        return string.Empty;

                    if (s == 0)
                    {
                        foreach (var season in root.AsEnumerable().Reverse())
                        {
                            string link = host + $"lite/collaps?kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={season.season}";

                            html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season.season} сезон" + "</div></div></div>";
                            firstjson = false;
                        }
                    }
                    else
                    {
                        var episodes = root.First(i => i.season == s).episodes;
                        if (episodes == null)
                            return string.Empty;

                        foreach (var episode in episodes)
                        {
                            #region voicename
                            string voicename = string.Empty;

                            if (episode?.audio?.names != null)
                            {
                                voicename = string.Join(", ", episode.audio.names);
                                voicename = Regex.Replace(voicename, "[, ]+$", "");
                            }
                            #endregion

                            #region subtitle
                            string subtitles = string.Empty;

                            if (episode?.cc != null && episode.cc.Count > 0)
                            {
                                foreach (var cc in episode.cc)
                                {
                                    if (!string.IsNullOrEmpty(cc.url))
                                    {
                                        string suburl = onstreamfile.Invoke(cc.url);
                                        subtitles += "{\"label\": \"" + cc.name + "\",\"url\": \"" + suburl + "\"},";
                                    }
                                }
                            }

                            subtitles = Regex.Replace(subtitles, ",$", "");
                            #endregion

                            if (string.IsNullOrEmpty(episode?.hls) || string.IsNullOrEmpty(episode?.episode))
                                continue;

                            string file = onstreamfile.Invoke(episode.hls);
                            html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.episode + "\" data-json='{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + $"{title ?? original_title} ({episode.episode} серия)" + "\", \"subtitles\": [" + subtitles + "], \"voice_name\":\"" + voicename + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.episode} серия" + "</div></div>";
                            firstjson = false;
                        }
                    }
                }
                catch
                {
                    return string.Empty;
                }
                #endregion
            }

            return html + "</div>";
        }
        #endregion
    }
}
