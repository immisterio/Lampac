using Shared.Model.Base;
using Shared.Model.Online.FanCDN;
using Shared.Model.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class FanCDNInvoke
    {
        #region FanCDNInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public FanCDNInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null; this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
        }
        #endregion

        #region Embed
        async public ValueTask<EmbedModel?> Embed(string imdb_id, long kinopoisk_id, string title, string original_title, int year, bool searchsite = false)
        {
            var episodes = await Embed(null, imdb_id, kinopoisk_id);
            if (episodes != null)
                return episodes;

            if (string.IsNullOrEmpty(title) || year == 0 || !searchsite)
                return null;

            string? search = await onget($"{apihost}/?do=search&subaction=search&story={HttpUtility.UrlEncode(title)}");
            if (string.IsNullOrEmpty(search))
                return null;

            string? href = null;

            foreach (string itemsearch in search.Split("item-search-serial"))
            {
                string? info = itemsearch.Split("torrent-link")?[0];
                if (!string.IsNullOrEmpty(info) && 
                    (info.Contains($"({year-1}") || info.Contains($"({year}") || info.Contains($"({year+1}")) && 
                    (info.Contains(title) || info.Contains(original_title)))
                {
                    href = Regex.Match(info, "<a href=\"(https?://[^\"]+\\.html)\"").Groups[1].Value;
                    break;
                }
            }

            if (string.IsNullOrEmpty(href))
                return null;

            string? html = await onget(href);
            if (string.IsNullOrEmpty(html))
                return null;

            string iframe_url = Regex.Match(html, "id=\"iframe-player\" src=\"([^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrEmpty(iframe_url))
                return null;

            return await Embed(iframe_url, null, 0);
        }


        async public ValueTask<EmbedModel?> Embed(string? iframe_url, string imdb_id, long kinopoisk_id)
        {
            if (string.IsNullOrEmpty(iframe_url) && string.IsNullOrEmpty(imdb_id) && kinopoisk_id == 0)
                return null;

            if (string.IsNullOrEmpty(iframe_url))
            {
                iframe_url = "https://fancdn.net/iframe/";

                if (kinopoisk_id > 0)
                    iframe_url += $"?kinopoisk={kinopoisk_id}";

                if (!string.IsNullOrEmpty(imdb_id))
                    iframe_url += (iframe_url.Contains("?") ? "&" : "?") + $"imdb_id={imdb_id}";
            }

            string? iframe = await onget(iframe_url);
            if (string.IsNullOrEmpty(iframe))
                return null;

            iframe = Regex.Replace(iframe, "[\n\r\t]+", "").Replace("var ", "\n");

            string playlist = Regex.Match(iframe, "playlist ?= ?(\\[[^\n\r]+\\]);").Groups[1].Value;
            if (string.IsNullOrEmpty(playlist))
                return null;

            try
            {
                if (iframe.Contains("\"folder\""))
                {
                    var serial = JsonSerializer.Deserialize<List<Voice>>(playlist);
                    if (serial == null || serial.Count == 0)
                        return null;

                    return new EmbedModel() { serial = serial };
                }
                else
                {
                    var movies = JsonSerializer.Deserialize<List<Episode>>(playlist);
                    if (movies == null || movies.Count == 0)
                        return null;

                    return new EmbedModel() { movies = movies };
                }
            }
            catch { }

            return null;
        }
        #endregion

        #region Html
        public string Html(EmbedModel? root, string imdb_id, long kinopoisk_id, string? title, string? original_title, int t = -1, int s = -1, bool rjson = false, VastConf? vast = null)
        {
            if (root == null)
                return string.Empty;

            if (root.movies != null)
            {
                var mtpl = new MovieTpl(title, original_title, root.movies.Count);

                foreach (var m in root.movies)
                {
                    if (string.IsNullOrEmpty(m.file))
                        continue;

                    #region subtitle
                    var subtitles = new SubtitleTpl();

                    if (!string.IsNullOrEmpty(m.subtitles))
                    {
                        // [rus]rus1.srt,[eng]eng2.srt,[eng]eng3.srt
                        var match = new Regex("\\[([^\\]]+)\\]([^\\,]+)").Match(m.subtitles);
                        while (match.Success)
                        {
                            string srt = m.file.Replace("/hls.m3u8", "/") + match.Groups[2].Value;
                            subtitles.Append(match.Groups[1].Value, onstreamfile.Invoke(srt));
                            match = match.NextMatch();
                        }
                    }
                    #endregion

                    mtpl.Append(m.title, onstreamfile.Invoke(m.file), subtitles: subtitles, vast: vast);
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
            }
            else
            {
                #region Сериал
                string? enc_title = HttpUtility.UrlEncode(title);
                string? enc_original_title = HttpUtility.UrlEncode(original_title);

                if (s == -1)
                {
                    #region Сезоны
                    var tpl = new SeasonTpl();
                    var hash = new HashSet<int>();

                    foreach (var voice in root.serial)
                    {
                        if (hash.Contains(voice.seasons))
                            continue;

                        hash.Add(voice.seasons);

                        string link = host + $"lite/fancdn?rjson={rjson}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&s={voice.seasons}";
                        tpl.Append($"{voice.seasons} сезон", link, voice.seasons);
                    }

                    return rjson ? tpl.ToJson() : tpl.ToHtml();
                    #endregion
                }
                else
                {
                    #region Перевод
                    var vtpl = new VoiceTpl();

                    foreach (var voice in root.serial)
                    {
                        if (s > voice.seasons)
                            continue;

                        if (t == -1)
                            t = voice.id;

                        string link = host + $"lite/fancdn?rjson={rjson}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&s={s}&t={voice.id}";
                        bool active = t == voice.id;

                        vtpl.Append(voice.title, active, link);
                    }
                    #endregion

                    var etpl = new EpisodeTpl();

                    foreach (var episode in root.serial.First(i => i.id == t).folder[s.ToString()].folder)
                        etpl.Append($"{episode.Key} серия", title ?? original_title, s.ToString(), episode.Key, onstreamfile.Invoke(episode.Value.file), vast: vast);

                    if (rjson)
                        return etpl.ToJson(vtpl);

                    return vtpl.ToHtml() + etpl.ToHtml();
                }
                #endregion
            }
        }
        #endregion
    }
}
