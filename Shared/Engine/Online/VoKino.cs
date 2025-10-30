using Newtonsoft.Json.Linq;
using Shared.Models.Base;
using Shared.Models.Online.Settings;
using Shared.Models.Online.VoKino;
using Shared.Models.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public struct VoKinoInvoke
    {
        #region VoKinoInvoke
        string host;
        string apihost;
        string token;
        Func<string, ValueTask<string>> onget;
        Func<string, string> onstreamfile;
        Func<string, string> onlog;
        Action requesterror;

        public VoKinoInvoke(string host, string apihost, string token, Func<string, ValueTask<string>> onget, Func<string, string> onstreamfile, Func<string, string> onlog = null, Action requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.token = token;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.requesterror = requesterror;
        }
        #endregion

        public static void SendOnline(VokinoSettings init, List<(dynamic init, string name, string url, string plugin, int index)> online, JObject view)
        {
            var on = init.online;

            void send(string name, int x)
            {
                string url = "{localhost}/lite/vokino?balancer=" + name.ToLower();

                string displayname = $"{init.displayname ?? "VoKino"}";
                if (name != "VoKino")
                    displayname = $"{name} ({init.displayname ?? "VoKino"})";

                if (init.onlyBalancerName)
                    displayname = name;

                online.Add((init, displayname, url, (name == "VoKino" ? "vokino" : $"vokino-{name.ToLower()}"), init.displayindex > 0 ? (init.displayindex + x) : online.Count));
            }

            if (on.vokino && (view == null || view.ContainsKey("Vokino")))
                send("VoKino", 1);

            if (on.filmix && (view == null || view.ContainsKey("Filmix")))
                send("Filmix", 2);

            if (on.alloha && (view == null || view.ContainsKey("Alloha")))
                send("Alloha", 3);

            if (on.vibix && (view == null || view.ContainsKey("Vibix")))
                send("Vibix", 4);

            if (on.monframe && (view == null || view.ContainsKey("MonFrame")))
                send("MonFrame", 5);

            if (on.remux && (view == null || view.ContainsKey("Remux")))
                send("Remux", 6);

            if (on.ashdi && (view == null || view.ContainsKey("Ashdi")))
                send("Ashdi", 7);

            if (on.hdvb && (view == null || view.ContainsKey("Hdvb")))
                send("HDVB", 8);
        }

        #region Embed
        public async ValueTask<EmbedModel> Embed(string origid, long kinopoisk_id, string balancer, string t)
        {
            try
            {
                if (string.IsNullOrEmpty(balancer))
                {
                    string json = await onget($"{apihost}/v2/view/{origid ?? kinopoisk_id.ToString()}?token={token}");
                    if (json == null)
                    {
                        requesterror?.Invoke();
                        return null;
                    }

                    if (json.Contains("not found"))
                        return new EmbedModel() { IsEmpty = true };

                    var similars = new List<Similar>(10);

                    foreach (var item in JsonSerializer.Deserialize<JsonElement>(json).GetProperty("online").EnumerateObject())
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

                    string json = await onget(uri);
                    if (json == null)
                    {
                        requesterror?.Invoke();
                        return null;
                    }

                    if (json.Contains("not found"))
                        return new EmbedModel() { IsEmpty = true };

                    var root = JsonSerializer.Deserialize<RootObject>(json);
                    if (root?.channels == null || root.channels.Length == 0)
                        return null;

                    return new EmbedModel() { menu = root.menu, channels = root.channels };
                }
            }
            catch { }

            return null;
        }
        #endregion

        #region Html
        public string Html(EmbedModel result, string origid, long kinopoisk_id, string title, string original_title, string balancer, string t, int s, VastConf vast = null, bool rjson = false)
        {
            if (result == null || result.IsEmpty)
                return string.Empty;

            string enc_title = HttpUtility.UrlEncode(title);
            string enc_original_title = HttpUtility.UrlEncode(original_title);

            #region similar
            if (result.similars != null)
            {
                var stpl = new SimilarTpl(result.similars.Count);

                foreach (var similar in result.similars)
                {
                    string link = host + $"lite/vokino?rjson={rjson}&origid={origid}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&balancer={similar.balancer}";

                    stpl.Append(similar.title, string.Empty, string.Empty, link);
                }

                return rjson ? stpl.ToJson() : stpl.ToHtml();
            }
            #endregion

            if (result?.channels == null || result.channels.Length == 0)
                return string.Empty;

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
                        vtpl.Append(translation.title, translation.selected, host + $"lite/vokino?rjson={rjson}&origid={origid}&kinopoisk_id={kinopoisk_id}&balancer={balancer}&title={enc_title}&original_title={enc_original_title}&t={_t}&s={s}");
                    }
                }
            }
            #endregion

            if (result!.channels.First().playlist_url == "submenu")
            {
                if (s == -1)
                {
                    var tpl = new SeasonTpl(quality: result.channels[0].quality_full?.Replace("2160p.", "4K "), result.channels.Length);

                    foreach (var ch in result.channels)
                    {
                        string sname = Regex.Match(ch.title, "^([0-9]+)").Groups[1].Value;
                        if (string.IsNullOrEmpty(sname))
                            sname = Regex.Match(ch.title, "([0-9]+)$").Groups[1].Value;

                        tpl.Append(ch.title, host + $"lite/vokino?rjson={rjson}&origid={origid}&kinopoisk_id={kinopoisk_id}&balancer={balancer}&title={enc_title}&original_title={enc_original_title}&t={t}&s={sname}", sname);
                    }

                    return rjson ? tpl.ToJson() : tpl.ToHtml();
                }
                else
                {
                    var series = result.channels.First(i => i.title.StartsWith($"{s} ") || i.title.EndsWith($" {s}")).submenu;

                    var tpl = new EpisodeTpl(series.Length);

                    string sArhc = s.ToString();

                    foreach (var e in series)
                    {
                        string ename = Regex.Match(e.ident, "([0-9]+)$").Groups[1].Value;
                        tpl.Append(e.title, title ?? original_title, sArhc, ename, onstreamfile(e.stream_url), vast: vast);
                    }

                    if (rjson)
                        return tpl.ToJson(vtpl);

                    return vtpl.ToHtml() + tpl.ToHtml();
                }
            }
            else
            {
                var mtpl = new MovieTpl(title, original_title, result.channels.Length);

                foreach (var ch in result!.channels)
                {
                    string name = ch.quality_full;
                    if (!string.IsNullOrWhiteSpace(name.Replace("2160p.", "")))
                    {
                        name = name.Replace("2160p.", "4K ");

                        if (ch.extra != null && ch.extra.TryGetValue("size", out string size) && !string.IsNullOrEmpty(size))
                            name += $" - {size}";
                    }

                    mtpl.Append(name, onstreamfile(ch.stream_url), vast: vast);
                }

                if (rjson)
                    return mtpl.ToJson(vtpl: vtpl);

                return vtpl.ToHtml() + mtpl.ToHtml();
            }
        }
        #endregion
    }
}
