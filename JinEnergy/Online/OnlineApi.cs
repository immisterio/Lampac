using JinEnergy.Engine;
using Microsoft.JSInterop;
using System.Text.RegularExpressions;

namespace JinEnergy.Online
{
    public class OnlineApiController : BaseController
    {
        #region Externalids
        static Dictionary<string, string?> eids = new Dictionary<string, string?>();

        [JSInvokable("externalids")]
        async public static Task<dynamic> Externalids(string args)
        {
            long id = long.Parse(arg("id", args) ?? "0");
            if (id == 0)
                return OnError("id");

            string? imdb_id = arg("imdb_id", args);
            int serial = int.Parse(arg("serial", args) ?? "0");

            #region getAlloha / getVSDN / getTabus
            async ValueTask<string?> getAlloha(string imdb)
            {
                string? json = await JsHttpClient.Get("https://api.alloha.tv/?token=04941a9a3ca3ac16e2b4327347bbc1&imdb=" + imdb, timeoutSeconds: 5);
                if (json == null)
                    return null;

                string kpid = Regex.Match(json, "\"id_kp\":([0-9]+),").Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(kpid) && kpid != "0" && kpid != "null")
                    return kpid;

                return null;
            }

            async ValueTask<string?> getVSDN(string imdb)
            {
                string? json = await JsHttpClient.Get("http://cdn.svetacdn.in/api/short?api_token=3i40G5TSECmLF77oAqnEgbx61ZWaOYaE&imdb_id=" + imdb, timeoutSeconds: 5);
                if (json == null)
                    return null;

                string kpid = Regex.Match(json, "\"kp_id\":\"([0-9]+)\"").Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(kpid) && kpid != "0" && kpid != "null")
                    return kpid;

                return null;
            }

            async ValueTask<string?> getTabus(string imdb)
            {
                string? json = await JsHttpClient.Get("https://api.bhcesh.me/list?token=eedefb541aeba871dcfc756e6b31c02e&imdb_id=" + imdb, timeoutSeconds: 5);
                if (json == null)
                    return null;

                string kpid = Regex.Match(json, "\"kinopoisk_id\":\"([0-9]+)\"").Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(kpid) && kpid != "0" && kpid != "null")
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
                    kinopoisk_id = await getAlloha(imdb_id) ?? await getVSDN(imdb_id) ?? await getTabus(imdb_id);
                    eids.TryAdd(imdb_id, kinopoisk_id);
                }
            }
            #endregion

            return new { imdb_id, kinopoisk_id };
        }
        #endregion

        #region Events
        [JSInvokable("lite/events")]
        public static string Events(string args)
        {
            string online = string.Empty;
            defaultOnlineArgs(args, out long id, out string? imdb_id, out long kinopoisk_id, out string? title, out string? original_title, out int serial, out string? original_language, out int year, out string? source, out int clarification, out long cub_id, out string? account_email);

            serial = int.Parse(arg("serial", args) ?? "-1");
            bool isanime = original_language == "ja";
            bool life = arg(args, "life")?.ToLower() == "true";

            //if (!string.IsNullOrWhiteSpace(conf.VoKino.token) && (serial == -1 || serial == 0))
            //    online += "{\"name\":\"" + (conf.VoKino.displayname ?? "VoKino") + "\",\"url\":\"{localhost}/vokino\"},";

            //if (conf.KinoPub.enable)
            //    online += "{\"name\":\"" + (conf.KinoPub.displayname ?? "KinoPub") + "\",\"url\":\"{localhost}/kinopub\"},";

            if (AppInit.Filmix.enable)
                online += "{\"name\":\"Filmix\",\"url\":\"lite/filmix\"},";

            //if (!string.IsNullOrWhiteSpace(conf.Bazon.token))
            //    online += "{\"name\":\"" + (conf.Bazon.displayname ?? "Bazon") + "\",\"url\":\"{localhost}/bazon\"},";

            //if (!string.IsNullOrWhiteSpace(conf.Alloha.token))
            //    online += "{\"name\":\"" + (conf.Alloha.displayname ?? "Alloha") + "\",\"url\":\"{localhost}/alloha\"},";

            //if (conf.Rezka.enable)
            //    online += "{\"name\":\"" + (conf.Rezka.displayname ?? "Rezka") + "\",\"url\":\"{localhost}/rezka\"},";

            if (AppInit.VideoDB.enable)
                online += "{\"name\":\"VideoDB\",\"url\":\"lite/videodb\"},";

            //if (conf.Kinobase.enable)
            //    online += "{\"name\":\"" + (conf.Kinobase.displayname ?? "Kinobase") + "\",\"url\":\"{localhost}/kinobase\"},";

            //if (conf.Zetflix.enable)
            //    online += "{\"name\":\"" + (conf.Zetflix.displayname ?? "Zetflix") + "\",\"url\":\"{localhost}/zetflix\"},";

            //if (conf.Voidboost.enable)
            //    online += "{\"name\":\"" + (conf.Voidboost.displayname ?? "Voidboost") + "\",\"url\":\"{localhost}/voidboost\"},";

            if (AppInit.VCDN.enable)
                online += "{\"name\":\"VideoCDN\",\"url\":\"lite/vcdn\"},";

            if (AppInit.Ashdi.enable)
                online += "{\"name\":\"Ashdi (UKR)\",\"url\":\"lite/ashdi\"},";

            if (AppInit.Eneyida.enable)
                online += "{\"name\":\"Eneyida (UKR)\",\"url\":\"lite/eneyida\"},";

            //if (!string.IsNullOrWhiteSpace(conf.Kodik.token))
            //    online += "{\"name\":\"" + (conf.Kodik.displayname ?? "Kodik") + "\",\"url\":\"{localhost}/kodik\"},";

            //if (!string.IsNullOrWhiteSpace(conf.Seasonvar.token) && (serial == -1 || serial == 1))
            //    online += "{\"name\":\"" + (conf.Seasonvar.displayname ?? "Seasonvar") + "\",\"url\":\"{localhost}/seasonvar\"},";

            //if (conf.Lostfilmhd.enable && (serial == -1 || serial == 1))
            //    online += "{\"name\":\"" + (conf.Lostfilmhd.displayname ?? "LostfilmHD") + "\",\"url\":\"{localhost}/lostfilmhd\"},";

            if (AppInit.Collaps.enable)
                online += "{\"name\":\"Collaps\",\"url\":\"lite/collaps\"},";

            //if (!string.IsNullOrWhiteSpace(conf.HDVB.token))
            //    online += "{\"name\":\"" + (conf.HDVB.displayname ?? "HDVB") + "\",\"url\":\"{localhost}/hdvb\"},";

            if (AppInit.CDNmovies.enable && (serial == -1 || (serial == 1 && !isanime)))
                online += "{\"name\":\"CDNmovies\",\"url\":\"lite/cdnmovies\"},";

            //if (serial == -1 || isanime)
            //{
            //    if (conf.AnilibriaOnline.enable)
            //        online += "{\"name\":\"" + (conf.AnilibriaOnline.displayname ?? "Anilibria") + "\",\"url\":\"{localhost}/anilibria\"},";

            //    if (conf.Animevost.enable)
            //        online += "{\"name\":\"" + (conf.Animevost.displayname ?? "Animevost") + "\",\"url\":\"{localhost}/animevost\"},";

            //    if (conf.Animebesst.enable)
            //        online += "{\"name\":\"" + (conf.Animebesst.displayname ?? "Animebesst") + "\",\"url\":\"{localhost}/animebesst\"},";

            //    if (conf.AnimeGo.enable)
            //        online += "{\"name\":\"" + (conf.AnimeGo.displayname ?? "AnimeGo") + "\",\"url\":\"{localhost}/animego\"},";

            //    if (conf.AniMedia.enable)
            //        online += "{\"name\":\"" + (conf.AniMedia.displayname ?? "AniMedia") + "\",\"url\":\"{localhost}/animedia\"},";
            //}

            //if (conf.Kinotochka.enable)
            //    online += "{\"name\":\"" + (conf.Kinotochka.displayname ?? "Kinotochka") + "\",\"url\":\"{localhost}/kinotochka\"},";

            //if (serial == -1 || serial == 0 || (serial == 1 && !isanime))
            //{
            //    if (conf.Kinokrad.enable)
            //        online += "{\"name\":\"" + (conf.Kinokrad.displayname ?? "Kinokrad") + "\",\"url\":\"{localhost}/kinokrad\"},";

            //    if (conf.Kinoprofi.enable)
            //        online += "{\"name\":\"" + (conf.Kinoprofi.displayname ?? "Kinoprofi") + "\",\"url\":\"{localhost}/kinoprofi\"},";

            //    if (conf.Redheadsound.enable && (serial == -1 || serial == 0))
            //        online += "{\"name\":\"" + (conf.Redheadsound.displayname ?? "Redheadsound") + "\",\"url\":\"{localhost}/redheadsound\"},";

            //    if (!string.IsNullOrWhiteSpace(conf.VideoAPI.token) && (serial == -1 || serial == 0))
            //        online += "{\"name\":\"" + (conf.VideoAPI.displayname ?? "VideoAPI (ENG)") + "\",\"url\":\"{localhost}/videoapi\"},";
            //}

            //if (conf.IframeVideo.enable && (serial == -1 || serial == 0))
            //    online += "{\"name\":\"" + (conf.IframeVideo.displayname ?? "IframeVideo") + "\",\"url\":\"{localhost}/iframevideo\"},";


            return $"[{Regex.Replace(online, ",$", "")}]";
        }
        #endregion
    }
}
