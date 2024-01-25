using JinEnergy.Engine;
using Microsoft.JSInterop;
using System.Text;
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
            long id = long.Parse(parse_arg("id", args) ?? "0");
            if (id == 0)
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
                string path = $"imdb_id:{serial}:{id}";
                if (!eids.TryGetValue(path, out imdb_id))
                {
                    string cat = serial == 1 ? "tv" : "movie";
                    string? json = await JsHttpClient.Get($"https://api.themoviedb.org/3/{cat}/{id}?api_key=4ef0d7355d9ffb5151e987764708ce96&append_to_response=external_ids", timeoutSeconds: 5);
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
            var online = new StringBuilder();

            var arg = defaultArgs(args);
            int serial = int.Parse(parse_arg("serial", args) ?? "-1");
            bool isanime = arg.original_language == "ja";
            bool titleSearch = string.IsNullOrEmpty(arg.imdb_id) && arg.kinopoisk_id == 0;

            if (AppInit.Kodik.enable && (arg.original_language is "ja" or "ko" or "zh"))
                online.Append("{\"name\":\"Kodik - 720p\",\"url\":\"lite/kodik\"},");

            if (AppInit.AnilibriaOnline.enable && isanime)
                online.Append("{\"name\":\"Anilibria - 1080p\",\"url\":\"lite/anilibria\"},");

            if (isanime)
            {
                online.Append("{\"name\":\"Animevost\",\"url\":\"https://bwa-cloud.apn.monster/lite/animevost\"},");
                online.Append("{\"name\":\"AniMedia\",\"url\":\"https://bwa-cloud.apn.monster/lite/animedia\"},");
            }

            if (AppInit.Filmix.enable && AppInit.Filmix.pro)
                online.Append("{\"name\":\"Filmix - 4K HDR\",\"url\":\"lite/filmix" + (arg.source == "filmix" ? $"?postid={arg.id}" : "") + "\"},");

            if (AppInit.KinoPub.enable)
                online.Append("{\"name\":\"KinoPub - 4K HDR\",\"url\":\"lite/kinopub" + (arg.source == "pub" ? $"?postid={arg.id}" : "")+"\"},");

            if (AppInit.VideoDB.enable && arg.kinopoisk_id > 0)
                online.Append("{\"name\":\"VideoDB - 1080p\",\"url\":\"lite/videodb\"},");

            if (AppInit.Rezka.enable)
                online.Append("{\"name\":\"Rezka - 4K\",\"url\":\"lite/rezka\"},");

            if (AppInit.VoKino.enable && serial == 0 && !isanime && arg.kinopoisk_id > 0)
                online.Append("{\"name\":\"VoKino - 4K HDR\",\"url\":\"lite/vokino\"},");

            if (AppInit.Kinobase.enable)
                online.Append("{\"name\":\"Kinobase - 1080p\",\"url\":\"lite/kinobase\"},");

            if (AppInit.Country != "RU" && AppInit.Country != "BY")
            {
                if (AppInit.Ashdi.enable && arg.kinopoisk_id > 0)
                    online.Append("{\"name\":\"Ashdi (UKR) - 1080p\",\"url\":\"lite/ashdi\"},");

                if (AppInit.Eneyida.enable)
                    online.Append("{\"name\":\"Eneyida (UKR) - 1080p\",\"url\":\"lite/eneyida\"},");
            }

            if (AppInit.Collaps.enable && !titleSearch)
                online.Append("{\"name\":\"Collaps - 720p\",\"url\":\"lite/collaps\"},");

            if (AppInit.Voidboost.enable && !titleSearch)
                online.Append("{\"name\":\"Voidboost - 720p"+(AppInit.Country == "UA" ? " / vpn" : "")+"\",\"url\":\"lite/voidboost\"},");

            if (AppInit.Filmix.enable && !AppInit.Filmix.pro)
                online.Append("{\"name\":\"Filmix - 480p\",\"url\":\"lite/filmix" + (arg.source == "filmix" ? $"?postid={arg.id}" : "") + "\"},");

            if (serial == 0 && !isanime)
            {
                if (AppInit.Redheadsound.enable)
                    online.Append("{\"name\":\"RHS - 1080p\",\"url\":\"lite/redheadsound\"},");

                if (AppInit.Kinotochka.enable && arg.kinopoisk_id > 0)
                    online.Append("{\"name\":\"Kinotochka - 480p\",\"url\":\"lite/kinotochka\"},");
            }

            if (AppInit.VCDN.enable)
                online.Append("{\"name\":\"VideoCDN - 1080p"+(AppInit.Country == "UA" ? " / vpn" : "")+"\",\"url\":\"lite/vcdn\"},");

            online.Append("{\"name\":\"HDVB\",\"url\":\"https://bwa-cloud.apn.monster/lite/hdvb\"},");

            if (AppInit.Zetflix.enable && arg.kinopoisk_id > 0)
                online.Append("{\"name\":\"Zetflix - 1080p\",\"url\":\"lite/zetflix\"},");

            if (AppInit.VDBmovies.enable && !titleSearch)
                online.Append("{\"name\":\"VDBmovies - 720p"+(AppInit.Country == "UA" ? " / vpn" : "")+"\",\"url\":\"lite/vdbmovies\"},");

            if (AppInit.CDNmovies.enable && arg.kinopoisk_id > 0 && serial == 1 && !isanime)
                online.Append("{\"name\":\"CDNmovies - 360p\",\"url\":\"lite/cdnmovies\"},");


            return $"[{Regex.Replace(online.ToString(), ",$", "")}]";
        }
        #endregion
    }
}
