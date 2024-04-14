using Lampac.Models.LITE.Filmix;
using Shared.Model.Online;
using Shared.Model.Online.Filmix;
using Shared.Model.Templates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class FilmixInvoke
    {
        #region FilmixInvoke
        public bool disableSphinxSearch;

        public string? token;
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string, List<HeadersModel>?, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;
        Action? requesterror;

        public FilmixInvoke(string? host, string apihost, string? token, Func<string, ValueTask<string?>> onget, Func<string, string, List<HeadersModel>?, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null, Action? requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.token = token;
            this.onget = onget;
            this.onpost = onpost;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.requesterror = requesterror;
        }
        #endregion

        #region Search
        async public ValueTask<SearchResult?> Search(string? title, string? original_title, int clarification, int year)
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title) || year == 0)
                return null;

            string uri = $"{apihost}/api/v2/search?story={HttpUtility.UrlEncode(clarification == 1 ? title : (original_title ?? title))}&user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_token={token}&user_dev_vendor=Xiaomi";
            onlog?.Invoke(uri);
            
            string? json = await onget.Invoke(uri);
            if (json == null)
                return await Search2(title, original_title, clarification, year);

            List<SearchModel>? root = null;

            try
            {
                root = JsonSerializer.Deserialize<List<SearchModel>>(json);
            }
            catch { }

            if (root == null)
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

            return new SearchResult() { similars = stpl.ToHtml() };
        }
        #endregion

        #region Search2
        async ValueTask<SearchResult?> Search2(string? title, string? original_title, int clarification, int year)
        {
            if (disableSphinxSearch)
            {
                requesterror?.Invoke();
                return null;
            }

            onlog?.Invoke("Search2");

            string? html = await onpost.Invoke("https://filmix.biz/engine/ajax/sphinx_search.php", $"scf=fx&story={HttpUtility.UrlEncode(clarification == 1 ? title : (original_title ?? title))}&search_start=0&do=search&subaction=search&years_ot=1902&years_do={DateTime.Today.Year}&kpi_ot=1&kpi_do=10&imdb_ot=1&imdb_do=10&sort_name=&undefined=asc&sort_date=&sort_favorite=&simple=1", HeadersModel.Init( 
                ("Origin", "https://filmix.biz"),
                ("Referer", "https://filmix.biz/search/"),
                ("X-Requested-With", "XMLHttpRequest"),
                ("Sec-Fetch-Site", "same-origin"),
                ("Sec-Fetch-Mode", "cors"),
                ("Sec-Fetch-Dest", "empty"),
                ("Cookie", "x-a-key=sinatra; FILMIXNET=2g5orcue70hmbkugbr7vi431l0; _ga_GYLWSWSZ3C=GS1.1.1703578122.1.0.1703578122.0.0.0; _ga=GA1.1.1855910641.1703578123"),
                ("Accept-Language", "ru-RU,ru;q=0.9")
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

            return new SearchResult() { similars = stpl.ToHtml() };
        }
        #endregion

        #region Post
        async public ValueTask<RootObject?> Post(int postid)
        {
            string uri = $"{apihost}/api/v2/post/{postid}?user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_token={token}&user_dev_vendor=Xiaomi";
            onlog?.Invoke(uri);

            string? json = await onget.Invoke(uri);
            if (json == null)
            {
                requesterror?.Invoke();
                return null;
            }

            try
            {
                var root = JsonSerializer.Deserialize<RootObject>(json.Replace("\"playlist\":[],", "\"playlist\":null,"));

                if (root?.player_links == null)
                    return null;

                return root;
            }
            catch { return null; }
        }
        #endregion

        #region Html
        public string Html(RootObject? root, bool pro, int postid, string? title, string? original_title, int t, int? s)
        {
            var player_links = root?.player_links;
            if (player_links == null)
                return string.Empty;

            onlog?.Invoke("html reder");

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

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

                    mtpl.Append(v.translation, streams[0].link, streamquality: new StreamQualityTpl(streams));
                }

                return mtpl.ToHtml();
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
                    var tpl = new SeasonTpl(!string.IsNullOrEmpty(root?.quality) ? $"{root.quality}p" : null);

                    foreach (var season in player_links.playlist)
                    {
                        string link = host + $"lite/filmix?postid={postid}&title={enc_title}&original_title={enc_original_title}&s={season.Key}";
                        tpl.Append($"{season.Key.Replace("-1", "1")} сезон", link);
                    }

                    return tpl.ToHtml();
                    #endregion
                }
                else
                {
                    #region Перевод
                    int indexTranslate = 0;

                    foreach (var translation in player_links.playlist[s.ToString()])
                    {
                        string link = host + $"lite/filmix?postid={postid}&title={enc_title}&original_title={enc_original_title}&s={s}&t={indexTranslate}";
                        string active = t == indexTranslate ? "active" : "";

                        indexTranslate++;
                        html.Append("<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + translation.Key + "</div>");
                    }

                    html.Append("</div><div class=\"videos__line\">");
                    #endregion

                    #region Deserialize
                    Dictionary<string, Movie>? episodes = null;

                    try
                    {
                        episodes = player_links.playlist[s.ToString()].ElementAt(t).Value.Deserialize<Dictionary<string, Movie>>();
                    }
                    catch 
                    {
                        try
                        {
                            int episod_id = 0;
                            episodes = new Dictionary<string, Movie>();

                            foreach (var item in player_links.playlist[s.ToString()].ElementAt(t).Value.Deserialize<List<Movie>>())
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

                        int? fis = s == -1 ? 1 : s;
                        html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + fis + "\" e=\"" + episode.Key + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + $"{title ?? original_title} ({episode.Key} серия)" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.Key} серия" + "</div></div>");
                        firstjson = false;
                    }
                    #endregion
                }
                #endregion
            }

            return html.ToString() + "</div>";
        }
        #endregion
    }
}
