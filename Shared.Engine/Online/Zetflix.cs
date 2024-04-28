using Shared.Model.Online;
using Shared.Model.Online.Zetflix;
using Shared.Model.Templates;
using System.Text;
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

        public ZetflixInvoke(string? host, string? apihost, bool hls, Func<string, List<HeadersModel>?, ValueTask<string?>> onget, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
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

        public EmbedModel? Embed(string? html)
        {
            onlog?.Invoke(html ?? "html null");

            if (html == null)
                return null;

            string quality = html.Contains("1080p") ? "1080p" : html.Contains("720p") ? "720p" : "480p";
            string check_url = Regex.Match(html, "(https?://[^\\[\\|,\n\r\t ]+\\.mp4)").Groups[1].Value;

            string? file = Regex.Match(html, "file:(\\[[^\n\r]+\\]),").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(file))
            {
                file = Regex.Match(html, "file:\"([^\"]+)\"").Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(file))
                    return new EmbedModel() { pl = new List<RootObject>() { new RootObject() { file = file, title = "Дубляж" } }, movie = true, quality = quality, check_url = check_url };

                return null;
            }

            file = Regex.Replace(file.Trim(), "(\\{|, )([a-z]+): ?", "$1\"$2\":")
                        .Replace("},]", "}]");

            var pl = JsonSerializer.Deserialize<List<RootObject>>(file);
            if (pl == null || pl.Count == 0)
                return null;

            return new EmbedModel() { pl = pl, movie = !file.Contains("\"comment\":"), quality = quality, check_url = check_url };
        }
        #endregion

        #region number_of_seasons
        public async ValueTask<int> number_of_seasons(long id)
        {
            int number_of_seasons = 1;

            var themoviedb = await onget.Invoke($"https://api.themoviedb.org/3/tv/{id}?api_key=4ef0d7355d9ffb5151e987764708ce96", null);
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
        public string Html(EmbedModel? root, int number_of_seasons, long kinopoisk_id, string? title, string? original_title, string? t, int s)
        {
            if (root?.pl == null || root.pl.Count == 0)
                return string.Empty;

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

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

                    var streams = new List<(string link, string quality)>() { Capacity = 4 };

                    foreach (Match m in Regex.Matches(file, $"\\[(1080|720|480|360)p?\\]([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))"))
                    {
                        string link = m.Groups[2].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        if (usehls && !link.Contains(".m3u"))
                            link += ":hls:manifest.m3u8";
                        else if (!usehls && link.Contains(".m3u"))
                            link = link.Replace(":hls:manifest.m3u8", "");

                        streams.Insert(0, (onstreamfile.Invoke(link), $"{m.Groups[1].Value}p"));
                    }

                    if (streams.Count == 0)
                        continue;

                    mtpl.Append(name, streams[0].link, streamquality: new StreamQualityTpl(streams));
                }

                return mtpl.ToHtml();
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
                        string link = host + $"lite/zetflix?kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&s={i}";
                        tpl.Append($"{i} сезон", link);
                    }

                    return tpl.ToHtml();
                }
                else
                {
                    var hashvoices = new HashSet<string>();
                    var htmlvoices = new StringBuilder();
                    var htmlepisodes = new StringBuilder();

                    foreach (var episode in root.pl.AsEnumerable().Reverse())
                    {
                        var episodes = episode?.folder;
                        if (episodes == null || episodes.Count == 0)
                            continue;

                        string? perevod = episode?.title;
                        if (perevod != null && string.IsNullOrEmpty(t))
                            t = perevod;

                        #region Переводы
                        if (!hashvoices.Contains(perevod))
                        {
                            hashvoices.Add(perevod);
                            string link = host + $"lite/zetflix?kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&s={s}&t={HttpUtility.UrlEncode(perevod)}";
                            string active = t == perevod ? "active" : "";

                            htmlvoices.Append("<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + perevod + "</div>");
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

                            var streams = new List<(string link, string quality)>() { Capacity = 4 };

                            foreach (Match m in Regex.Matches(file, $"\\[(1080|720|480|360)p?\\]([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))"))
                            {
                                string link = m.Groups[2].Value;
                                if (string.IsNullOrEmpty(link))
                                    continue;

                                if (usehls && !link.Contains(".m3u"))
                                    link += ":hls:manifest.m3u8";
                                else if (!usehls && link.Contains(".m3u"))
                                    link = link.Replace(":hls:manifest.m3u8", "");

                                streams.Insert(0, (onstreamfile.Invoke(link), $"{m.Groups[1].Value}p"));
                            }

                            if (streams.Count == 0)
                                continue;

                            string streansquality = "\"quality\": {" + string.Join(",", streams.Select(s => $"\"{s.quality}\":\"{s.link}\"")) + "}";

                            htmlepisodes.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(name, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + $"{title ?? original_title} ({name})" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + name + "</div></div>");
                            firstjson = false;
                        }
                    }

                    html.Append(htmlvoices.ToString() + "</div><div class=\"videos__line\">" + htmlepisodes.ToString());
                }
                #endregion
            }

            return html.ToString() + "</div>";
        }
        #endregion
    }
}
