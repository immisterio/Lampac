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

namespace Ashdi;

public struct AshdiInvoke
{
    #region AshdiInvoke
    string host;
    HttpHydra httpHydra;
    Func<string, string> onstreamfile;

    public AshdiInvoke(string host, HttpHydra httpHydra, Func<string, string> onstreamfile)
    {
        this.host = host != null ? $"{host}/" : null;
        this.httpHydra = httpHydra;
        this.onstreamfile = onstreamfile;
    }
    #endregion

    #region Embed
    public async Task<EmbedModel> Embed(string iframeUri)
    {
        EmbedModel result = null;

        await httpHydra.GetSpan(iframeUri, content =>
        {
            if (!content.Contains("new Playerjs", StringComparison.Ordinal))
                return;

            if (Regex.IsMatch(content, "file:([\t ]+)?'\\[\\{"))
            {
                try
                {
                    ReadOnlySpan<char> json = Rx.Slice(content, "file:", "',")
                        .TrimStart()
                        .Slice(1);

                    var root = JsonSerializer.Deserialize<Voice[]>(json, new JsonSerializerOptions
                    {
                        AllowTrailingCommas = true
                    });

                    if (root != null && root.Length > 0)
                        result = new EmbedModel() { serial = root };
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "{Class} {CatchId}", "Ashdi", "id_4egjgt5u");
                }
            }
            else
            {
                string file = Rx.Slice(content, "file:", "\n").ToString();
                string hls = Regex.Match(file, "(https?://[^\"'\n\r\t ]+/index.m3u8)").Groups[1].Value;

                if (!string.IsNullOrEmpty(hls))
                {
                    List<Cc> subs = null;
                    string subtitle = Rx.Match(content, "subtitle(\")?:\"([^\"]+)\"", 2);

                    if (subtitle != null)
                    {
                        var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(subtitle);

                        if (match.Success)
                            subs = new List<Cc>(match.Length);

                        while (match.Success)
                        {
                            subs.Add(new Cc()
                            {
                                name = match.Groups[1].Value,
                                url = match.Groups[2].Value
                            });

                            match = match.NextMatch();
                        }
                    }

                    result = new EmbedModel()
                    {
                        movie = new Movie()
                        {
                            hls = hls,
                            subs = subs
                        }
                    };
                }
            }
        });

        return result;
    }
    #endregion

    #region Tpl
    public ITplResult Tpl(EmbedModel md, string uri, string title, string original_title, short t, short s, VastConf vast = null, bool rjson = false)
    {
        if (md == null || md.IsEmpty || (md.movie == null && md.serial == null))
            return default;

        string fixStream(string _l)
        {
            if (_l.Contains("0yql3tj"))
                return _l.Replace("0yql3tj", "oyql3tj");

            return _l;
        }

        if (md.movie != null)
        {
            #region Фильм
            var mtpl = new MovieTpl(title, original_title, 1);

            SubtitleTpl subtitles = null;

            if (md.movie.subs != null)
            {
                subtitles = new SubtitleTpl(md.movie.subs.Count);

                foreach (var sub in md.movie.subs)
                    subtitles.Append(sub.name, onstreamfile.Invoke(fixStream(sub.url)));
            }

            mtpl.Append(
                "По умолчанию",
                onstreamfile.Invoke(fixStream(md.movie.hls)),
                subtitles: subtitles,
                vast: vast
            );

            return mtpl;
            #endregion
        }
        else
        {
            #region Сериал
            try
            {
                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);
                string enc_uri = HttpUtility.UrlEncode(uri);

                if (s == -1)
                {
                    var tpl = new SeasonTpl();
                    var hashseason = new HashSet<string>(20);

                    foreach (var voice in md.serial)
                    {
                        foreach (var season in voice.folder)
                        {
                            string numberseason = Regex.Match(season.title, "([0-9]+)$").Groups[1].Value;
                            if (string.IsNullOrEmpty(numberseason))
                                continue;

                            if (hashseason.Add(numberseason))
                            {
                                tpl.Append(
                                    season.title,
                                    host + $"lite/ashdi?rjson={rjson}&title={enc_title}&original_title={enc_original_title}&uri={enc_uri}&s={numberseason}",
                                    numberseason
                                );
                            }
                        }
                    }

                    return tpl;
                }
                else
                {
                    #region Перевод
                    var vtpl = new VoiceTpl();

                    for (short i = 0; i < md.serial.Length; i++)
                    {
                        if (md.serial[i].folder?.FirstOrDefault(i => i.title.EndsWith($" {s}")) == null)
                            continue;

                        if (t == -1)
                            t = i;

                        vtpl.Append(
                            md.serial[i].title,
                            t == i,
                            host + $"lite/ashdi?rjson={rjson}&title={enc_title}&original_title={enc_original_title}&uri={enc_uri}&s={s}&t={i}"
                        );
                    }
                    #endregion

                    string sArch = s.ToString();
                    var episodes = md.serial[t].folder.First(i => i.title.EndsWith($" {s}")).folder;

                    var etpl = new EpisodeTpl(vtpl, episodes.Length);

                    foreach (var episode in episodes)
                    {
                        #region subtitle
                        SubtitleTpl subtitles = null;

                        if (!string.IsNullOrEmpty(episode.subtitle))
                        {
                            var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(episode.subtitle);

                            if (match.Success)
                                subtitles = new SubtitleTpl(match.Length);

                            while (match.Success)
                            {
                                subtitles.Append(match.Groups[1].Value, onstreamfile.Invoke(fixStream(match.Groups[2].Value)));
                                match = match.NextMatch();
                            }
                        }
                        #endregion

                        etpl.Append(
                            episode.title,
                            title ?? original_title,
                            s,
                            Regex.Match(episode.title, "([0-9]+)$").Groups[1].Value,
                            onstreamfile.Invoke(fixStream(episode.file)),
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
