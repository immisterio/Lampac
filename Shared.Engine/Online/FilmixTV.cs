using Shared.Model.Online;
using Shared.Model.Online.Filmix;
using Shared.Model.Online.FilmixTV;
using Shared.Model.Templates;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace Shared.Engine.Online
{
    public class FilmixTVInvoke
    {
        #region FilmixTVInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string, List<HeadersModel>?, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;
        Action? requesterror;
        bool rjson;

        public FilmixTVInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string, List<HeadersModel>?, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null, Action? requesterror = null, bool rjson = false)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onpost = onpost;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.requesterror = requesterror;
            this.rjson = rjson;
        }
        #endregion

        #region Search
        async public ValueTask<SearchResult?> Search(string? title, string? original_title, int clarification, int year)
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title) || year == 0)
                return null;

            string uri = $"{apihost}/api-fx/list?search={HttpUtility.UrlEncode(clarification == 1 ? title : (original_title ?? title))}&limit=48";
            onlog?.Invoke(uri);

            string? json = await onget.Invoke(uri);
            if (string.IsNullOrEmpty(json) || !json.Contains("\"status\":\"ok\""))
                return await Search2(title, original_title, year, clarification);

            List<SearchModel>? root = null;

            try
            {
                root = JsonNode.Parse(json)?["items"]?.Deserialize<List<SearchModel>>();
            }
            catch { }

            if (root == null || root.Count == 0)
                return await Search2(title, original_title, year, clarification);

            var ids = new List<int>();
            var stpl = new SimilarTpl(root.Count);

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            foreach (var item in root)
            {
                if (item == null)
                    continue;

                string? name = !string.IsNullOrEmpty(item.title) && !string.IsNullOrEmpty(item.original_title) ? $"{item.title} / {item.original_title}" : (item.title ?? item.original_title);

                stpl.Append(name, item.year.ToString(), string.Empty, host + $"lite/filmixtv?postid={item.id}&title={enc_title}&original_title={enc_original_title}");

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

        #region Search2
        async ValueTask<SearchResult?> Search2(string? title, string? original_title, int year, int clarification)
        {
            async ValueTask<List<SearchModel>?> gosearch(string? story)
            {
                if (string.IsNullOrEmpty(story))
                    return null;

                string uri = $"http://filmixapp.cyou/api/v2/search?story={HttpUtility.UrlEncode(story)}&user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_token=&user_dev_vendor=Xiaomi";
                onlog?.Invoke(uri);

                string? json = await onget.Invoke(uri);
                if (json == null)
                    return null;

                List<SearchModel>? root = null;

                try
                {
                    root = JsonSerializer.Deserialize<List<SearchModel>>(json);
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
                return null;

            var ids = new List<int>();
            var stpl = new SimilarTpl(result.Count);

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            foreach (var item in result)
            {
                if (item == null)
                    continue;

                string? name = !string.IsNullOrEmpty(item.title) && !string.IsNullOrEmpty(item.original_title) ? $"{item.title} / {item.original_title}" : (item.title ?? item.original_title);

                stpl.Append(name, item.year.ToString(), string.Empty, host + $"lite/filmixtv?postid={item.id}&title={enc_title}&original_title={enc_original_title}");

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

        #region Post
        public RootObject? Post(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                requesterror?.Invoke();
                return null;
            }

            try
            {
                var rootMs = new RootObject();

                if (JsonDocument.Parse(json).RootElement.ValueKind == JsonValueKind.Array)
                {
                    rootMs.Movies = JsonSerializer.Deserialize<MovieTV[]>(json);
                }
                else
                {
                    rootMs.SerialVoice = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Season>>>(json);
                }

                return rootMs;
            }
            catch { return null; }
        }
        #endregion

        #region Html
        public string Html(RootObject? root, bool pro, int postid, string? title, string? original_title, int t, int? s)
        {
            if (root == null)
                return string.Empty;

            #region Сериал
            if (root.SerialVoice != null)
            {
                string? enc_title = HttpUtility.UrlEncode(title);
                string? enc_original_title = HttpUtility.UrlEncode(original_title);

                int indexTranslate = 0;
                var vtpl = new VoiceTpl();

                foreach (var voiceover in root.SerialVoice)
                {
                    string link = host + $"lite/filmixtv?rjson={rjson}&postid={postid}&title={enc_title}&original_title={enc_original_title}&t={indexTranslate}";
                    bool active = t == indexTranslate;

                    indexTranslate++;
                    vtpl.Append(voiceover.Key, active, link);
                }

                var selectedVoiceOverIndex = t != null ? t : 0;
                var selectedVoiceOver = root.SerialVoice.ElementAt(selectedVoiceOverIndex).Value;

                if (s == null)
                {
                    #region Сезоны
                    var maxQuality = selectedVoiceOver
                        .SelectMany(season => season.Value.episodes)
                        .SelectMany(episode => episode.Value.files)
                        .Max(file => file.quality);

                    var quality = $"{maxQuality}p";
                    var tpl = new SeasonTpl(quality);

                    foreach (var season in selectedVoiceOver)
                    {
                        var link = $"{host}lite/filmixtv?rjson={rjson}&postid={postid}&title={enc_title}&original_title={enc_original_title}&s={season.Value.season}&t={selectedVoiceOverIndex}";
                        tpl.Append($"{season.Value.season} сезон", link, season.Value.season);
                    }

                    return rjson ? tpl.ToJson(vtpl) : tpl.ToHtml(vtpl);
                    #endregion
                }
                else
                {
                    var selectedSeason = selectedVoiceOver.FirstOrDefault(x => x.Value.season == s);

                    if (selectedSeason.Value == null)
                    {
                        return "<div>Сезон не найден</div>";
                    }

                    var etpl = new EpisodeTpl();

                    foreach (var episode in selectedSeason.Value.episodes)
                    {
                        var streams = new List<(string link, string quality)>() { Capacity = pro ? episode.Value.files.Count : 2 };

                        var sortedFiles = episode.Value.files
                            .Where(file => pro || file.quality <= 720)
                            .OrderByDescending(file => file.quality);

                        foreach (var file in sortedFiles)
                        {
                            streams.Add((onstreamfile.Invoke(file.url), $"{file.quality}p"));
                        }

                        if (streams.Count == 0)
                            continue;

                        etpl.Append($"{episode.Key.TrimStart('e')} серия", title ?? original_title, selectedSeason.Value.season.ToString(), episode.Key.TrimStart('e'), streams[0].link, streamquality: new StreamQualityTpl(streams));
                    }

                    if (rjson)
                        return etpl.ToJson(vtpl);

                    return vtpl.ToHtml() + etpl.ToHtml();
                }
            }
            #endregion

            #region Фильм
            else if (root.Movies != null)
            {
                var mtpl = new MovieTpl(title, original_title, root.Movies.Length);

                foreach (var item in root.Movies)
                {
                    var streams = new List<(string link, string quality)>() { Capacity = pro ? item.files.Count : 2 };

                    foreach (var file in item.files)
                    {
                        if (!pro)
                        {
                            if (pro && file.quality > 480)
                                continue;

                            if (file.quality > 720)
                                continue;
                        }

                        streams.Add((onstreamfile.Invoke(file.url), $"{file.quality}p"));
                    }

                    if (streams.Count == 0)
                        continue;

                    mtpl.Append(item.voiceover, streams[0].link, streamquality: new StreamQualityTpl(streams));
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
            }
            #endregion

            return string.Empty;
        }
        #endregion
    }
}
