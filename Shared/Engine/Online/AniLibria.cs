using Shared.Models.Base;
using Shared.Models.Online.AniLibria;
using Shared.Models.Templates;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public struct AniLibriaInvoke
    {
        #region AniLibriaInvoke
        string host;
        string apihost;
        Func<string, ValueTask<List<RootObject>>> onget;
        Func<string, string> onstreamfile;
        Action requesterror;

        public AniLibriaInvoke(string host, string apihost, Func<string, ValueTask<List<RootObject>>> onget, Func<string, string> onstreamfile, Action requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        public async ValueTask<List<RootObject>> Embed(string title)
        {
            List<RootObject> search = await onget($"{apihost}/v2/searchTitles?search=" + HttpUtility.UrlEncode(title));
            if (search == null)
            {
                requesterror?.Invoke();
                return null;
            }

            string stitle = StringConvert.SearchName(title);

            var result = new List<RootObject>(search.Count);

            foreach (var item in search)
            {
                if (item.names.ru != null && StringConvert.SearchName(item.names.ru).StartsWith(stitle))
                    result.Add(item);
                else if (item.names.en != null && StringConvert.SearchName(item.names.en).StartsWith(stitle))
                    result.Add(item);
            }

            if (result.Count == 0)
                return search;

            return result;
        }
        #endregion

        #region Html
        public string Html(List<RootObject> result, string title, string code, int year, bool rjson = false, VastConf vast = null, bool similar = false)
        {
            if (result == null || result.Count == 0)
                return string.Empty;

            string stitle = StringConvert.SearchName(title);

            if (!similar && (!string.IsNullOrEmpty(code) || (result.Count == 1 && result[0].season.year == year && (StringConvert.SearchName(result[0].names.ru) == stitle || StringConvert.SearchName(result[0].names.en) == stitle))))
            {
                #region Серии
                var root = string.IsNullOrEmpty(code) ? result[0] : result.Find(i => i.code == code);
                var episodes = root.player.playlist.Select(i => i.Value);

                var etpl = new EpisodeTpl(episodes.Count());

                foreach (var episode in episodes)
                {
                    var streamquality = new StreamQualityTpl();

                    foreach (var f in new List<(string quality, string url)> { ("1080p", episode.hls.fhd), ("720p", episode.hls.hd), ("480p", episode.hls.sd) })
                    {
                        if (string.IsNullOrWhiteSpace(f.url))
                            continue;

                        streamquality.Append(onstreamfile($"https://{root.player.host}{f.url}"), f.quality);
                    }

                    string season = StringConvert.SearchName(root.names.ru) == stitle || StringConvert.SearchName(root.names.en) == stitle ? "1" : "0";
                    if (season == "0")
                    {
                        season = Regex.Match(code ?? "", "-([0-9]+)(nd|th)").Groups[1].Value;
                        if (string.IsNullOrEmpty(season))
                        {
                            season = Regex.Match(code ?? "", "season-([0-9]+)").Groups[1].Value;
                            if (string.IsNullOrEmpty(season))
                                season = string.IsNullOrEmpty(code) ? "0" : "1";
                        }
                    }

                    etpl.Append($"{episode.serie} серия", title, season, episode.serie.ToString(), streamquality.Firts().link, streamquality: streamquality, vast: vast);
                }

                return rjson ? etpl.ToJson() : etpl.ToHtml();
                #endregion
            }
            else
            {
                #region Поиск
                var stpl = new SimilarTpl(result.Count);
                string enc_title = HttpUtility.UrlEncode(title);

                foreach (var root in result)
                {
                    string name = !string.IsNullOrEmpty(root.names.ru) && !string.IsNullOrEmpty(root.names.en) ? $"{root.names.ru} / {root.names.en}" : (root.names.ru ?? root.names.en);

                    string img = root.posters.original.url;
                    if (!string.IsNullOrEmpty(img))
                        img = "https://anilibria.tv" + img;

                    stpl.Append(name, root.season.year.ToString(), string.Empty, host + $"lite/anilibria?title={enc_title}&code={root.code}", PosterApi.Size(img));
                }

                return rjson ? stpl.ToJson() : stpl.ToHtml();
                #endregion
            }
        }
        #endregion
    }
}
