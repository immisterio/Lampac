using Lampac.Models.LITE.Filmix;
using Shared.Model.Online.Filmix;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
        async public ValueTask<int> Search(string? title, string? original_title, int clarification, int year)
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title) || year == 0)
                return 0;

            string uri = $"{apihost}/api/v2/search?story={HttpUtility.UrlEncode(clarification == 1 ? title : (original_title ?? title))}&user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_token=&user_dev_vendor=Xiaomi";
           
            onlog?.Invoke("uri: " + uri);
            
            string? json = await onget.Invoke(uri);
            if (json == null)
                return 0;

            onlog?.Invoke("json: " + json);

            var root = JsonSerializer.Deserialize<List<SearchModel>>(json);
            if (root == null || root.Count == 0)
                return 0;

            onlog?.Invoke("root.Count " + root.Count);

            int? reservedid = 0;
            foreach (var item in root)
            {
                if (item == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(title) && item.title?.ToLower() == title.ToLower())
                {
                    reservedid = item.id;
                    if (item.year == year)
                        return reservedid ?? 0;
                }

                if (!string.IsNullOrWhiteSpace(original_title) && item.original_title?.ToLower() == original_title.ToLower())
                {
                    reservedid = item.id;
                    if (item.year == year)
                        return reservedid ?? 0;
                }
            }

            return reservedid ?? 0;
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
            string html = "<div class=\"videos__line\">";

            var filmixservtime = DateTime.UtcNow.AddHours(2).Hour;
            bool hidefree720 = string.IsNullOrWhiteSpace(token) && filmixservtime >= 19 && filmixservtime <= 23;

            if (player_links.movie != null && player_links.movie.Count > 0)
            {
                #region Фильм
                if (player_links.movie.Count == 1 && player_links.movie[0].translation.ToLower().StartsWith("заблокировано "))
                    return string.Empty;

                foreach (var v in player_links.movie)
                {
                    string? link = null;
                    string streansquality = string.Empty;
                    List<(string link, string quality)> streams = new List<(string, string)>();

                    foreach (int q in new int[] { 2160, 1440, 1080, 720, 480, 360 })
                    {
                        if (!v.link.Contains($"{q},"))
                            continue;

                        if (hidefree720 && q > 480)
                            continue;

                        if (!pro && q > 720)
                            continue;

                        string l = Regex.Replace(v.link, "_\\[[0-9,]+\\]\\.mp4", $"_{q}.mp4");
                        l = onstreamfile.Invoke(l);

                        if (link == null)
                            link = l;

                        streams.Add((l, $"{q}p"));
                        streansquality += $"\"{$"{q}p"}\":\"" + l + "\",";
                    }

                    streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + link + "\",\"title\":\"" + (title ?? original_title) + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + v.translation + "</div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Сериал
                firstjson = true;

                if (s == -1)
                {
                    #region Сезоны
                    foreach (var season in player_links.playlist)
                    {
                        string link = host + $"lite/filmix?postid={postid}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={season.Key}";

                        html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season.Key} сезон" + "</div></div></div>";
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
                        string link = host + $"lite/filmix?postid={postid}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={s}&t={indexTranslate}";
                        string active = t == indexTranslate ? "active" : "";

                        indexTranslate++;
                        html += "<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + translation.Key + "</div>";
                    }

                    html += "</div><div class=\"videos__line\">";
                    #endregion

                    #region Серии
                    foreach (var episode in player_links.playlist[s.ToString()].ElementAt(t).Value)
                    {
                        string streansquality = string.Empty;
                        List<(string link, string quality)> streams = new List<(string, string)>();

                        foreach (int lq in episode.Value.qualities.OrderByDescending(i => i))
                        {
                            if (hidefree720 && lq > 480)
                                continue;

                            if (!pro && lq > 720)
                                continue;

                            string l = episode.Value.link.Replace("_%s.mp4", $"_{lq}.mp4");
                            l = onstreamfile.Invoke(l);

                            streams.Add((l, $"{lq}p"));
                            streansquality += $"\"{lq}p\":\"" + l + "\",";
                        }

                        streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.Key + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + $"{title ?? original_title} ({episode.Key} серия)" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.Key} серия" + "</div></div>";
                        firstjson = false;
                    }
                    #endregion
                }
                #endregion
            }

            return html + "</div>";
        }
        #endregion
    }
}
