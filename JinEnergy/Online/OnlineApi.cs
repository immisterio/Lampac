using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using Shared.Model.Base;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JinEnergy.Online
{
    public class OnlineApiController : BaseController
    {
        #region Externalids
        static Dictionary<string, string?> eids = new Dictionary<string, string?>();

        [JSInvokable("externalids")]
        async public static ValueTask<string> Externalids(string args)
        {
            string? kpid = parse_arg("id", args);
            if (kpid != null && kpid.StartsWith("KP_"))
            {
                kpid = kpid.Replace("KP_", "");
                string? json = await JsHttpClient.Get("https://videocdn.tv/api/short?api_token=3i40G5TSECmLF77oAqnEgbx61ZWaOYaE&kinopoisk_id=" + kpid, timeoutSeconds: 5);
                return JsonSerializer.Serialize(new { imdb_id = Regex.Match(json ?? "", "\"imdb_id\":\"(tt[^\"]+)\"").Groups[1].Value, kinopoisk_id = kpid });
            }

            var arg = defaultArgs(args);
            if (arg.id == 0)
                return EmptyError("id");

            string? imdb_id = parse_arg("imdb_id", args);
            int serial = int.Parse(parse_arg("serial", args) ?? "0");

            #region getAlloha / getVSDN / getTabus
            async Task<string?> getAlloha(string imdb)
            {
                string? json = await JsHttpClient.Get("https://api.alloha.tv/?token=04941a9a3ca3ac16e2b4327347bbc1&imdb=" + imdb, timeoutSeconds: 4);
                if (json == null)
                    return null;

                string kpid = Regex.Match(json, "\"id_kp\":([0-9]+),").Groups[1].Value;
                if (!string.IsNullOrEmpty(kpid) && kpid != "0" && kpid != "null")
                    return kpid;

                return null;
            }

            async Task<string?> getVSDN(string imdb)
            {
                string? json = await JsHttpClient.Get("https://videocdn.tv/api/short?api_token=3i40G5TSECmLF77oAqnEgbx61ZWaOYaE&imdb_id=" + imdb, timeoutSeconds: 4);
                if (json == null)
                    return null;

                string kpid = Regex.Match(json, "\"kp_id\":\"?([0-9]+)\"?").Groups[1].Value;
                if (!string.IsNullOrEmpty(kpid) && kpid != "0" && kpid != "null")
                    return kpid;

                return null;
            }

            async Task<string?> getTabus(string imdb)
            {
                string? json = await JsHttpClient.Get("https://api.bhcesh.me/franchise/details?token=eedefb541aeba871dcfc756e6b31c02e&imdb_id=" + imdb.Remove(0, 2), timeoutSeconds: 4);
                if (json == null)
                    return null;

                string kpid = Regex.Match(json, "\"kinopoisk_id\":\"?([0-9]+)\"?").Groups[1].Value;
                if (!string.IsNullOrEmpty(kpid) && kpid != "0" && kpid != "null")
                    return kpid;

                return null;
            }
            #endregion

            #region get imdb_id
            if (string.IsNullOrWhiteSpace(imdb_id))
            {
                string path = $"imdb_id:{serial}:{arg.id}";
                if (!eids.TryGetValue(path, out imdb_id))
                {
                    string cat = serial == 1 ? "tv" : "movie";
                    string? json = await JsHttpClient.Get($"https://api.themoviedb.org/3/{cat}/{arg.id}?api_key=4ef0d7355d9ffb5151e987764708ce96&append_to_response=external_ids", timeoutSeconds: 5);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        imdb_id = Regex.Match(json, "\"imdb_id\":\"(tt[0-9]+)\"").Groups[1].Value;
                        if (!string.IsNullOrWhiteSpace(imdb_id))
                            eids.TryAdd(path, imdb_id);
                    }
                }
            }
            #endregion

            #region get kinopoisk_id
            string? kinopoisk_id = null;

            if (!string.IsNullOrWhiteSpace(imdb_id))
            {
                if (!eids.TryGetValue(imdb_id, out kinopoisk_id))
                {
                    var tasks = new Task<string?>[] { getAlloha(imdb_id), getVSDN(imdb_id), getTabus(imdb_id) };
                    await Task.WhenAll(tasks);

                    kinopoisk_id = tasks[0].Result ?? tasks[1].Result ?? tasks[2].Result;
                    eids.TryAdd(imdb_id, kinopoisk_id);
                }
            }
            #endregion

            return JsonSerializer.Serialize(new { imdb_id, kinopoisk_id });
        }
        #endregion

        #region Events
        [JSInvokable("lite/events")]
        public static string Events(string args)
        {
            var online = new List<(string name, string url, string plugin, int index)>(20);

            var arg = defaultArgs(args);
            int serial = int.Parse(parse_arg("serial", args) ?? "-1");
            bool isanime = arg.original_language == "ja";
            bool titleSearch = string.IsNullOrEmpty(arg.imdb_id) && arg.kinopoisk_id == 0;
            string argTitle_vpn = string.Empty; // AppInit.Country == "UA" ? " / vpn" : "";

            void send(string name, string plugin, BaseSettings init, string? arg_title = null, string? arg_url = null, string? overridehost = null)
            {
                if (init.enable && !init.rip)
                {
                    string? url = overridehost ?? init.overridehost;
                    if (string.IsNullOrEmpty(url))
                        url = "lite/" + plugin + arg_url;

                    if (arg.original_language is "ru" or "ja" or "ko" or "zh" or "cn" or "zh|cn")
                    {
                        if (plugin is "eneyida" or "filmix" or "kinoukr" or "rezka" or "redheadsound" or "kinopub" || (plugin == "kodik" && arg.kinopoisk_id == 0 && string.IsNullOrEmpty(arg.imdb_id)))
                            url += (url.Contains("?") ? "&" : "?") + "clarification=1";
                    }

                    online.Add(($"{init.displayname ?? name}{arg_title}", url, plugin, init.displayindex > 0 ? init.displayindex : online.Count));
                }
            }

            if (arg.original_language is "ja" or "ko" or "zh" or "cn" or "zh|cn")
                send("Kodik - 720p", "kodik", AppInit.Kodik);

            if (isanime)
            {
                send("Anilibria - 1080p", "anilibria", AppInit.AnilibriaOnline);
                send("Animevost - 720p", "animevost", AppInit.Animevost);
                send("AniMedia - 1080p", "animedia", AppInit.AniMedia);
            }

            if (AppInit.Filmix.pro)
                send("Filmix - 4K HDR", "filmix", AppInit.Filmix, arg_url: (arg.source == "filmix" ? $"?postid={arg.id}" : ""));

            send("KinoPub - 4K HDR", "kinopub", AppInit.KinoPub, arg_url: (arg.source == "pub" ? $"?postid={arg.id}" : ""));

            if (arg.kinopoisk_id > 0 && AppInit.VoKino.enable)
                VoKinoInvoke.SendOnline(AppInit.VoKino, online, bwa: true);

            if (arg.kinopoisk_id > 0)
                send("Zetflix - 1080p", "zetflix", AppInit.Zetflix);

            send("Rezka - " + (!string.IsNullOrEmpty(AppInit.Rezka.cookie) ? "2160p" : "720p"), "rezka", AppInit.Rezka);
            send("VideoCDN - 1080p", "vcdn", AppInit.VCDN, argTitle_vpn);
            send("Kinobase - 1080p", "kinobase", AppInit.Kinobase);

            if (AppInit.Country != "RU" && AppInit.Country != "BY")
            {
                if (arg.kinopoisk_id > 0)
                    send("Ashdi (UKR) - 4K", "ashdi", AppInit.Ashdi);

                send("Eneyida (UKR) - 1080p", "eneyida", AppInit.Eneyida);

                if (!isanime)
                    send("Kinoukr (UKR) - 1080p", "kinoukr", AppInit.Kinoukr);
            }

            if (!titleSearch)
                send(AppInit.Collaps.dash ? "Collaps - 1080p" : "Collaps - 720p", "collaps", AppInit.Collaps);

            if (!AppInit.Filmix.pro)
                send($"Filmix - {(string.IsNullOrEmpty(AppInit.Filmix.token) ? "480p" : "720p")}", "filmix", AppInit.Filmix, arg_url: (arg.source == "filmix" ? $"?postid={arg.id}" : ""));

            if (serial == 0 && !isanime)
            {
                send("iRemux - 4K HDR", "remux", AppInit.iRemux);
                send("RHS - 1080p", "redheadsound", AppInit.Redheadsound);

                if (arg.kinopoisk_id > 0)
                    send("Kinotochka - 720p", "kinotochka", AppInit.Kinotochka);
            }

            //if (!titleSearch)
            //    send("Voidboost - 720p", "voidboost", AppInit.Voidboost, argTitle_vpn);

            if (arg.kinopoisk_id > 0)
                send("VDBmovies - 1080p", "vdbmovies", AppInit.VDBmovies, argTitle_vpn);

            send("HDVB - 1080p", "hdvb", AppInit.HDVB);

            if (arg.kinopoisk_id > 0 && serial == 1 && !isanime)
                send("CDNmovies - 360p", "cdnmovies", AppInit.CDNmovies);


            return $"[{string.Join(",", online.OrderBy(i => i.index).Select(i => "{\"name\":\"" + i.name + "\",\"url\":\"" + i.url + "\",\"balanser\":\"" + i.plugin + "\"}"))}]";
        }
        #endregion
    }
}
