using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace VoKino;

public struct VoKinoInvoke
{
    #region VoKinoInvoke
    string host;
    string apihost;
    string token;
    HttpHydra httpHydra;
    Func<string, string> onstreamfile;

    public VoKinoInvoke(string host, string apihost, string token, HttpHydra httpHydra, Func<string, string> onstreamfile)
    {
        this.host = host != null ? $"{host}/" : null;
        this.apihost = apihost;
        this.token = token;
        this.httpHydra = httpHydra;
        this.onstreamfile = onstreamfile;
    }
    #endregion

    #region Embed
    public async Task<EmbedModel> Embed(string origid, long kinopoisk_id, string balancer, string t)
    {
        try
        {
            if (string.IsNullOrEmpty(balancer))
            {
                var json = await httpHydra.Get<JsonElement>($"{apihost}/v2/view/{origid ?? kinopoisk_id.ToString()}?token={token}", safety: true, textJson: true);
                var online = json.GetProperty("online").EnumerateObject();
                if (!online.Any())
                    return new EmbedModel() { IsEmpty = true };

                var similars = new List<Similar>(10);

                foreach (var item in online)
                {
                    string playlistUrl = item.Value.GetProperty("playlist_url").GetString();
                    if (string.IsNullOrEmpty(playlistUrl))
                        continue;

                    var model = new Similar()
                    {
                        title = item.Name,
                        balancer = Regex.Match(playlistUrl, "/v2/online/([^/]+)/").Groups[1].Value
                    };

                    if (item.Name == "Vokino")
                        similars.Insert(0, model);
                    else
                        similars.Add(model);
                }

                return new EmbedModel() { similars = similars };
            }
            else
            {
                string uri = $"{apihost}/v2/online/{balancer}/{origid ?? kinopoisk_id.ToString()}?token={token}";
                if (!string.IsNullOrEmpty(t))
                    uri += $"&{t}";

                var root = await httpHydra.Get<RootObject>(uri, safety: true, textJson: true);
                if (root?.channels == null || root.channels.Length == 0)
                    return new EmbedModel() { IsEmpty = true };

                return new EmbedModel() { menu = root.menu, channels = root.channels };
            }
        }
        catch (System.Exception ex)
        {
            Serilog.Log.Error(ex, "{Class} {CatchId}", "VoKino", "id_tfj170h1");
        }

        return null;
    }
    #endregion

    #region Tpl
    public ITplResult Tpl(EmbedModel result, string origid, long kinopoisk_id, string title, string original_title, string balancer, string t, int s, VastConf vast = null, bool rjson = false)
    {
        if (result == null || result.IsEmpty)
            return default;

        string enc_title = HttpUtility.UrlEncode(title);
        string enc_original_title = HttpUtility.UrlEncode(original_title);

        #region similar
        if (result.similars != null)
        {
            var stpl = new SimilarTpl(result.similars.Count);

            foreach (var similar in result.similars)
            {
                string link = host + $"lite/vokino?rjson={rjson}&origid={origid}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&balancer={similar.balancer}";

                stpl.Append(
                    similar.title,
                    string.Empty,
                    string.Empty,
                    link
                );
            }

            return stpl;
        }
        #endregion

        if (result?.channels == null || result.channels.Length == 0)
            return default;

        #region Переводы
        var voices = result?.menu?.FirstOrDefault(i => i.title == "Перевод")?.submenu;
        var vtpl = new VoiceTpl(voices != null ? voices.Length : 0);

        if (voices != null && voices.Length > 0)
        {
            foreach (var translation in voices)
            {
                if (translation.playlist_url != null && translation.playlist_url.Contains("?"))
                {
                    string _t = HttpUtility.UrlEncode(translation.playlist_url.Split("?")[1]);
                    vtpl.Append(
                        translation.title,
                        translation.selected,
                        host + $"lite/vokino?rjson={rjson}&origid={origid}&kinopoisk_id={kinopoisk_id}&balancer={balancer}&title={enc_title}&original_title={enc_original_title}&t={_t}&s={s}"
                    );
                }
            }
        }
        #endregion

        if (result.channels.First().playlist_url == "submenu")
        {
            if (s == -1)
            {
                var tpl = new SeasonTpl(quality: result.channels[0].quality_full?.Replace("2160p.", "4K "), result.channels.Length);

                foreach (var ch in result.channels)
                {
                    string sname = Regex.Match(ch.title, "^([0-9]+)").Groups[1].Value;
                    if (string.IsNullOrEmpty(sname))
                        sname = Regex.Match(ch.title, "([0-9]+)$").Groups[1].Value;

                    tpl.Append(
                        ch.title,
                        host + $"lite/vokino?rjson={rjson}&origid={origid}&kinopoisk_id={kinopoisk_id}&balancer={balancer}&title={enc_title}&original_title={enc_original_title}&t={t}&s={sname}",
                        sname
                    );
                }

                return tpl;
            }
            else
            {
                var series = result.channels.First(i => i.title.StartsWith($"{s} ") || i.title.EndsWith($" {s}")).submenu;

                var etpl = new EpisodeTpl(vtpl, series.Length);

                foreach (var e in series)
                {
                    etpl.Append(
                        e.title,
                        title ?? original_title,
                        s.ToString(),
                        Regex.Match(e.ident, "([0-9]+)$").Groups[1].Value,
                        onstreamfile(e.stream_url),
                        vast: vast
                    );
                }

                return etpl;
            }
        }
        else
        {
            var mtpl = new MovieTpl(title, original_title, vtpl, result.channels.Length);

            foreach (var ch in result!.channels)
            {
                string name = ch.quality_full;
                if (!string.IsNullOrWhiteSpace(name.Replace("2160p.", "")))
                {
                    name = name.Replace("2160p.", "4K ");

                    if (ch.extra != null && ch.extra.TryGetValue("size", out string size) && !string.IsNullOrEmpty(size))
                        name += $" - {size}";
                }

                mtpl.Append(
                    name,
                    onstreamfile(ch.stream_url),
                    vast: vast
                );
            }

            return mtpl;
        }
    }
    #endregion
}
