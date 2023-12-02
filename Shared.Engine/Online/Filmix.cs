using Lampac.Models.LITE.Filmix;
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
        string? host, token;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public FilmixInvoke(string? host, string apihost, string? token, Func<string, ValueTask<string?>> onget, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.token = token;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
        }
        #endregion

        #region Search
        async public ValueTask<(int id, string? similars)> Search(string? title, string? original_title, int clarification, int year)
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title) || year == 0)
                return (0, null);

            string uri = $"{apihost}/api/v2/search?story={HttpUtility.UrlEncode(clarification == 1 ? title : (original_title ?? title))}&user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_token={token}&user_dev_vendor=Xiaomi";
           
            onlog?.Invoke("uri: " + uri);
            
            string? json = await onget.Invoke(uri);
            if (json == null)
                return (0, null);

            onlog?.Invoke("json: " + json);

            var root = JsonSerializer.Deserialize<List<SearchModel>>(json);
            if (root == null || root.Count == 0)
                return (0, null);

            onlog?.Invoke("root.Count " + root.Count);

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

            if (ids.Count == 1)
                return (ids[0], null);

            return (0, stpl.ToHtml());
        }
        #endregion

        #region Post
        async public ValueTask<PlayerLinks?> Post(int postid)
        {
            string uri = $"{apihost}/api/v2/post/{postid}?user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_token={token}&user_dev_vendor=Xiaomi";

            onlog?.Invoke(uri);

            string? json = await onget.Invoke(uri);
            if (json == null)
                return null;

            json = json.Replace("\"playlist\":[],", "\"playlist\":null,");
            onlog?.Invoke(json);

            var root = JsonSerializer.Deserialize<RootObject>(json);

            if (root?.player_links == null)
                return null;

            onlog?.Invoke("player_links ok");
            return root.player_links;
        }
        #endregion

        #region Html
        public string Html(PlayerLinks player_links, bool pro, int postid, string? title, string? original_title, int t, int s)
        {
            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            var filmixservtime = DateTime.UtcNow.AddHours(2).Hour;
            bool hidefree720 = string.IsNullOrWhiteSpace(token) && filmixservtime >= 19 && filmixservtime <= 23;

            if (player_links.movie != null && player_links.movie.Count > 0)
            {
                #region Фильм
                if (player_links.movie.Count == 1 && player_links.movie[0].translation.ToLower().StartsWith("заблокировано "))
                    return string.Empty;

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

                    string streansquality = "\"quality\": {" + string.Join(",", streams.Select(s => $"\"{s.quality}\":\"{s.link}\"")) + "}";

                    html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + (title ?? original_title) + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + v.translation + "</div></div>");
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Сериал
                if (player_links.playlist == null || player_links.playlist.Count == 0)
                    return string.Empty;

                string? enc_title = HttpUtility.UrlEncode(title);
                string? enc_original_title = HttpUtility.UrlEncode(original_title);

                if (s == -1)
                {
                    #region Сезоны
                    foreach (var season in player_links.playlist)
                    {
                        string link = host + $"lite/filmix?postid={postid}&title={enc_title}&original_title={enc_original_title}&s={season.Key}";

                        html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season.Key} сезон" + "</div></div></div>");
                        firstjson = false;
                    }
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

                        html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.Key + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + $"{title ?? original_title} ({episode.Key} серия)" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.Key} серия" + "</div></div>");
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
