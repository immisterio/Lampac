using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.Services;
using Shared.Services.RxEnumerate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace HdvbUA;

public struct HdvbUAInvoke
{
    #region HdvbUAInvoke
    string host;
    HttpHydra httpHydra;
    Func<string, string> onstreamfile;

    public HdvbUAInvoke(string host, HttpHydra httpHydra, Func<string, string> onstreamfile)
    {
        this.host = host != null ? $"{host}/" : null;
        this.httpHydra = httpHydra;
        this.onstreamfile = onstreamfile;
    }
    #endregion

    #region Embed
    public async Task<EmbedModel> Embed(string iframeUri)
    {
        var result = new EmbedModel();

        await httpHydra.GetSpan(iframeUri, content =>
        {
            if (!content.Contains("file:", StringComparison.Ordinal))
                return;

            if (Regex.IsMatch(content, "file: ?'\\["))
            {
                try
                {
                    ReadOnlySpan<char> json = Rx.Slice(content, "file: '", "',");

                    var root = JsonSerializer.Deserialize<List<Voice>>(json, new JsonSerializerOptions
                    {
                        AllowTrailingCommas = true
                    });

                    if (root != null && root.Count > 0)
                        result.serial = root;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "{Class} {CatchId}", "HdvbUA", "id_vq7wnuwy");
                }
            }
            else
            {
                string hls = Rx.Match(content, "file: ?\"(https?://[^\"]+/index.m3u8)\"");
                if (!string.IsNullOrWhiteSpace(hls))
                {
                    result.movie = new Movie()
                    {
                        hls = hls
                    };

                    string subtitle = Rx.Match(content, "subtitle: ?\"([^\"]+)\"");

                    if (!string.IsNullOrEmpty(subtitle))
                    {
                        result.movie.subs = new List<Cc>();

                        var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(subtitle);
                        while (match.Success)
                        {
                            result.movie.subs.Add(new Cc()
                            {
                                name = match.Groups[1].Value,
                                url = match.Groups[2].Value
                            });

                            match = match.NextMatch();
                        }
                    }
                }
            }
        });

        if (result.serial == null && result.movie == null)
            return null;

        return result;
    }
    #endregion

    #region Tpl
    public ITplResult Tpl(EmbedModel result, string title, string original_title, short t, short s, string uri, VastConf vast = null, bool rjson = false)
    {
        if (result == null || result.IsEmpty)
            return default;

        if (result.movie != null)
        {
            #region Фильм
            var mtpl = new MovieTpl(title, original_title);

            SubtitleTpl subtitles = null;

            if (result.movie.subs != null)
            {
                subtitles = new SubtitleTpl(result.movie.subs.Count);

                foreach (var sub in result.movie.subs)
                    subtitles.Append(sub.name, onstreamfile.Invoke(sub.url));
            }

            mtpl.Append(
                "По умолчанию",
                onstreamfile.Invoke(result.movie.hls),
                subtitles: subtitles,
                vast: vast
            );

            return mtpl;
            #endregion
        }
        else
        {
            #region Сериал
            string enc_uri = HttpUtility.UrlEncode(uri);
            string enc_title = HttpUtility.UrlEncode(title);
            string enc_original_title = HttpUtility.UrlEncode(original_title);

            try
            {
                if (s == -1)
                {
                    #region Сезоны
                    var tpl = new SeasonTpl();

                    foreach (var season in result.serial)
                    {
                        string numberseason = Regex.Match(season.title, "^([0-9]+)").Groups[1].Value;
                        if (string.IsNullOrEmpty(numberseason))
                            continue;

                        tpl.Append(
                            season.title,
                            host + $"lite/hdvbua?rjson={rjson}&title={enc_title}&original_title={enc_original_title}&uri={enc_uri}&s={numberseason}",
                            numberseason
                        );
                    }

                    return tpl;
                    #endregion
                }
                else
                {
                    var season = result.serial.FirstOrDefault(i => i.title.StartsWith($"{s} "));
                    if (season?.folder == null || season.folder.Length == 0)
                        return default;

                    #region Перевод
                    var vtpl = new VoiceTpl();
                    var hash = new HashSet<string>();

                    for (short i = 0; i < season.folder.Length; i++)
                    {
                        if (hash.Add(season.folder[i].title))
                        {
                            if (t == -1)
                                t = i;

                            vtpl.Append(
                                season.folder[i].title,
                                t == i,
                                host + $"lite/hdvbua?rjson={rjson}&title={enc_title}&original_title={enc_original_title}&uri={enc_uri}&s={s}&t={i}"
                            );
                        }
                    }
                    #endregion

                    var episodes = season.folder[t].folder;

                    var etpl = new EpisodeTpl(vtpl, episodes.Length);

                    foreach (var episode in episodes)
                    {
                        #region subtitle
                        SubtitleTpl subtitles = null;

                        if (!string.IsNullOrEmpty(episode.subtitle))
                        {
                            var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(episode.subtitle);
                            subtitles = new SubtitleTpl(match.Length);

                            while (match.Success)
                            {
                                subtitles.Append(match.Groups[1].Value, onstreamfile.Invoke(match.Groups[2].Value));
                                match = match.NextMatch();
                            }
                        }
                        #endregion

                        etpl.Append(
                            episode.title,
                            title ?? original_title,
                            s,
                            Regex.Match(episode.title, "^([0-9]+)").Groups[1].Value,
                            onstreamfile.Invoke(episode.file),
                            subtitles: subtitles,
                            vast: vast
                        );
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
