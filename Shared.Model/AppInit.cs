using Lampac.Models.SISI;
using Lampac.Models.LITE;
using Lampac.Models.AppConf;
using System.Text.RegularExpressions;
using Shared.Model.Base;
using Shared.Model.Online.Settings;

namespace Shared.Model
{
    public class AppInit
    {
        public static string corseuhost { get; set; } = "https://cors.apn.monster";

        public ApnConf apn { get; set; } = new ApnConf() { host = "https://apn.watch", secure = "none" };

        public static bool IsDefaultApnOrCors(string? apn) => apn != null && Regex.IsMatch(apn, "(apn.monster|apn.watch|cfhttp.top|lampac.workers.dev)");

        public string? corsehost { get; set; }

        public SisiConf sisi { get; set; } = new SisiConf()
        {
            component = "sisi", iconame = "",
            heightPicture = 240, rsize = true, rsize_disable = new string[] { "bgs", "chu" },
            bookmarks = new SISI.BookmarksConf() { saveimage = true, savepreview = true }
        };

        public SisiSettings BongaCams { get; set; } = new SisiSettings("https://ee.bongacams.com");

        public SisiSettings Chaturbate { get; set; } = new SisiSettings("https://chaturbate.com");

        public SisiSettings Ebalovo { get; set; } = new SisiSettings("https://www.ebalovo.pro");

        public SisiSettings Eporner { get; set; } = new SisiSettings("https://www.eporner.com", streamproxy: true);

        public SisiSettings HQporner { get; set; } = new SisiSettings("https://m.hqporner.com") { geostreamproxy = new List<string>() { "ALL" } };

        public SisiSettings Porntrex { get; set; } = new SisiSettings("https://www.porntrex.com");

        public SisiSettings Spankbang { get; set; } = new SisiSettings("https://ru.spankbang.com");

        public SisiSettings Xhamster { get; set; } = new SisiSettings("https://ru.xhamster.com");

        public SisiSettings Xnxx { get; set; } = new SisiSettings("https://www.xnxx.com");

        public SisiSettings Tizam { get; set; } = new SisiSettings("https://wwa.tizam.info");

        public SisiSettings Xvideos { get; set; } = new SisiSettings("https://www.xvideos.com");

        public SisiSettings XvideosRED { get; set; } = new SisiSettings("https://www.xvideos.red", enable: false);

        public SisiSettings PornHub { get; set; } = new SisiSettings("https://rt.pornhub.com");

        public SisiSettings PornHubPremium { get; set; } = new SisiSettings("https://rt.pornhubpremium.com", enable: false);



        public OnlinesSettings Kinobase { get; set; } = new OnlinesSettings("https://kinobase.org") { rip = true, geostreamproxy = new List<string>() { "ALL" } };

        public RezkaSettings Rezka { get; set; } = new RezkaSettings("https://hdrezka.me") { scheme = "http" };

        public RezkaSettings RezkaPrem { get; set; } = new RezkaSettings("https://standby-rezka.tv") { enable = false, scheme = "http" };

        public CollapsSettings Collaps { get; set; } = new CollapsSettings("https://api.ninsel.ws", streamproxy: true, two: true);

        public OnlinesSettings Ashdi { get; set; } = new OnlinesSettings("https://base.ashdi.vip");

        public OnlinesSettings Kinoukr { get; set; } = new OnlinesSettings("https://kinoukr.com");

        public OnlinesSettings Kinotochka { get; set; } = new OnlinesSettings("https://kinovibe.co", streamproxy: true);

        public OnlinesSettings CDNvideohub { get; set; } = new OnlinesSettings("https://player.cdnvideohub.com", streamproxy: true, enable: false);

        public OnlinesSettings Redheadsound { get; set; } = new OnlinesSettings("https://redheadsound.studio");

        public OnlinesSettings iRemux { get; set; } = new OnlinesSettings("https://megaoblako.com") { corseu = true, geostreamproxy = new List<string>() { "UA" } };

        public PidTorSettings PidTor { get; set; } = new PidTorSettings() { enable = true, redapi = "http://redapi.cfhttp.top", min_sid = 15 };

        public FilmixSettings Filmix { get; set; } = new FilmixSettings("http://filmixapp.cyou");

        public FilmixSettings FilmixTV { get; set; } = new FilmixSettings("https://api.filmix.tv", enable: false);

        public FilmixSettings FilmixPartner { get; set; } = new FilmixSettings("http://5.61.56.18/partner_api", enable: false);

        public ZetflixSettings Zetflix { get; set; } = new ZetflixSettings("https://zetfix.online") { geostreamproxy = new List<string>() { "ALL" }, hls = true };

        /// <summary>
        /// aHR0cHM6Ly9raW5vcGxheTIuc2l0ZS8=
        /// a2lub2dvLm1lZGlh
        /// </summary>
        public OnlinesSettings VideoDB { get; set; } = new OnlinesSettings("https://30bf3790.obrut.watch") { geostreamproxy = new List<string>() { "ALL" } };

        /// <summary>
        /// aHR0cHM6Ly9jb2xkZmlsbS5pbmsv
        /// </summary>
        public OnlinesSettings CDNmovies { get; set; } = new OnlinesSettings("https://coldcdn.xyz");

        public OnlinesSettings VDBmovies { get; set; } = new OnlinesSettings("https://cdnmovies-stream.online", rip: true) { geostreamproxy = new List<string>() { "ALL" } };

        public OnlinesSettings FanCDN { get; set; } = new OnlinesSettings("https://s2.fanserialstv.net", enable: false) { geostreamproxy = new List<string>() { "ALL" } };

        public OnlinesSettings VCDN { get; set; } = new OnlinesSettings("http://22884.annacdn.cc/nPBZWDQ5doe2", "https://videocdn.tv", token: "3i40G5TSECmLF77oAqnEgbx61ZWaOYaE") { scheme = "http", geostreamproxy = new List<string>() { "ALL" } };

        public VokinoSettings VoKino { get; set; } = new VokinoSettings("http://api.vokino.tv", streamproxy: true);

        public IframeVideoSettings IframeVideo { get; set; } = new IframeVideoSettings("https://iframe.video", "https://videoframe.space", enable: false);

        public OnlinesSettings HDVB { get; set; } = new OnlinesSettings("https://apivb.info", token: "5e2fe4c70bafd9a7414c4f170ee1b192");

        public KinoPubSettings KinoPub { get; set; } = new KinoPubSettings("https://api.srvkp.com") { uhd = true, hevc = true, hdr = true, filetype = "hls4" };

        public AllohaSettings Alloha { get; set; } = new AllohaSettings("https://api.apbugall.org", "https://torso-as.allarknow.online", "", "", true, true);



        public KodikSettings Kodik { get; set; } = new KodikSettings("https://kodikapi.com", "http://kodik.info", "71d163b40d50397a86ca54c366f33b72", "", true) { geostreamproxy = new List<string>() { "UA" } };

        public OnlinesSettings AnilibriaOnline { get; set; } = new OnlinesSettings("https://api.anilibria.tv");

        /// <summary>
        /// aHR0cHM6Ly9hbmlsaWIubWU=
        /// </summary>
        public OnlinesSettings AnimeLib { get; set; } = new OnlinesSettings("https://api.mangalib.me");

        public OnlinesSettings AniMedia { get; set; } = new OnlinesSettings("https://online.animedia.tv", streamproxy: true);

        public OnlinesSettings Animevost { get; set; } = new OnlinesSettings("https://animevost.org", streamproxy: true);

        public OnlinesSettings MoonAnime { get; set; } = new OnlinesSettings("https://api.moonanime.art", enable: false);

        public OnlinesSettings Animebesst { get; set; } = new OnlinesSettings("https://anime1.best");

        public OnlinesSettings AnimeGo { get; set; } = new OnlinesSettings("https://animego.org", streamproxy: true, enable: false);





        public RezkaSettings Voidboost { get; set; } = new RezkaSettings("https://voidboost.net", streamproxy: true) { enable = false, rip = true };

        public OnlinesSettings Seasonvar { get; set; } = new OnlinesSettings("http://api.seasonvar.ru", enable: false, rip: true);

        public OnlinesSettings Lostfilmhd { get; set; } = new OnlinesSettings("http://www.disneylove.ru", streamproxy: true, rip: true);

        public OnlinesSettings Eneyida { get; set; } = new OnlinesSettings("https://eneyida.tv", rip: true);
    }
}
