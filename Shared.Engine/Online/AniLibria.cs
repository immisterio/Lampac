using Lampac.Models.LITE.AniLibria;
using Shared.Model.Templates;
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
                else if (item.names.en != null && item.names.en.ToLower().StartsWith(title.ToLower()))
                    result.Add(item);
            }

            return result;
        }
        #endregion

        #region Html
        public string Html(List<RootObject>? result, string title, string? code, int year, bool rjson = false)
        {
            if (result == null || result.Count == 0)
                return string.Empty;

            if (!string.IsNullOrEmpty(code) || (result.Count == 1 && result[0].season.year == year && (result[0].names.ru?.ToLower() == title.ToLower() || result[0].names.en?.ToLower() == title.ToLower())))
            {
                #region Серии
                var etpl = new EpisodeTpl();

                var root = string.IsNullOrEmpty(code) ? result[0] : result.Find(i => i.code == code);

                foreach (var episode in root.player.playlist.Select(i => i.Value))
                {
                    #region streansquality
                    var streams = new List<(string link, string quality)>() { Capacity = 5 };

                    foreach (var f in new List<(string quality, string? url)> { ("1080p", episode.hls.fhd), ("720p", episode.hls.hd), ("480p", episode.hls.sd) })
                    {
                        if (string.IsNullOrWhiteSpace(f.url))
                            continue;

                        streams.Add((onstreamfile($"https://{root.player.host}{f.url}"), f.quality));
                    }
                    #endregion

                    string hls = episode.hls.fhd ?? episode.hls.hd ?? episode.hls.sd;
                    hls = onstreamfile($"https://{root.player.host}{hls}");

                    string season = root.names.ru?.ToLower() == title.ToLower() || root.names.en?.ToLower() == title.ToLower() ? "1" : "0";
                    if (season == "0")
                    {
                        season = Regex.Match(code ?? "", "-([0-9]+)(nd|th)").Groups[1].Value;
                        if (string.IsNullOrEmpty(season))
                            season = string.IsNullOrEmpty(code) ? "0" : "1";
                    }

                    etpl.Append($"{episode.serie} серия", title, season, episode.serie.ToString(), hls, streamquality: new StreamQualityTpl(streams));
                }

                return rjson ? etpl.ToJson() : etpl.ToHtml();
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

                return rjson ? stpl.ToJson() : stpl.ToHtml();
                #endregion
            }
        }
        #endregion
    }
}
