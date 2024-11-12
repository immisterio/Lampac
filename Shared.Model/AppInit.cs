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

        public SisiSettings BongaCams { get; set; } = new SisiSettings("kwwsv=22hh1erqjdfdpv1frp");

        public SisiSettings Chaturbate { get; set; } = new SisiSettings("kwwsv=22fkdwxuedwh1frp");

        public SisiSettings Ebalovo { get; set; } = new SisiSettings("kwwsv=22zzz1hedoryr1sur");

        public SisiSettings Eporner { get; set; } = new SisiSettings("kwwsv=22zzz1hsruqhu1frp", streamproxy: true);

        public SisiSettings HQporner { get; set; } = new SisiSettings("kwwsv=22p1ktsruqhu1frp") { geostreamproxy = new List<string>() { "ALL" } };

        public SisiSettings Porntrex { get; set; } = new SisiSettings("kwwsv=22zzz1sruqwuh{1frp");

        public SisiSettings Spankbang { get; set; } = new SisiSettings("kwwsv=22ux1vsdqnedqj1frp");

        public SisiSettings Xhamster { get; set; } = new SisiSettings("kwwsv=22ux1{kdpvwhu1frp");

        public SisiSettings Xnxx { get; set; } = new SisiSettings("kwwsv=22zzz1{q{{1frp");

        public SisiSettings Tizam { get; set; } = new SisiSettings("kwwsv=22jr1wl}dp1lqir");

        public SisiSettings Xvideos { get; set; } = new SisiSettings("kwwsv=22zzz1{ylghrv1frp");

        public SisiSettings XvideosRED { get; set; } = new SisiSettings("kwwsv=22zzz1{ylghrv1uhg", enable: false);

        public SisiSettings PornHub { get; set; } = new SisiSettings("kwwsv=22uw1sruqkxe1frp");

        public SisiSettings PornHubPremium { get; set; } = new SisiSettings("kwwsv=22uw1sruqkxesuhplxp1frp", enable: false);



        public OnlinesSettings Kinobase { get; set; } = new OnlinesSettings("kwwsv=22nlqredvh1ruj") { rip = true, geostreamproxy = new List<string>() { "ALL" } };

        public RezkaSettings Rezka { get; set; } = new RezkaSettings("kwwsv=22kguh}nd1ph") { scheme = "http" };

        public RezkaSettings RezkaPrem { get; set; } = new RezkaSettings("kwwsv=22vwdqge|0uh}nd1wy") { enable = false, scheme = "http" };

        public CollapsSettings Collaps { get; set; } = new CollapsSettings("kwwsv=22dsl1qlqvho1zv", streamproxy: true, two: true);

        public OnlinesSettings Ashdi { get; set; } = new OnlinesSettings("kwwsv=22edvh1dvkgl1yls");

        public OnlinesSettings Kinoukr { get; set; } = new OnlinesSettings("kwwsv=22nlqrxnu1frp");

        public OnlinesSettings Kinotochka { get; set; } = new OnlinesSettings("kwwsv=22nlqryleh1fr", streamproxy: true);

        public OnlinesSettings CDNvideohub { get; set; } = new OnlinesSettings("kwwsv=22sod|hu1fgqylghrkxe1frp", streamproxy: true, enable: false);

        public OnlinesSettings Redheadsound { get; set; } = new OnlinesSettings("kwwsv=22uhgkhdgvrxqg1vwxglr");

        public OnlinesSettings iRemux { get; set; } = new OnlinesSettings("kwwsv=22phjdreodnr1frp") { corseu = true, geostreamproxy = new List<string>() { "UA" } };

        public PidTorSettings PidTor { get; set; } = new PidTorSettings() { enable = true, redapi = "http://redapi.cfhttp.top", min_sid = 15 };

        public FilmixSettings Filmix { get; set; } = new FilmixSettings("kwws=22ilopl{dss1f|rx");

        public FilmixSettings FilmixTV { get; set; } = new FilmixSettings("kwwsv=22dsl1ilopl{1wy", enable: false);

        public FilmixSettings FilmixPartner { get; set; } = new FilmixSettings("kwws=22819418914;2sduwqhubdsl", enable: false);

        public ZetflixSettings Zetflix { get; set; } = new ZetflixSettings("kwwsv=22}hwil{1rqolqh") { geostreamproxy = new List<string>() { "ALL" }, hls = true };

        /// <summary>
        /// aHR0cHM6Ly9raW5vcGxheTIuc2l0ZS8=
        /// a2lub2dvLm1lZGlh
        /// </summary>
        public OnlinesSettings VideoDB { get; set; } = new OnlinesSettings("kwwsv=2263ei6:<31reuxw1vkrz") { geostreamproxy = new List<string>() { "ALL" } };

        /// <summary>
        /// aHR0cHM6Ly9jb2xkZmlsbS5pbmsv
        /// </summary>
        public OnlinesSettings CDNmovies { get; set; } = new OnlinesSettings("kwwsv=22frogfgq1{|}");

        public OnlinesSettings VDBmovies { get; set; } = new OnlinesSettings("kwwsv=22fgqprylhv0vwuhdp1rqolqh", rip: true) { geostreamproxy = new List<string>() { "ALL" } };

        public OnlinesSettings FanCDN { get; set; } = new OnlinesSettings("kwwsv=22v61idqvhuldovwy1qhw", enable: false) { geostreamproxy = new List<string>() { "ALL" } };

        public OnlinesSettings VCDN { get; set; } = new OnlinesSettings("kwws=2255;;71dqqdfgq1ff2qSE]ZGT8grh5", "kwwsv=22sruwdo1oxph{1krvw", token: "F:]{GKxq7f9PGpQQ|lyGxOgYTSXnMK:l", rip: true) { scheme = "http", geostreamproxy = new List<string>() { "ALL" } };

        public OnlinesSettings Lumex { get; set; } = new OnlinesSettings("kwwsv=22s1oxph{1sz2qSE]ZGT8grh5", "kwwsv=22sruwdo1oxph{1krvw", token: "F:]{GKxq7f9PGpQQ|lyGxOgYTSXnMK:l", enable: false) { scheme = "http", geostreamproxy = new List<string>() { "ALL" } };

        public VokinoSettings VoKino { get; set; } = new VokinoSettings("kwws=22dsl1yrnlqr1wy", streamproxy: true);

        public IframeVideoSettings IframeVideo { get; set; } = new IframeVideoSettings("kwwsv=22liudph1ylghr", "kwwsv=22ylghriudph1vsdfh", enable: false);

        /// <summary>
        /// aHR0cHM6Ly92aWQxNzMwODAxMzcwLmZvdHBybzEzNWFsdG8uY29tL2FwaS9pZGtwP2twX2lkPTEzOTI1NTAmZD1raW5vZ28uaW5j
        /// </summary>
        public OnlinesSettings HDVB { get; set; } = new OnlinesSettings("kwwsv=22dslye1frp", token: "8h5ih7f:3edig<d:747f7i4:3hh4e4<5");

        public KinoPubSettings KinoPub { get; set; } = new KinoPubSettings("kwwsv=22dsl1vuyns1frp") { uhd = true, hevc = true, hdr = true, filetype = "hls4" };

        public AllohaSettings Alloha { get; set; } = new AllohaSettings("kwwsv=22dsl1dsexjdoo1ruj", "kwwsv=22wruvr0dv1doodunqrz1rqolqh", "", "", true, true);



        public KodikSettings Kodik { get; set; } = new KodikSettings("kwwsv=22nrglndsl1frp", "kwws=22nrgln1lqir", "hh438g49<d<7g87dhe7hgh<6f6f4935:", "", true) { geostreamproxy = new List<string>() { "UA" } };

        public OnlinesSettings AnilibriaOnline { get; set; } = new OnlinesSettings("kwwsv=22dsl1dqloleuld1wy");

        /// <summary>
        /// aHR0cHM6Ly9hbmlsaWIubWU=
        /// </summary>
        public OnlinesSettings AnimeLib { get; set; } = new OnlinesSettings("kwwsv=22dsl1pdqjdole1ph");

        public OnlinesSettings AniMedia { get; set; } = new OnlinesSettings("kwwsv=22rqolqh1dqlphgld1wy", streamproxy: true);

        public OnlinesSettings Animevost { get; set; } = new OnlinesSettings("kwwsv=22dqlphyrvw1ruj", streamproxy: true);

        public OnlinesSettings MoonAnime { get; set; } = new OnlinesSettings("kwwsv=22dsl1prrqdqlph1duw", enable: false);

        public OnlinesSettings Animebesst { get; set; } = new OnlinesSettings("kwwsv=22dqlph41ehvw");

        public OnlinesSettings AnimeGo { get; set; } = new OnlinesSettings("kwwsv=22dqlphjr1ruj", streamproxy: true, enable: false);





        public RezkaSettings Voidboost { get; set; } = new RezkaSettings("kwwsv=22yrlgerrvw1qhw", streamproxy: true) { enable = false, rip = true };

        public OnlinesSettings Seasonvar { get; set; } = new OnlinesSettings("kwws=22dsl1vhdvrqydu1ux", enable: false, rip: true);

        public OnlinesSettings Lostfilmhd { get; set; } = new OnlinesSettings("kwws=22zzz1glvqh|oryh1ux", streamproxy: true, rip: true);

        public OnlinesSettings Eneyida { get; set; } = new OnlinesSettings("kwwsv=22hqh|lgd1wy", rip: true);
    }
}
