using Shared.Models.Templates;
using Shared.Services.Pools;
using Shared.Services.RxEnumerate;
using Shared.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

        string file = decodePlayer(html);
        if (file == null)
            return null;

        var pl = JsonNode.Parse(file)?["file"]?.Deserialize<RootObject[]>();
        if (pl == null || pl.Length == 0)
            return null;

        string quality = file.Contains("2160p") ? "2160p" : file.Contains("1080p") ? "1080p" : file.Contains("720p") ? "720p" : "480p";
        return new EmbedModel() { pl = pl, movie = !file.Contains("\"folder\":"), quality = quality };
    }


    static string decodePlayer(ReadOnlySpan<char> _html)
    {
        try
        {
            string base64 = Rx.Match(_html, "new Player\\(\".{73}([^\n\r]+)\"\\);");
            if (base64 == null)
                return null;

            if (Regex.IsMatch(base64, "//[^=]+="))
                base64 = Regex.Replace(base64, "//[^=]+=", "");

            using (var nbuf = new BufferBytePool(Encoding.UTF8.GetMaxByteCount(base64.Length)))
            {
                if (Convert.TryFromBase64String(base64, nbuf.Span, out int bytesWritten))
                    return Encoding.UTF8.GetString(nbuf.Span.Slice(0, bytesWritten));

                return null;
            }
        }
        catch
        {
            return null;
        }
    }
    #endregion

    #region Html
    public ITplResult Tpl(EmbedModel root, string args, string uri, string title, string original_title, string t, int s, int sid, bool rjson, bool rhub = false)
    {
        if (root?.pl == null || root.pl.Length == 0)
            return default;

        if (!string.IsNullOrEmpty(args))
            args = $"&{args.Remove(0, 1)}";

        string enc_title = HttpUtility.UrlEncode(title);
        string enc_original_title = HttpUtility.UrlEncode(original_title);

        if (root.movie)
        {
            #region Фильм
            var mtpl = new MovieTpl(title, original_title, root.pl.Length);

            foreach (var pl in root.pl)
            {
                string name = pl.title;
                string file = pl.file;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                    continue;

                #region streams
                var streams = new List<(string link, string quality)>(7);

                foreach (Match m in Regex.Matches(file, $"\\[(Авто|2160|1440|1080|720|480|360)p?\\]([^\"\\,\\[ ]+)"))
                {
                    string link = m.Groups[2].Value;
                    if (string.IsNullOrEmpty(link))
                        continue;

                    string quality = file.Contains("2160p") ? "2160" : file.Contains("1080p") ? "1080" : file.Contains("720p") ? "720" : "480";
                    streams.Add((host + $"lite/videodb/manifest.m3u8?link={CrypTo.EncryptQuery(link)}{args}", quality));
                }

                if (streams.Count == 0)
                    continue;

                streams.Reverse();
                #endregion

                if (rhub)
                {
                    mtpl.Append(
                        name,
                        streams[0].link.Replace("/manifest.m3u8", "/manifest"),
                        "call"
                    );
                }
                else
                {
                    mtpl.Append(
                        name,
                        streams[0].link,
                        quality: streams[0].quality
                    );
                }
            }

            return mtpl;
            #endregion
        }
        else
        {
            #region Сериал
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

                string sArhc = s.ToString();

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
                        if (!hashvoices.Contains(perevod))
                        {
                            hashvoices.Add(perevod);
                            vtpl.Append(
                                perevod,
                                t == perevod,
                                host + $"lite/videodb?rjson={rjson}&uri={HttpUtility.UrlEncode(uri)}&title={enc_title}&original_title={enc_original_title}&s={s}&sid={sid}&t={HttpUtility.UrlEncode(perevod)}"
                            );
                        }
                        #endregion

                        if (perevod != t)
                            continue;

                        string name = episode.title;
                        string file = pl.file;

                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                            continue;

                        var streamquality = new StreamQualityTpl();

                        foreach (Match m in Regex.Matches(file, $"\\[(1080|720|480|360)p?\\]([^\"\\,\\[ ]+)"))
                        {
                            string link = m.Groups[2].Value;
                            if (string.IsNullOrEmpty(link))
                                continue;

                            streamquality.Insert(host + $"lite/videodb/manifest.m3u8?serial=true&link={CrypTo.EncryptQuery(link)}{args}", $"{m.Groups[1].Value}p");
                        }

                        if (!streamquality.Any())
                            continue;

                        if (rhub)
                        {
                            string streamlink = rhub ? streamquality.Firts().link : null;

                            if (streamlink != null)
                            {
                                etpl.Append(
                                    name,
                                    title ?? original_title,
                                    sArhc,
                                    Regex.Match(name,
                                    "^([0-9]+)").Groups[1].Value,
                                    streamquality.Firts().link.Replace("/manifest.m3u8",
                                    "/manifest"),
                                    "call",
                                    streamlink: streamlink
                                );
                            }
                        }
                        else
                        {
                            etpl.Append(
                                name,
                                title ?? original_title,
                                sArhc,
                                Regex.Match(name,
                                "^([0-9]+)").Groups[1].Value,
                                streamquality.Firts().link,
                                streamquality: streamquality
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
