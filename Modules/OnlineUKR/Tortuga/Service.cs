using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.Services;
using Shared.Services.RxEnumerate;
using Shared.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Tortuga;

public struct TortugaInvoke
{
    #region TortugaInvoke
    string host;
    HttpHydra http;
    Func<string, string> onstreamfile;

    public TortugaInvoke(string host, HttpHydra httpHydra, Func<string, string> onstreamfile)
    {
        this.host = host != null ? $"{host}/" : null;
        http = httpHydra;
        this.onstreamfile = onstreamfile;
    }
    #endregion

    #region Embed
    public async Task<EmbedModel> Embed(string iframeUri)
    {
        var result = new EmbedModel();

        await http.GetSpan(iframeUri, content =>
        {
            if (!content.Contains("file:", StringComparison.Ordinal))
                return;

            string file = Rx.Match(content, "file: ?.([^'\"]+)==('|\")");
            string decoded = null; 
            
            CrypTo.DecodeBase64(file, base64 =>
            {
                decoded = string.Create(base64.Length, base64, static (span, src) =>
                {
                    for (int i = 0; i < src.Length; i++)
                        span[i] = src[src.Length - 1 - i];
                });
            });

            if (decoded == null)
                return;

            if (decoded.StartsWith("http"))
            {
                result.hls = decoded;
            }
            else
            {
                try
                {
                    var root = JsonSerializer.Deserialize<List<Voice>>(decoded, new JsonSerializerOptions
                    {
                        AllowTrailingCommas = true
                    });

                    if (root != null && root.Count > 0)
                        result.serial = root;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "{Class} {CatchId}", "Tortuga", "id_frqb3um1");
                }
            }
        });

        if (string.IsNullOrEmpty(result.hls) && result.serial == null)
            return null;

        return result;
    }
    #endregion

    #region Tpl
    public ITplResult Tpl(EmbedModel result, string title, string original_title, string t, int s, string uri, VastConf vast = null, bool rjson = false)
    {
        if (result == null || result.IsEmpty)
            return default;

        if (result.hls != null)
        {
            var mtpl = new MovieTpl(title, original_title, 1);

            mtpl.Append(
                "По умолчанию",
                onstreamfile.Invoke(result.hls),
                vast: vast
            );

            return mtpl;
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
                    #region Сезоны
                    var tpl = new SeasonTpl();

                    foreach (var season in result.serial)
                    {
                        tpl.Append(
                            season.title,
                            host + $"lite/tortuga?rjson={rjson}&title={enc_title}&original_title={enc_original_title}&uri={enc_uri}&s={season.season}",
                            season.season
                        );
                    }

                    return tpl;
                    #endregion
                }
                else
                {
                    string sArhc = s.ToString();
                    var episodes = result.serial.FirstOrDefault(i => i.season == sArhc).folder;
                    if (episodes == null || episodes.Length == 0)
                        return default;

                    #region Перевод
                    var vtpl = new VoiceTpl();
                    var hashVoice = new HashSet<string>(20);

                    foreach (var episode in episodes)
                    {
                        foreach (var voice in episode.folder)
                        {
                            if (hashVoice.Add(voice.title))
                            {
                                if (string.IsNullOrEmpty(t))
                                    t = voice.title;

                                vtpl.Append(
                                    voice.title,
                                    t == voice.title,
                                    host + $"lite/tortuga?rjson={rjson}&title={enc_title}&original_title={enc_original_title}&uri={enc_uri}&s={s}&t={voice.title}"
                                );
                            }
                        }
                    }
                    #endregion

                    var etpl = new EpisodeTpl(vtpl, episodes.Length);

                    foreach (var episode in episodes)
                    {
                        var video = episode.folder.FirstOrDefault(i => i.title == t);
                        if (video?.file == null)
                            continue;

                        #region subtitle
                        var subtitles = new SubtitleTpl();

                        if (!string.IsNullOrEmpty(video.subtitle))
                        {
                            var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(video.subtitle);
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
                            sArhc,
                            episode.number,
                            onstreamfile.Invoke(video.file),
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
