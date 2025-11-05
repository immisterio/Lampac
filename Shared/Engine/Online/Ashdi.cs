using Shared.Models.Base;
using Shared.Models.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Shared.Models.Online.Ashdi;

namespace Shared.Engine.Online
{
    public struct AshdiInvoke
    {
        #region AshdiInvoke
        string host;
        string apihost;
        Func<string, ValueTask<string>> onget;
        Func<string, string> onstreamfile;
        Func<string, string> onlog;
        Action requesterror;

        public AshdiInvoke(string host, string apihost, Func<string, ValueTask<string>> onget, Func<string, string> onstreamfile, Func<string, string> onlog = null, Action requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        public async ValueTask<EmbedModel> Embed(long kinopoisk_id)
        {
            string product = await onget.Invoke($"{apihost}/api/product/read_api.php?kinopoisk={kinopoisk_id}");
            if (product == null)
            {
                requesterror?.Invoke();
                return null;
            }

            if (product.Contains("Product does not exist"))
                return new EmbedModel() { IsEmpty = true };

            string iframeuri = Regex.Match(product, "src=\"(https?://[^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(iframeuri))
            {
                requesterror?.Invoke();
                return null;
            }

            string content = await onget.Invoke(iframeuri);
            if (content == null || !content.Contains("Playerjs"))
            {
                requesterror?.Invoke();
                return null;
            }

            if (!content.Contains("file:'[{"))
                return new EmbedModel() { content = content };

            Voice[] root = null;

            try
            {
                root = JsonSerializer.Deserialize<Voice[]>(Regex.Match(content, "file:'([^\n\r]+)',").Groups[1].Value);
                if (root == null || root.Length == 0)
                    return null;
            }
            catch { return null; }

            return new EmbedModel() { serial = root };
        }
        #endregion

        #region Html
        public string Html(EmbedModel md, long kinopoisk_id, string title, string original_title, int t, int s, VastConf vast = null, bool rjson = false, string mybaseurl = null)
        {
            if (md == null || md.IsEmpty || (string.IsNullOrEmpty(md.content) && md.serial == null))
                return string.Empty;

            string fixStream(string _l) => _l.Replace("0yql3tj", "oyql3tj");

            if (md.content != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, 1);

                string hls = Regex.Match(md.content, "file:([\t ]+)?(\"|')([\t ]+)?(?<hls>https?://[^\"'\n\r\t ]+/index.m3u8)").Groups["hls"].Value;
                if (string.IsNullOrEmpty(hls))
                    return string.Empty;

                #region subtitle
                SubtitleTpl? subtitles = null;
                string subtitle = new Regex("subtitle(\")?:\"([^\"]+)\"").Match(md.content).Groups[2].Value;

                if (!string.IsNullOrEmpty(subtitle))
                {
                    var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(subtitle);
                    subtitles = new SubtitleTpl(match.Length);

                    while (match.Success)
                    {
                        subtitles.Value.Append(match.Groups[1].Value, onstreamfile.Invoke(fixStream(match.Groups[2].Value)));
                        match = match.NextMatch();
                    }
                }
                #endregion

                mtpl.Append("По умолчанию", onstreamfile.Invoke(fixStream(hls)), subtitles: subtitles, vast: vast);

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);

                try
                {
                    if (s == -1)
                    {
                        var tpl = new SeasonTpl();
                        var hashseason = new HashSet<string>();

                        foreach (var voice in md.serial)
                        {
                            foreach (var season in voice.folder)
                            {
                                if (hashseason.Contains(season.title))
                                    continue;

                                hashseason.Add(season.title);
                                string numberseason = Regex.Match(season.title, "([0-9]+)$").Groups[1].Value;
                                if (string.IsNullOrEmpty(numberseason))
                                    continue;

                                string baseUrl = mybaseurl ?? (host + $"lite/ashdi?rjson={rjson}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}");
                                string link = $"{baseUrl}&s={numberseason}";

                                tpl.Append(season.title, link, numberseason);
                            }
                        }

                        return rjson ? tpl.ToJson() : tpl.ToHtml();
                    }
                    else
                    {
                        #region Перевод
                        var vtpl = new VoiceTpl();

                        for (int i = 0; i < md.serial.Length; i++)
                        {
                            if (md.serial[i].folder.FirstOrDefault(i => i.title.EndsWith($" {s}")).title == null)
                                continue;

                            if (t == -1)
                                t = i;

                            string baseUrl = mybaseurl ?? (host + $"lite/ashdi?rjson={rjson}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}");
                            string link = $"{baseUrl}&s={s}&t={i}";

                            vtpl.Append(md.serial[i].title, t == i, link);
                        }
                        #endregion

                        string sArch = s.ToString();
                        var episodes = md.serial[t].folder.First(i => i.title.EndsWith($" {s}")).folder;

                        var etpl = new EpisodeTpl(episodes.Length);

                        foreach (var episode in episodes)
                        {
                            #region subtitle
                            SubtitleTpl? subtitles = null;

                            if (!string.IsNullOrEmpty(episode.subtitle))
                            {
                                var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(episode.subtitle);
                                subtitles = new SubtitleTpl(match.Length);

                                while (match.Success)
                                {
                                    subtitles.Value.Append(match.Groups[1].Value, onstreamfile.Invoke(fixStream(match.Groups[2].Value)));
                                    match = match.NextMatch();
                                }
                            }
                            #endregion

                            string file = onstreamfile.Invoke(fixStream(episode.file));
                            etpl.Append(episode.title, title ?? original_title, sArch, Regex.Match(episode.title, "([0-9]+)$").Groups[1].Value, file, subtitles: subtitles, vast: vast);
                        }

                        if (rjson)
                            return etpl.ToJson(vtpl);

                        return vtpl.ToHtml() + etpl.ToHtml();
                    }
                }
                catch
                {
                    return string.Empty;
                }
                #endregion
            }
        }
        #endregion
    }
}
