using Shared.Engine.RxEnumerate;
using Shared.Models;
using Shared.Models.Base;
using Shared.Models.Online.Collaps;
using Shared.Models.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public struct CollapsInvoke
    {
        #region CollapsInvoke
        string host, route;
        string apihost;
        bool dash;
        Func<string, string> onstreamfile;
        Action requesterror;
        HttpHydra httpHydra;

        public CollapsInvoke(string host, string route, HttpHydra httpHydra, string apihost, bool dash, Func<string, string> onstreamfile, Action requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.route = route;
            this.apihost = apihost;
            this.dash = dash;
            this.onstreamfile = onstreamfile;
            this.requesterror = requesterror;
            this.httpHydra = httpHydra;
        }
        #endregion

        #region Embed
        async public Task<EmbedModel> Embed(string imdb_id, long kinopoisk_id, long orid)
        {
            string url = $"{apihost}/embed/imdb/{imdb_id}";

            if (kinopoisk_id > 0)
                url = $"{apihost}/embed/kp/{kinopoisk_id}";

            if (orid > 0)
                url = $"{apihost}/embed/movie/{orid}";

            EmbedModel embed = null;

            await httpHydra.GetSpan(url, content =>
            {
                if (!content.Contains("seasons:", StringComparison.Ordinal))
                {
                    var rx = Rx.Split("makePlayer\\(\\{", content);
                    if (1 > rx.Count)
                        return;

                    embed = new EmbedModel()
                    {
                        content = rx[1].ToString()
                    };
                }
                else
                {
                    try
                    {
                        var root = JsonSerializer.Deserialize<RootObject[]>(Rx.Match(content, "seasons:([^\n\r]+)"));
                        if (root != null && root.Length > 0)
                            embed = new EmbedModel() { serial = root };
                    }
                    catch { }
                }
            });

            if (embed == null)
                requesterror?.Invoke();

            return embed;
        }
        #endregion

        #region Tpl
        public ITplResult Tpl(EmbedModel md, string imdb_id, long kinopoisk_id, long orid, string title, string original_title, int s, bool rjson = false, List<HeadersModel> headers = null, VastConf vast = null)
        {
            if (md == null)
                return default;

            if (md.content != null)
            {
                #region Фильм
                string stream = Regex.Match(md.content, "hls: +\"(https?://[^\"]+\\.m3u[^\"]+)\"").Groups[1].Value;

                if (dash)
                {
                    string _dash = Regex.Match(md.content, "dasha?: +\"(https?://[^\"]+\\.mp[^\"]+)\"").Groups[1].Value;
                    if (!string.IsNullOrEmpty(_dash))
                        stream = _dash;
                }

                if (string.IsNullOrEmpty(stream))
                    return default;

                var mtpl = new MovieTpl(title, original_title, 1);

                string name = Regex.Match(md.content, "audio: +\\{\"names\":\\[\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(name))
                    name = "По умолчанию";

                #region subtitle
                SubtitleTpl? subtitles = null;

                try
                {
                    var subs = JsonSerializer.Deserialize<List<Cc>>(Regex.Match(md.content, "cc: +(\\[[^\n\r]+\\]),").Groups[1].Value);
                    if (subs != null)
                    {
                        subtitles = new SubtitleTpl(subs.Count);
                        foreach (var cc in subs)
                        {
                            if (cc.url != null)
                                subtitles.Value.Append(cc.name, onstreamfile.Invoke(cc.url));
                        }
                    }
                }
                catch { }
                #endregion

                string voicename = Regex.Match(md.content, "audio: +\\{\"names\":\\[\"([^\\]]+)\\]").Groups[1].Value;
                voicename = voicename.Replace("\"", "").Replace("delete", "").Replace(",", ", ");
                voicename = Regex.Replace(voicename, "[, ]+$", "");

                mtpl.Append(name, onstreamfile.Invoke(stream.Replace("\u0026", "&")), subtitles: subtitles, voice_name: voicename, headers: headers, vast: vast);

                return mtpl;
                #endregion
            }
            else
            {
                #region Сериал
                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);

                try
                {
                    if (s == -1)
                    {
                        var tpl = new SeasonTpl(md.serial.Length);

                        foreach (var season in md.serial.OrderBy(i => i.season))
                        {
                            string link = host + $"{route}?rjson={rjson}&kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&orid={orid}&title={enc_title}&original_title={enc_original_title}&s={season.season}";
                            tpl.Append($"{season.season} сезон", link, season.season);
                        }

                        return tpl;
                    }
                    else
                    {
                        var episodes = md.serial.FirstOrDefault(i => i.season == s).episodes;
                        if (episodes == null)
                            return default;

                        var etpl = new EpisodeTpl(episodes.Length);
                        string sArch = s.ToString();

                        foreach (var episode in episodes)
                        {
                            string stream = episode.hls ?? episode.dasha ?? episode.dash;
                            if (dash && (episode.dasha ?? episode.dash) != null)
                                stream = episode.dasha ?? episode.dash;

                            if (string.IsNullOrEmpty(stream) || string.IsNullOrEmpty(episode.episode))
                                continue;

                            #region voicename
                            string voicename = string.Empty;

                            if (episode.audio.names != null)
                                voicename = Regex.Replace(string.Join(", ", episode.audio.names), "[, ]+$", "");
                            #endregion

                            #region subtitle
                            var subtitles = new SubtitleTpl(episode.cc?.Length ?? 0);

                            if (episode.cc != null && episode.cc.Length > 0)
                            {
                                foreach (var cc in episode.cc)
                                {
                                    if (cc.url != null)
                                        subtitles.Append(cc.name, onstreamfile.Invoke(cc.url));
                                }
                            }
                            #endregion

                            string file = onstreamfile.Invoke(stream.Replace("\u0026", "&"));
                            etpl.Append($"{episode.episode} серия", title ?? original_title, sArch, episode.episode, file, subtitles: subtitles, voice_name: voicename, headers: headers, vast: vast);
                        }

                        return etpl;
                    }
                }
                catch
                {
                    return default;
                }
                #endregion
            }
        }
        #endregion
    }
}
