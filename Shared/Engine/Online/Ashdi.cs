using Shared.Engine.RxEnumerate;
using Shared.Models.Base;
using Shared.Models.Online.Ashdi;
using Shared.Models.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public struct AshdiInvoke
    {
        #region AshdiInvoke
        string host;
        string apihost;
        HttpHydra httpHydra;
        Func<string, string> onstreamfile;
        Action requesterror;

        public AshdiInvoke(string host, string apihost, HttpHydra httpHydra, Func<string, string> onstreamfile, Action requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.httpHydra = httpHydra;
            this.onstreamfile = onstreamfile;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        public async Task<EmbedModel> Embed(long kinopoisk_id)
        {
            string iframeuri = null;
            EmbedModel embed = null;

            await httpHydra.GetSpan($"{apihost}/api/product/read_api.php?kinopoisk={kinopoisk_id}", product =>
            {
                if (product.Contains("Product does not exist", StringComparison.OrdinalIgnoreCase))
                {
                    embed = new EmbedModel() { IsEmpty = true };
                    return;
                }

                iframeuri = Rx.Match(product, "src=\"(https?://[^\"]+)\"");

            }, statusCodeOK: false);

            if (string.IsNullOrWhiteSpace(iframeuri))
                return null;


            await httpHydra.GetSpan(iframeuri, content =>
            {
                if (!content.Contains("new Playerjs", StringComparison.Ordinal))
                    return;

                if (!Regex.IsMatch(content, "file:([\t ]+)?'\\[\\{"))
                {
                    var rx = Rx.Split("new Playerjs", content);
                    if (1 > rx.Count)
                        return;

                    embed = new EmbedModel()
                    {
                        content = rx[1].ToString()
                    };

                    return;
                }
                else
                {
                    try
                    {
                        var root = JsonSerializer.Deserialize<Voice[]>(Rx.Match(content, "file:([\t ]+)?'([^\n\r]+)',", 2));
                        if (root != null && root.Length > 0)
                            embed = new EmbedModel() { serial = root };
                    }
                    catch { }
                }
            });



            if (embed == null)
                requesterror?.Invoke();

            return embed;
        }
        #endregion

        #region Tpl
        public ITplResult Tpl(EmbedModel md, long kinopoisk_id, string title, string original_title, int t, int s, VastConf vast = null, bool rjson = false, string mybaseurl = null)
        {
            if (md == null || md.IsEmpty || (string.IsNullOrEmpty(md.content) && md.serial == null))
                return default;

            string fixStream(string _l) => _l.Replace("0yql3tj", "oyql3tj");

            if (md.content != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, 1);

                string hls = Regex.Match(md.content, "file:([\t ]+)?(\"|')([\t ]+)?(?<hls>https?://[^\"'\n\r\t ]+/index.m3u8)").Groups["hls"].Value;
                if (string.IsNullOrEmpty(hls))
                    return default;

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

                return mtpl;
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
                        var hashseason = new HashSet<string>(20);

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

                        return tpl;
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

                        var etpl = new EpisodeTpl(vtpl, episodes.Length);

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

                        return etpl;
                    }
                }
                catch
                {
                    return default;
                }
                #endregion
            }
        }
        #endregion
    }
}
