using Shared.Model.Base;
using Shared.Model.Online;
using Shared.Model.Online.Zetflix;
using Shared.Model.Templates;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class ZetflixInvoke
    {
        #region ZetflixInvoke
        string? host, apihost;
        bool usehls;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;
        Func<string, List<HeadersModel>?, ValueTask<string?>> onget;

        public ZetflixInvoke(in string? host, in string? apihost, in bool hls, Func<string, List<HeadersModel>?, ValueTask<string?>> onget, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.onget = onget;
            usehls = hls;
        }
        #endregion

        #region Embed
        public async ValueTask<EmbedModel?> Embed(long kinopoisk_id, int s)
        {
            string? html = await onget.Invoke($"{apihost}/iplayer/videodb.php?kp={kinopoisk_id}" + (s > 0 ? $"&season={s}" : ""), HeadersModel.Init(
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("referer", "https://www.google.com/"),
                ("upgrade-insecure-requests", "1")
            ));

            return Embed(html);
        }

        public EmbedModel? Embed(in string? html)
        {
            onlog?.Invoke(html ?? "html null");

            if (html == null)
                return null;

            string quality = html.Contains("1080p") ? "1080p" : html.Contains("720p") ? "720p" : "480p";
            string check_url = Regex.Match(html, "(https?://[^\\[\\|,\n\r\t ]+\\.mp4)").Groups[1].Value;

            string? file = Regex.Match(html, "file:(\\[[^\n\r]+\\])(,|}\\) ;)").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(file))
            {
                file = Regex.Match(html, "file:\"([^\"]+)\"").Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(file))
                    return new EmbedModel() { pl = new List<RootObject>() { new RootObject() { file = file, title = "Дубляж" } }, movie = true, quality = quality, check_url = check_url };

                return null;
            }

            file = Regex.Replace(file.Trim(), "(\\{|, )([a-z]+): ?", "$1\"$2\":")
                        .Replace("},]", "}]");

            List<RootObject>? pl = null;

            try
            {
                pl = JsonSerializer.Deserialize<List<RootObject>>(file);
                if (pl == null || pl.Count == 0)
                    return null;
            }
            catch { return null; }

            return new EmbedModel() { pl = pl, movie = !file.Contains("\"comment\":"), quality = quality, check_url = check_url };
        }
        #endregion

        #region number_of_seasons
        public async ValueTask<int> number_of_seasons(long id)
        {
            int number_of_seasons = 1;
            string? themoviedb = await onget.Invoke($"https://tmdb.mirror-kurwa.men/3/tv/{id}?api_key=4ef0d7355d9ffb5151e987764708ce96", null);

            if (themoviedb != null)
            {
                try
                {
                    var root = JsonSerializer.Deserialize<JsonObject>(themoviedb);
                    number_of_seasons = root["number_of_seasons"].GetValue<int>();
                    if (1 > number_of_seasons)
                        number_of_seasons = 1;
                }
                catch { }
            }

            if (0 >= number_of_seasons)
                number_of_seasons = 1;

            return number_of_seasons;
        }
        #endregion

        #region Html
        public string Html(EmbedModel? root, in int number_of_seasons, in long kinopoisk_id, in string? title, in string? original_title, string? t, in int s, in bool isbwa = false, in bool rjson = false, VastConf? vast = null)
        {
            if (root?.pl == null || root.pl.Count == 0)
                return string.Empty;

            if (root.movie)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, root.pl.Count);

                foreach (var pl in root.pl)
                {
                    string? name = pl.title;
                    string? file = pl.file;

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                        continue;

                    var streams = new List<(string link, string quality)>(4);

                    foreach (Match m in Regex.Matches(file, $"\\[(1080|720|480|360)p?\\]([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))"))
                    {
                        string link = m.Groups[2].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        if (usehls && !link.Contains(".m3u"))
                            link += ":hls:manifest.m3u8";
                        else if (!usehls && link.Contains(".m3u"))
                            link = link.Replace(":hls:manifest.m3u8", "");

                        if (isbwa)
                            link = Regex.Replace(link, "/([0-9]+)\\.(m3u8|mp4)", $"/{m.Groups[1].Value}.$2");

                        streams.Add((onstreamfile.Invoke(link), $"{m.Groups[1].Value}p"));
                    }

                    if (streams.Count == 0)
                        continue;

                    streams.Reverse();

                    mtpl.Append(name, streams[0].link, streamquality: new StreamQualityTpl(streams), vast: vast);
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                string? enc_title = HttpUtility.UrlEncode(title);
                string? enc_original_title = HttpUtility.UrlEncode(original_title);

                if (s == -1)
                {
                    var tpl = new SeasonTpl(root.quality);

                    for (int i = 1; i <= number_of_seasons; i++)
                    {
                        string link = host + $"lite/zetflix?rjson={rjson}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&s={i}";
                        tpl.Append($"{i} сезон", link, i);
                    }

                    return rjson ? tpl.ToJson() : tpl.ToHtml();
                }
                else
                {
                    var vtpl = new VoiceTpl();
                    var etpl = new EpisodeTpl();
                    var hashvoices = new HashSet<string>();

                    string sArhc = s.ToString();

                    foreach (var episode in root.pl.AsEnumerable().Reverse())
                    {
                        var episodes = episode?.folder;
                        if (episodes == null || episodes.Count == 0)
                            continue;

                        string? perevod = episode?.title;
                        if (perevod != null && string.IsNullOrEmpty(t))
                            t = perevod;

                        #region Переводы
                        if (perevod != null && !hashvoices.Contains(perevod))
                        {
                            hashvoices.Add(perevod);
                            string link = host + $"lite/zetflix?rjson={rjson}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&s={s}&t={HttpUtility.UrlEncode(perevod)}";

                            vtpl.Append(perevod, t == perevod, link);
                        }
                        #endregion

                        if (perevod != t)
                            continue;

                        foreach (var pl in episodes)
                        {
                            string? name = pl?.comment;
                            string? file = pl?.file;

                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                                continue;

                            var streams = new List<(string link, string quality)>(4);

                            foreach (Match m in Regex.Matches(file, $"\\[(1080|720|480|360)p?\\]([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))"))
                            {
                                string link = m.Groups[2].Value;
                                if (string.IsNullOrEmpty(link))
                                    continue;

                                if (usehls && !link.Contains(".m3u"))
                                    link += ":hls:manifest.m3u8";
                                else if (!usehls && link.Contains(".m3u"))
                                    link = link.Replace(":hls:manifest.m3u8", "");

                                if (isbwa)
                                    link = Regex.Replace(link, "/([0-9]+)\\.(m3u8|mp4)", $"/{m.Groups[1].Value}.$2");

                                streams.Add((onstreamfile.Invoke(link), $"{m.Groups[1].Value}p"));
                            }

                            if (streams.Count == 0)
                                continue;

                            streams.Reverse();

                            etpl.Append(name, title ?? original_title, sArhc, Regex.Match(name, "^([0-9]+)").Groups[1].Value, streams[0].link, streamquality: new StreamQualityTpl(streams), vast: vast);
                        }
                    }

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
