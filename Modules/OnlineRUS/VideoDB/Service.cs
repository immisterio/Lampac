using Shared.Models.Templates;
using Shared.Services.RxEnumerate;
using Shared.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace VideoDB;

public struct VideoDBInvoke
{
    #region VideoDBInvoke
    string host;

    public VideoDBInvoke(string host)
    {
        this.host = host != null ? $"{host}/" : null;
    }
    #endregion

    #region Embed
    public EmbedModel Embed(ReadOnlySpan<char> html)
    {
        if (html.IsEmpty)
            return null;

        RootNode root = null;

        string base64 = Rx.Slice(html, "new Player(\"", "\");")
            .Slice(73)
            .ToString();

        if (string.IsNullOrEmpty(base64))
            return null;

        if (Regex.IsMatch(base64, "//[^=]+="))
            base64 = Regex.Replace(base64, "//[^=]+=", "");

        bool ismovie = false;
        string quality = null;

        CrypTo.DecodeBase64(base64, json =>
        {
            ismovie = !json.Contains("\"folder\":", StringComparison.Ordinal);

            quality = json.Contains("2160p", StringComparison.Ordinal)
                ? "2160p"
                : json.Contains("1080p", StringComparison.Ordinal)
                    ? "1080p"
                    : json.Contains("720p", StringComparison.Ordinal)
                        ? "720p"
                        : "480p";

            root = JsonSerializer.Deserialize<RootNode>(json, new JsonSerializerOptions
            {
                AllowTrailingCommas = true
            });
        });

        var pl = root?.file;
        if (pl == null || pl.Length == 0)
            return null;

        return new EmbedModel()
        {
            pl = pl,
            movie = ismovie,
            quality = quality
        };
    }
    #endregion

    #region Html
    public ITplResult Tpl(EmbedModel root, string args, string uri, string title, string original_title, string t, short s, short sid, bool rjson, bool rhub = false)
    {
        if (root?.pl == null || root.pl.Length == 0)
            return default;

        if (!string.IsNullOrEmpty(args))
            args = $"&{args.Remove(0, 1)}";

        if (root.movie)
        {
            #region Фильм
            var mtpl = new MovieTpl(title, original_title, root.pl.Length);

            foreach (var pl in root.pl)
            {
                if (string.IsNullOrWhiteSpace(pl.title))
                    continue;

                if (pl.streams == null)
                {
                    string file = pl.file;
                    if (string.IsNullOrWhiteSpace(file))
                        continue;

                    pl.streams = new List<StreamQualityDto>(5);

                    foreach (Match m in Regex.Matches(file, $"\\[(Авто|2160|1440|1080|720|480|360)p?\\]([^\"\\,\\[ ]+)"))
                    {
                        string link = m.Groups[2].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        pl.streams.Add(new(
                            host + $"lite/videodb/manifest.m3u8?link={AesTo.Encrypt(link)}",
                            file.Contains("2160p") ? "2160" : file.Contains("1080p") ? "1080" : file.Contains("720p") ? "720" : "480"
                        ));
                    }

                    pl.streams.Reverse();
                }

                if (pl.streams.Count == 0)
                    continue;

                if (rhub)
                {
                    mtpl.Append(
                        pl.title,
                        pl.streams[0].link.Replace("/manifest.m3u8", "/manifest"),
                        "call"
                    );
                }
                else
                {
                    mtpl.Append(
                        pl.title,
                        pl.streams[0].link + args,
                        quality: pl.streams[0].quality
                    );
                }
            }

            return mtpl;
            #endregion
        }
        else
        {
            #region Сериал
            string enc_title = HttpUtility.UrlEncode(title);
            string enc_original_title = HttpUtility.UrlEncode(original_title);

            if (s == -1)
            {
                var tpl = new SeasonTpl(root.quality, root.pl.Length);

                for (int i = 0; i < root.pl.Length; i++)
                {
                    string name = root.pl?[i].title;
                    if (name == null)
                        continue;

                    string season = Regex.Match(name, "^([0-9]+)").Groups[1].Value;
                    if (string.IsNullOrEmpty(season))
                        continue;

                    tpl.Append(
                        name,
                        host + $"lite/videodb?rjson={rjson}&uri={HttpUtility.UrlEncode(uri)}&rjson={rjson}&title={enc_title}&original_title={enc_original_title}&s={season}&sid={i}",
                        season
                    );
                }

                return tpl;
            }
            else
            {
                var season = root.pl[sid].folder;
                if (season == null)
                    return default;

                var vtpl = new VoiceTpl();
                var etpl = new EpisodeTpl();

                var hashvoices = new HashSet<string>(20);

                foreach (var episode in season)
                {
                    var episodes = episode.folder;
                    if (episodes == null || episodes.Length == 0)
                        continue;

                    foreach (var pl in episodes)
                    {
                        string perevod = Regex.Replace(pl.title ?? "", "^[a-zA-Z]{3} \\| ", "");
                        if (!string.IsNullOrEmpty(perevod) && string.IsNullOrEmpty(t))
                            t = perevod;

                        #region Переводы
                        if (hashvoices.Add(perevod))
                        {
                            vtpl.Append(
                                perevod,
                                t == perevod,
                                host + $"lite/videodb?rjson={rjson}&uri={HttpUtility.UrlEncode(uri)}&title={enc_title}&original_title={enc_original_title}&s={s}&sid={sid}&t={HttpUtility.UrlEncode(perevod)}"
                            );
                        }
                        #endregion

                        if (perevod != t)
                            continue;

                        if (string.IsNullOrWhiteSpace(episode.title))
                            continue;

                        if (pl.streams == null)
                        {
                            string file = pl.file;
                            if (string.IsNullOrWhiteSpace(file))
                                continue;

                            pl.streams = new List<StreamQualityDto>(5);

                            foreach (Match m in Regex.Matches(file, $"\\[(1080|720|480|360)p?\\]([^\"\\,\\[ ]+)"))
                            {
                                string link = m.Groups[2].Value;
                                if (string.IsNullOrEmpty(link))
                                    continue;

                                pl.streams.Add(new(
                                    host + $"lite/videodb/manifest.m3u8?serial=true&link={AesTo.Encrypt(link)}",
                                    $"{m.Groups[1].Value}p"
                                ));
                            }

                            pl.streams.Reverse();
                        }

                        if (pl.streams.Count == 0)
                            continue;

                        if (rhub)
                        {
                            string streamlink = rhub ? pl.streams[0].link : null;

                            if (streamlink != null)
                            {
                                etpl.Append(
                                    episode.title,
                                    title ?? original_title,
                                    s,
                                    Regex.Match(episode.title, "^([0-9]+)").Groups[1].Value,
                                    streamlink.Replace("/manifest.m3u8", "/manifest"),
                                    "call",
                                    streamlink: streamlink + args
                                );
                            }
                        }
                        else
                        {
                            etpl.Append(
                                episode.title,
                                title ?? original_title,
                                s,
                                Regex.Match(episode.title, "^([0-9]+)").Groups[1].Value,
                                pl.streams[0].link + args,
                                streamquality: new StreamQualityTpl(pl.streams, linkPredicate: link => link + args)
                            );
                        }
                    }
                }

                etpl.Append(vtpl);

                return etpl;
            }
            #endregion
        }
    }
    #endregion
}
