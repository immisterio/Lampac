using Lampac.Models.LITE.Filmix;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Model.Base;
using Shared.Model.Online;
using Shared.Model.Online.Filmix;
using Shared.Model.Templates;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class FilmixInvoke
    {
        #region FilmixInvoke
        public bool disableSphinxSearch;

        public string? token;
        string? host, args;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string, List<HeadersModel>?, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;
        Action? requesterror;
        bool rjson;

        public FilmixInvoke(string? host, string apihost, string? token, Func<string, ValueTask<string?>> onget, Func<string, string, List<HeadersModel>?, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null, Action? requesterror = null, bool rjson = false)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.token = token;
            this.onget = onget;
            this.onpost = onpost;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.requesterror = requesterror;
            this.rjson = rjson;

            args = $"app_lang=ru_RU&user_dev_apk=2.2.0&user_dev_id={UnicTo.Code(16)}&user_dev_name=Xiaomi+24069PC21G&user_dev_os=14&user_dev_token={token}&user_dev_vendor=Xiaomi";
        }
        #endregion

        #region Search
        async public ValueTask<SearchResult?> Search(string? title, string? original_title, int clarification, int year, bool similar)
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title))
                return null;

            string uri = $"{apihost}/api/v2/search?story={HttpUtility.UrlEncode(clarification == 1 ? title : (original_title ?? title))}&{args}";
            onlog?.Invoke(uri);
            
            string? json = await onget.Invoke(uri);
            if (json == null)
                return await Search2(title, original_title, clarification, year);

            List<SearchModel>? root = null;

            try
            {
                root = JsonConvert.DeserializeObject<List<SearchModel>>(json);
            }
            catch { }

            if (root == null || root.Count == 0)
                return await Search2(title, original_title, clarification, year);

            var ids = new List<int>();
            var stpl = new SimilarTpl(root.Count);

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            foreach (var item in root)
            {
                if (item == null)
                    continue;

                string? name = !string.IsNullOrEmpty(item.title) && !string.IsNullOrEmpty(item.original_title) ? $"{item.title} / {item.original_title}"  : (item.title ?? item.original_title);

                stpl.Append(name, item.year.ToString(), string.Empty, host + $"lite/filmix?postid={item.id}&title={enc_title}&original_title={enc_original_title}", item.poster); 

                if ((!string.IsNullOrEmpty(title) && item.title?.ToLower() == title.ToLower()) ||
                    (!string.IsNullOrEmpty(original_title) && item.original_title?.ToLower() == original_title.ToLower()))
                {
                    if (item.year == year)
                        ids.Add(item.id);
                }
            }

            onlog?.Invoke("ids: " + ids.Count);

            if (ids.Count == 1 && !similar)
                return new SearchResult() { id = ids[0] };

            return new SearchResult() { similars = stpl };
        }
        #endregion

        #region Search2
        async ValueTask<SearchResult?> Search2(string? title, string? original_title, int clarification, int year)
        {
            async ValueTask<List<SearchModel>?> gosearch(string? story)
            {
                if (string.IsNullOrEmpty(story))
                    return null;

                string uri = $"https://api.filmix.tv/api-fx/list?search={HttpUtility.UrlEncode(story)}&limit=48";
                onlog?.Invoke(uri);

                string? json = await onget.Invoke(uri);
                if (string.IsNullOrEmpty(json) || !json.Contains("\"status\":\"ok\""))
                    return null;

                List<SearchModel>? root = null;

                try
                {
                    root = JObject.Parse(json)?["items"]?.ToObject<List<SearchModel>>();
                }
                catch { }

                if (root == null || root.Count == 0)
                    return null;

                return root;
            }

            var result = await gosearch(clarification == 1 ? original_title : title);
            if (result == null)
                result = await gosearch(clarification == 1 ? title : original_title);

            if (result == null)
                return await Search3(title, original_title, clarification, year);

            var ids = new List<int>();
            var stpl = new SimilarTpl(result.Count);

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            foreach (var item in result)
            {
                if (item == null)
                    continue;

                string? name = !string.IsNullOrEmpty(item.title) && !string.IsNullOrEmpty(item.original_title) ? $"{item.title} / {item.original_title}" : (item.title ?? item.original_title);

                stpl.Append(name, item.year.ToString(), string.Empty, host + $"lite/filmix?postid={item.id}&title={enc_title}&original_title={enc_original_title}");

                if ((!string.IsNullOrEmpty(title) && item.title?.ToLower() == title.ToLower()) ||
                    (!string.IsNullOrEmpty(original_title) && item.original_title?.ToLower() == original_title.ToLower()))
                {
                    if (item.year == year)
                        ids.Add(item.id);
                }
            }

            onlog?.Invoke("ids: " + ids.Count);

            if (ids.Count == 1)
                return new SearchResult() { id = ids[0] };

            return new SearchResult() { similars = stpl };
        }
        #endregion

        #region Search3
        async ValueTask<SearchResult?> Search3(string? title, string? original_title, int clarification, int year)
        {
            if (disableSphinxSearch)
            {
                requesterror?.Invoke();
                return null;
            }

            onlog?.Invoke("Search3");

            string? html = await onpost.Invoke("https://filmix.fm/engine/ajax/sphinx_search.php", $"scf=fx&story={HttpUtility.UrlEncode(clarification == 1 ? title : (original_title ?? title))}&search_start=0&do=search&subaction=search&years_ot=1902&years_do={DateTime.Today.Year}&kpi_ot=1&kpi_do=10&imdb_ot=1&imdb_do=10&sort_name=&undefined=asc&sort_date=&sort_favorite=&simple=1", HeadersModel.Init( 
                ("Origin", "https://filmix.fm"),
                ("Referer", "https://filmix.fm/search/"),
                ("X-Requested-With", "XMLHttpRequest"),
                ("Sec-Fetch-Site", "same-origin"),
                ("Sec-Fetch-Mode", "cors"),
                ("Sec-Fetch-Dest", "empty"),
                ("Cookie", "x-a-key=sinatra; FILMIXNET=2g5orcue70hmbkugbr7vi431l0; _ga_GYLWSWSZ3C=GS1.1.1703578122.1.0.1703578122.0.0.0; _ga=GA1.1.1855910641.1703578123")
            ));

            if (html == null)
            {
                requesterror?.Invoke();
                return null;
            }

            var ids = new List<int>();
            var stpl = new SimilarTpl();

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            foreach (string row in html.Split("</article>"))
            {
                string ftitle = Regex.Match(row, "itemprop=\"name\" content=\"([^\"]+)\"").Groups[1].Value;
                string ftitle_orig = Regex.Match(row, "itemprop=\"alternativeHeadline\" content=\"([^\"]+)\"").Groups[1].Value;
                string fyear = Regex.Match(row, "itemprop=\"copyrightYear\"[^>]+>([0-9]{4})").Groups[1].Value;
                string fid = Regex.Match(row, "data-id=\"([0-9]+)\"").Groups[1].Value;

                if (int.TryParse(fid, out int id) && id > 0)
                {
                    string? name = !string.IsNullOrEmpty(ftitle) && !string.IsNullOrEmpty(ftitle_orig) ? $"{ftitle} / {ftitle_orig}" : (ftitle ?? ftitle_orig);

                    stpl.Append(name, fyear, string.Empty, host + $"lite/filmix?postid={id}&title={enc_title}&original_title={enc_original_title}");

                    if ((!string.IsNullOrEmpty(title) && ftitle.ToLower() == title.ToLower()) ||
                        (!string.IsNullOrEmpty(original_title) && ftitle_orig.ToLower() == original_title.ToLower()))
                    {
                        if (fyear == year.ToString())
                            ids.Add(id);
                    }
                }
            }

            onlog?.Invoke("ids: " + ids.Count);

            if (ids.Count == 1)
                return new SearchResult() { id = ids[0] };

            return new SearchResult() { similars = stpl };
        }
        #endregion

        #region Post
        async public ValueTask<RootObject?> Post(int postid)
        {
            string uri = $"{apihost}/api/v2/post/{postid}?{args}";
            onlog?.Invoke(uri);

            string? json = await onget.Invoke(uri);
            if (json == null)
            {
                requesterror?.Invoke();
                return null;
            }

            try
            {
                var root = JsonConvert.DeserializeObject<RootObject>(json.Replace("\"playlist\":[],", "\"playlist\":null,"));

                if (root?.player_links == null)
                    return null;

                return root;
            }
            catch { return null; }
        }
        #endregion

        #region Html
        public string Html(RootObject? root, bool pro, int postid, string? title, string? original_title, int t, int? s, VastConf? vast = null)
        {
            var player_links = root?.player_links;
            if (player_links == null)
                return string.Empty;

            onlog?.Invoke("html reder");

            int filmixservtime = DateTime.UtcNow.AddHours(2).Hour;
            bool hidefree720 = string.IsNullOrEmpty(token) /*&& filmixservtime >= 19 && filmixservtime <= 23*/;

            if (player_links.movie != null && player_links.movie.Count > 0)
            {
                #region Фильм
                onlog?.Invoke("movie 1");

                if (player_links.movie.Count == 1 && player_links.movie[0].translation.ToLower().StartsWith("заблокировано "))
                    return string.Empty;

                onlog?.Invoke("movie 2");
                var mtpl = new MovieTpl(title, original_title, player_links.movie.Count);

                foreach (var v in player_links.movie)
                {
                    var streams = new List<(string link, string quality)>() { Capacity = pro ? 5 : 2 };

                    foreach (int q in new int[] { 2160, 1440, 1080, 720, 480 })
                    {
                        if (!pro)
                        {
                            if (hidefree720 && q > 480)
                                continue;

                            if (q > 720)
                                continue;
                        }

                        if (!v.link.Contains($"{q},"))
                            continue;

                        string l = Regex.Replace(v.link, "_\\[[0-9,]+\\]\\.mp4", $"_{q}.mp4");
                        streams.Add((onstreamfile.Invoke(l), $"{q}p"));
                    }

                    if (streams.Count == 0)
                        continue;

                    mtpl.Append(v.translation, streams[0].link, streamquality: new StreamQualityTpl(streams), vast: vast);
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                if (player_links.playlist == null || player_links.playlist.Count == 0)
                    return string.Empty;

                string? enc_title = HttpUtility.UrlEncode(title);
                string? enc_original_title = HttpUtility.UrlEncode(original_title);

                if (s == null)
                {
                    #region Сезоны
                    var tpl = new SeasonTpl(!string.IsNullOrEmpty(root?.quality) ? $"{root.quality.Replace("+", "")}p" : null);

                    foreach (var season in player_links.playlist)
                    {
                        string link = host + $"lite/filmix?rjson={rjson}&postid={postid}&title={enc_title}&original_title={enc_original_title}&s={season.Key}";
                        tpl.Append($"{season.Key.Replace("-1", "1")} сезон", link, season.Key);
                    }

                    return rjson ? tpl.ToJson() : tpl.ToHtml();
                    #endregion
                }
                else
                {
                    #region Перевод
                    int indexTranslate = 0;
                    var vtpl = new VoiceTpl();

                    foreach (var translation in player_links.playlist[s.ToString()])
                    {
                        string link = host + $"lite/filmix?rjson={rjson}&postid={postid}&title={enc_title}&original_title={enc_original_title}&s={s}&t={indexTranslate}";
                        bool active = t == indexTranslate;

                        indexTranslate++;
                        vtpl.Append(translation.Key, active, link);
                    }
                    #endregion

                    #region Deserialize
                    Dictionary<string, Movie>? episodes = null;

                    try
                    {
                        episodes = player_links.playlist[s.ToString()].ElementAt(t).Value.ToObject<Dictionary<string, Movie>>();
                    }
                    catch
                    {
                        try
                        {
                            int episod_id = 0;
                            episodes = new Dictionary<string, Movie>();

                            foreach (var item in player_links.playlist[s.ToString()].ElementAt(t).Value.ToObject<List<Movie>>())
                            {
                                episod_id++;
                                episodes.Add(episod_id.ToString(), item);
                            }
                        }
                        catch { }
                    }

                    if (episodes == null || episodes.Count == 0)
                        return string.Empty;
                    #endregion

                    #region Серии
                    onlog?.Invoke("episodes: " + episodes.Count);
                    var etpl = new EpisodeTpl();

                    foreach (var episode in episodes)
                    {
                        var streams = new List<(string link, string quality)>() { Capacity = pro ? episode.Value.qualities.Count : 2 };

                        foreach (int lq in episode.Value.qualities.OrderByDescending(i => i))
                        {
                            if (!pro)
                            {
                                if (hidefree720 && lq > 480)
                                    continue;

                                if (lq > 720)
                                    continue;
                            }

                            string l = episode.Value.link.Replace("_%s.mp4", $"_{lq}.mp4");
                            streams.Add((onstreamfile.Invoke(l), $"{lq}p"));
                        }

                        if (streams.Count == 0)
                            continue;

                        string streansquality = "\"quality\": {" + string.Join(",", streams.Select(s => $"\"{s.quality}\":\"{s.link}\"")) + "}";

                        int fis = s == -1 ? 1 : (s ?? 1);

                        etpl.Append($"{episode.Key} серия", title ?? original_title, fis.ToString(), episode.Key, streams[0].link, streamquality: new StreamQualityTpl(streams), vast: vast);
                    }
                    #endregion

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
