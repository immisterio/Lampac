using Lampac.Models.LITE.AniLibria;
using Shared.Model.Templates;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class AniLibriaInvoke
    {
        #region AniLibriaInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<List<RootObject>?>> onget;
        Func<string, string> onstreamfile;
        Action? requesterror;

        public AniLibriaInvoke(string? host, string apihost, Func<string, ValueTask<List<RootObject>?>> onget, Func<string, string> onstreamfile, Action? requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        public async ValueTask<List<RootObject>?> Embed(string title)
        {
            List<RootObject>? search = await onget($"{apihost}/v2/searchTitles?search=" + HttpUtility.UrlEncode(title));
            if (search == null)
            {
                requesterror?.Invoke();
                return null;
            }

            var result = new List<RootObject>();
            foreach (var item in search)
            {
                if (item.names.ru != null && item.names.ru.ToLower().StartsWith(title.ToLower()))
                    result.Add(item);
            }

            return result;
        }
        #endregion

        #region Html
        public string Html(List<RootObject>? result, string title, string? code, int year)
        {
            if (result == null || result.Count == 0)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(code) || (result.Count == 1 && result[0].season.year == year && result[0].names.ru?.ToLower() == title.ToLower()))
            {
                #region Серии
                bool firstjson = true;
                var html = new StringBuilder();
                html.Append("<div class=\"videos__line\">");

                var root = string.IsNullOrWhiteSpace(code) ? result[0] : result.Find(i => i.code == code);

                foreach (var episode in root.player.playlist.Select(i => i.Value))
                {
                    #region streansquality
                    string streansquality = string.Empty;

                    foreach (var f in new List<(string quality, string? url)> { ("1080p", episode.hls.fhd), ("720p", episode.hls.hd), ("480p", episode.hls.sd) })
                    {
                        if (string.IsNullOrWhiteSpace(f.url))
                            continue;

                        streansquality += $"\"{f.quality}\":\"" + onstreamfile($"https://{root.player.host}{f.url}") + "\",";
                    }

                    streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";
                    #endregion

                    string hls = episode.hls.fhd ?? episode.hls.hd ?? episode.hls.sd;
                    hls = onstreamfile($"https://{root.player.host}{hls}");

                    string season = string.IsNullOrWhiteSpace(code) || (root.names.ru?.ToLower() == title.ToLower()) ? "1" : "0";

                    html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + season + "\" e=\"" + episode.serie + "\" data-json='{\"method\":\"play\",\"url\":\"" + hls + "\",\"title\":\"" + $"{title} ({episode.serie} серия)" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.serie} серия" + "</div></div>");
                    firstjson = false;
                }

                return html.ToString() + "</div>";
                #endregion
            }
            else
            {
                #region Поиск
                var stpl = new SimilarTpl(result.Count);
                string? enc_title = HttpUtility.UrlEncode(title);

                foreach (var root in result)
                {
                    string? name = !string.IsNullOrEmpty(root.names.ru) && !string.IsNullOrEmpty(root.names.en) ? $"{root.names.ru} / {root.names.en}" : (root.names.ru ?? root.names.en);

                    stpl.Append(name, root.season.year.ToString(), string.Empty, host + $"lite/anilibria?title={enc_title}&code={root.code}");
                }

                return stpl.ToHtml();
                #endregion
            }
        }
        #endregion
    }
}
