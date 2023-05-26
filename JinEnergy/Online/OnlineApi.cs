using JinEnergy.Engine;
using Microsoft.JSInterop;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JinEnergy.Online
{
    public class OnlineApiController : BaseController
    {
        #region Externalids
        static Dictionary<string, string?> eids = new Dictionary<string, string?>();

        [JSInvokable("externalids")]
        async public static Task<string> Externalids(string args)
        {
            long id = long.Parse(parse_arg("id", args) ?? "0");
            if (id == 0)
                return OnError("id");

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
            string online = string.Empty;

            var arg = defaultArgs(args);
            int serial = int.Parse(parse_arg("serial", args) ?? "-1");
            bool isanime = arg.original_language == "ja";
            bool life = parse_arg(args, "life")?.ToLower() == "true";

            if (AppInit.VideoDB.enable)
                online += "{\"name\":\"VideoDB\",\"url\":\"lite/videodb\"},";

            if (AppInit.VCDN.enable)
                online += "{\"name\":\"VideoCDN\",\"url\":\"lite/vcdn\"},";

            if (AppInit.KinoPub.enable)
                online += "{\"name\":\"KinoPub\",\"url\":\"lite/kinopub\"},";

            if (AppInit.Filmix.enable)
                online += "{\"name\":\"Filmix\",\"url\":\"lite/filmix\"},";

            if (AppInit.VoKino.enable && (serial == -1 || serial == 0))
                online += "{\"name\":\"VoKino\",\"url\":\"lite/vokino\"},";

            //if (!string.IsNullOrWhiteSpace(conf.Bazon.token))
            //    online += "{\"name\":\"" + (conf.Bazon.displayname ?? "Bazon") + "\",\"url\":\"{localhost}/bazon\"},";

            //if (!string.IsNullOrWhiteSpace(conf.Alloha.token))
            //    online += "{\"name\":\"" + (conf.Alloha.displayname ?? "Alloha") + "\",\"url\":\"{localhost}/alloha\"},";

            if (AppInit.Rezka.enable)
                online += "{\"name\":\"Rezka\",\"url\":\"lite/rezka\"},";

            if (AppInit.Kinobase.enable)
                online += "{\"name\":\"Kinobase\",\"url\":\"lite/kinobase\"},";

            //if (conf.Zetflix.enable)
            //    online += "{\"name\":\"" + (conf.Zetflix.displayname ?? "Zetflix") + "\",\"url\":\"{localhost}/zetflix\"},";

            if (AppInit.Voidboost.enable)
                online += "{\"name\":\"Voidboost\",\"url\":\"lite/voidboost\"},";

            if (AppInit.Ashdi.enable)
                online += "{\"name\":\"Ashdi (UKR)\",\"url\":\"lite/ashdi\"},";

            if (AppInit.Eneyida.enable)
                online += "{\"name\":\"Eneyida (UKR)\",\"url\":\"lite/eneyida\"},";

            //if (!string.IsNullOrWhiteSpace(conf.Kodik.token))
            //    online += "{\"name\":\"" + (conf.Kodik.displayname ?? "Kodik") + "\",\"url\":\"{localhost}/kodik\"},";

            //if (conf.Lostfilmhd.enable && (serial == -1 || serial == 1))
            //    online += "{\"name\":\"" + (conf.Lostfilmhd.displayname ?? "LostfilmHD") + "\",\"url\":\"{localhost}/lostfilmhd\"},";

            if (AppInit.Collaps.enable)
                online += "{\"name\":\"Collaps\",\"url\":\"lite/collaps\"},";

            //if (!string.IsNullOrWhiteSpace(conf.HDVB.token))
            //    online += "{\"name\":\"" + (conf.HDVB.displayname ?? "HDVB") + "\",\"url\":\"{localhost}/hdvb\"},";

            if (AppInit.CDNmovies.enable && (serial == -1 || (serial == 1 && !isanime)))
                online += "{\"name\":\"CDNmovies\",\"url\":\"lite/cdnmovies\"},";

            if (serial == -1 || isanime)
            {
                if (AppInit.AnilibriaOnline.enable)
                    online += "{\"name\":\"Anilibria\",\"url\":\"lite/anilibria\"},";

                //    if (conf.Animevost.enable)
                //        online += "{\"name\":\"" + (conf.Animevost.displayname ?? "Animevost") + "\",\"url\":\"{localhost}/animevost\"},";

                //    if (conf.Animebesst.enable)
                //        online += "{\"name\":\"" + (conf.Animebesst.displayname ?? "Animebesst") + "\",\"url\":\"{localhost}/animebesst\"},";

                //    if (conf.AnimeGo.enable)
                //        online += "{\"name\":\"" + (conf.AnimeGo.displayname ?? "AnimeGo") + "\",\"url\":\"{localhost}/animego\"},";

                //    if (conf.AniMedia.enable)
                //        online += "{\"name\":\"" + (conf.AniMedia.displayname ?? "AniMedia") + "\",\"url\":\"{localhost}/animedia\"},";
            }

            if (serial == -1 || serial == 0)
            {
                //if (conf.IframeVideo.enable)
                //    online += "{\"name\":\"" + (conf.IframeVideo.displayname ?? "IframeVideo") + "\",\"url\":\"{localhost}/iframevideo\"},";

                if (AppInit.Kinotochka.enable)
                    online += "{\"name\":\"Kinotochka\",\"url\":\"lite/kinotochka\"},";

                if (AppInit.Redheadsound.enable)
                    online += "{\"name\":\"Redheadsound\",\"url\":\"lite/redheadsound\"},";
            }


            return $"[{Regex.Replace(online, ",$", "")}]";
        }
        #endregion
    }
}
