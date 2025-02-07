using Lampac.Models.SISI;
using Lampac.Models.LITE;
using Lampac.Models.AppConf;
using System.Text.RegularExpressions;
using Shared.Model.Base;
using Shared.Model.Online.Settings;
using Shared.Model.Online;

namespace Shared.Model
{
    public class AppInit
    {
        public static VastConf _vast;

        public VastConf vast = new VastConf();

        public static string corseuhost { get; set; } = "https://cors.apn.monster";

        public ApnConf apn { get; set; } = new ApnConf() { host = "http://apn.cfhttp.top", secure = "none" };

        public static bool IsDefaultApnOrCors(string? apn) => apn != null && Regex.IsMatch(apn, "(apn.monster|apn.watch|cfhttp.top|lampac.workers.dev)");

        public string? corsehost { get; set; }

        public SisiConf sisi { get; set; } = new SisiConf()
        {
            component = "sisi", iconame = "", push_all = true,
            heightPicture = 240, rsize = true, rsize_disable = new string[] { "bgs", "chu" },
            bookmarks = new SISI.BookmarksConf() { saveimage = true, savepreview = true }
        };

        public SisiSettings BongaCams { get; set; } = new SisiSettings("kwwsv=22hh1erqjdfdpv1frp") 
        {
            headers = HeadersModel.Init(
                ("dnt", "1"),
                ("cache-control", "no-cache"),
                ("pragma", "no-cache"),
                ("priority", "u=1, i"),
                ("sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("referer", "{host}"),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "same-origin"),
                ("x-requested-with", "XMLHttpRequest")
            )
        };

        public SisiSettings Chaturbate { get; set; } = new SisiSettings("kwwsv=22fkdwxuedwh1frp");

        public SisiSettings Ebalovo { get; set; } = new SisiSettings("kwwsv=22zzz1hedoryr1sur");

        public SisiSettings Eporner { get; set; } = new SisiSettings("kwwsv=22zzz1hsruqhu1frp", streamproxy: true);

        public SisiSettings HQporner { get; set; } = new SisiSettings("kwwsv=22p1ktsruqhu1frp") 
        { 
            geostreamproxy = new List<string>() { "ALL" },
            headers = HeadersModel.Init("referer", "https://m.hqporner.com")
        };

        public SisiSettings Porntrex { get; set; } = new SisiSettings("kwwsv=22zzz1sruqwuh{1frp");

        public SisiSettings Spankbang { get; set; } = new SisiSettings("kwwsv=22ux1vsdqnedqj1frp") 
        {
            headers = HeadersModel.Init(
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "cross-site"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36")
            )
        };

        public SisiSettings Xhamster { get; set; } = new SisiSettings("kwwsv=22ux1{kdpvwhu1frp");

        public SisiSettings Xnxx { get; set; } = new SisiSettings("kwwsv=22zzz1{q{{1frp");

        public SisiSettings Tizam { get; set; } = new SisiSettings("kwwsv=22lq1wl}dp1lqir");

        public SisiSettings Xvideos { get; set; } = new SisiSettings("kwwsv=22zzz1{ylghrv1frp");

        public SisiSettings XvideosRED { get; set; } = new SisiSettings("kwwsv=22zzz1{ylghrv1uhg", enable: false);

        public SisiSettings PornHub { get; set; } = new SisiSettings("kwwsv=22uw1sruqkxe1frp") 
        {
            headers = HeadersModel.Init(
                ("sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-site", "none"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1"),
                ("cookie", "platform=pc; bs=ukbqk2g03joiqzu68gitadhx5bhkm48j; ss=250837987735652383; fg_0d2ec4cbd943df07ec161982a603817e=56239.100000; atatusScript=hide; _gid=GA1.2.309162272.1686472069; d_fs=1; d_uidb=2f5e522a-fa28-a0fe-0ab2-fd90f45d96c0; d_uid=2f5e522a-fa28-a0fe-0ab2-fd90f45d96c0; d_uidb=2f5e522a-fa28-a0fe-0ab2-fd90f45d96c0; accessAgeDisclaimerPH=1; cookiesBannerSeen=1; _gat=1; __s=64858645-42FE722901BBA6E6-125476E1; __l=64858645-42FE722901BBA6E6-125476E1; hasVisited=1; fg_f916a4d27adf4fc066cd2d778b4d388e=78731.100000; fg_fa3f0973fd973fca3dfabc86790b408b=12606.100000; _ga_B39RFFWGYY=GS1.1.1686472069.1.1.1686472268.0.0.0; _ga=GA1.1.1515398043.1686472069")
            )
        };

        public SisiSettings PornHubPremium { get; set; } = new SisiSettings("kwwsv=22uw1sruqkxesuhplxp1frp", enable: false) 
        {
            headers = HeadersModel.Init(
                ("sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-site", "none"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1")
            )
        };



        public OnlinesSettings Kinobase { get; set; } = new OnlinesSettings("kwwsv=22nlqredvh1ruj") { rip = true, geostreamproxy = new List<string>() { "ALL" } };

        public RezkaSettings Rezka { get; set; } = new RezkaSettings("kwwsv=22kguh}nd1ph") { enable = false, hls = true, scheme = "http" };

        public RezkaSettings RezkaPrem { get; set; } = new RezkaSettings(null) { enable = false, hls = true, scheme = "http" };

        public CollapsSettings Collaps { get; set; } = new CollapsSettings("kwwsv=22dsl1qlqvho1zv", streamproxy: true, two: true);

        public OnlinesSettings Ashdi { get; set; } = new OnlinesSettings("kwwsv=22edvh1dvkgl1yls") { geo_hide = new string[] { "RU", "BY" } };

        public OnlinesSettings Kinoukr { get; set; } = new OnlinesSettings("kwwsv=22nlqrxnu1frp") { geo_hide = new string[] { "RU", "BY" } };

        public OnlinesSettings Kinotochka { get; set; } = new OnlinesSettings("kwwsv=22nlqryleh1fr", streamproxy: true);

        public OnlinesSettings CDNvideohub { get; set; } = new OnlinesSettings("kwwsv=22sod|hu1fgqylghrkxe1frp", streamproxy: true, enable: false);

        public OnlinesSettings Redheadsound { get; set; } = new OnlinesSettings("kwwsv=22uhgkhdgvrxqg1vwxglr");

        public OnlinesSettings iRemux { get; set; } = new OnlinesSettings("kwwsv=22phjdreodnr1frp") { corseu = true, geostreamproxy = new List<string>() { "UA" } };

        public PidTorSettings PidTor { get; set; } = new PidTorSettings() { enable = true, redapi = "http://redapi.cfhttp.top", min_sid = 15 };

        public FilmixSettings Filmix { get; set; } = new FilmixSettings("kwws=22ilopl{dss1f|rx") 
        {
            headers = HeadersModel.Init(
                ("Accept-Encoding", "gzip")
            )
        };

        public FilmixSettings FilmixTV { get; set; } = new FilmixSettings("kwwsv=22dsl1ilopl{1wy", enable: false);

        public FilmixSettings FilmixPartner { get; set; } = new FilmixSettings("kwws=22819418914;2sduwqhubdsl", enable: false);

        public ZetflixSettings Zetflix { get; set; } = new ZetflixSettings("kwwsv=22}hw0iol{1rqolqh") { geostreamproxy = new List<string>() { "ALL" }, hls = true };

        /// <summary>
        /// aHR0cHM6Ly9raW5vcGxheTIuc2l0ZS8=
        /// 
        /// a2lub2dvLm1lZGlh
        /// aHR0cHM6Ly9kNmRkMzg3ZS5vYnJ1dC5zaG93L2VtYmVkL2NqTS9jb250ZW50L2N6TndnVE8=
        /// aHR0cHM6Ly9maWxtLTIwMjQub3JnLw==
        /// </summary>
        public OnlinesSettings VideoDB { get; set; } = new OnlinesSettings("kwwsv=2263ei6:<31reuxw1vkrz") 
        { 
            geostreamproxy = new List<string>() { "ALL" },
            headers = HeadersModel.Init(
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("origin", "https://kinoplay2.site"),
                ("pragma", "no-cache"),
                ("priority", "u=1, i"),
                ("referer", "https://kinoplay2.site/"),
                ("sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "cross-site")
            )
        };

        /// <summary>
        /// aHR0cHM6Ly9jb2xkZmlsbS5pbmsv
        /// </summary>
        public OnlinesSettings CDNmovies { get; set; } = new OnlinesSettings("kwwsv=22frogfgq1{|}") 
        {
            headers = HeadersModel.Init(
                ("DNT", "1"),
                ("Upgrade-Insecure-Requests", "1")
            )
        };

        /// <summary>
        /// a2lub2dvLm1lZGlh
        /// </summary>
        public OnlinesSettings VDBmovies { get; set; } = new OnlinesSettings("kwwsv=22wuhphqgrxv0zhhn1fgqprylhv0vwuhdp1rqolqh") 
        {
            geostreamproxy = new List<string>() { "ALL" },
            headers = HeadersModel.Init(("Origin", "https://kinogo.media"), ("Referer", "https://kinogo.media/"))
        };

        public OnlinesSettings FanCDN { get; set; } = new OnlinesSettings("kwwsv=22v41idqvhuldo1wy", enable: true) 
        { 
            geostreamproxy = new List<string>() { "ALL" },
            headers = HeadersModel.Init(
                ("cache-control", "no-cache"),
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-mode", "navigate"),
                ("sec-fetch-site", "none"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1")
            )
        };

        public OnlinesSettings VCDN { get; set; } = new OnlinesSettings("kwws=2255;;71dqqdfgq1ff2qSE]ZGT8grh5", "kwwsv=22sruwdo1oxph{1krvw", token: "F:]{GKxq7f9PGpQQ|lyGxOgYTSXnMK:l", rip: true) { scheme = "http", geostreamproxy = new List<string>() { "ALL" } };

        /// <summary>
        /// aHR0cHM6Ly9tb3ZpZWxhYi5vbmU=
        /// </summary>
        public LumexSettings Lumex { get; set; } = new LumexSettings("kwwsv=22sruwdo1oxph{1krvw", "F:]{GKxq7f9PGpQQ|lyGxOgYTSXnMK:l", "oxph{1vsdfh", "tl6h28Hn1rL5") { enable = true, hls = true, scheme = "http", geostreamproxy = new List<string>() { "ALL" } };

        public VokinoSettings VoKino { get; set; } = new VokinoSettings("kwws=22dsl1yrnlqr1wy", streamproxy: true);

        public IframeVideoSettings IframeVideo { get; set; } = new IframeVideoSettings("kwwsv=22liudph1ylghr", "kwwsv=22ylghriudph1vsdfh", enable: false);

        /// <summary>
        /// aHR0cHM6Ly92aWQxNzMwODAxMzcwLmZvdHBybzEzNWFsdG8uY29tL2FwaS9pZGtwP2twX2lkPTEzOTI1NTAmZD1raW5vZ28uaW5j
        /// </summary>
        public OnlinesSettings HDVB { get; set; } = new OnlinesSettings("kwwsv=22dslye1frp", token: "8h5ih7f:3edig<d:747f7i4:3hh4e4<5");

        /// <summary>
        /// aHR0cHM6Ly92aWJpeC5vcmcvYXBpL2V4dGVybmFsL2RvY3VtZW50YXRpb24=
        /// </summary>
        public OnlinesSettings Vibix { get; set; } = new OnlinesSettings("kwwsv=22ylel{1ruj", enable: false, streamproxy: true);

        public OnlinesSettings Videoseed { get; set; } = new OnlinesSettings("kwwsv=22wy050nlqrvhuldo1qhw", streamproxy: true);

        /// <summary>
        /// kwwsv=22dsl1vuyns1frp
        /// kwwsv=22nsdss1olqn2dsl
        /// </summary>
        public KinoPubSettings KinoPub { get; set; } = new KinoPubSettings("kwwsv=22dsl1vuyns1frp") { filetype = "hls" };

        public AllohaSettings Alloha { get; set; } = new AllohaSettings("kwwsv=22dsl1dsexjdoo1ruj", "kwwsv=22wruvr0dv1doodunqrz1rqolqh", "", "", true, true);

        public AllohaSettings Mirage { get; set; } = new AllohaSettings("kwwsv=22dsl1dsexjdoo1ruj", "kwwsv=22roor0dv1doodunqrz1rqolqh", "fb7f82fe0ed1bd6d5dea884d57eeca", "", true, true) { enable = true, streamproxy = true };


        public KodikSettings Kodik { get; set; } = new KodikSettings("kwwsv=22nrglndsl1frp", "kwws=22nrgln1lqir", "hh438g49<d<7g87dhe7hgh<6f6f4935:", "", true);

        public OnlinesSettings AnilibriaOnline { get; set; } = new OnlinesSettings("kwwsv=22dsl1dqloleuld1wy");

        /// <summary>
        /// aHR0cHM6Ly9hbmlsaWIubWU=
        /// </summary>
        public OnlinesSettings AnimeLib { get; set; } = new OnlinesSettings("kwwsv=22dsl1pdqjdole1ph", streamproxy: true) 
        {
            headers = HeadersModel.Init(
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-mode", "navigate"),
                ("sec-fetch-site", "none"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1"),
                ("origin", "https://anilib.me"),
                ("referer", "https://anilib.me/")
            )
        };

        public OnlinesSettings AniMedia { get; set; } = new OnlinesSettings("kwwsv=22rqolqh1dqlphgld1wy", streamproxy: true, enable: false);

        public OnlinesSettings Animevost { get; set; } = new OnlinesSettings("kwwsv=22dqlphyrvw1ruj", streamproxy: true);

        public OnlinesSettings MoonAnime { get; set; } = new OnlinesSettings("kwwsv=22dsl1prrqdqlph1duw", token: ";98iHI0H5h4Ef05fd7640h9D4830:;3GIG0:6:F9E") { geo_hide = new string[] { "RU", "BY" } };

        public OnlinesSettings Animebesst { get; set; } = new OnlinesSettings("kwwsv=22dqlph41ehvw");

        public OnlinesSettings AnimeGo { get; set; } = new OnlinesSettings("kwwsv=22dqlphjr1ruj", streamproxy: true, enable: false);





        public RezkaSettings Voidboost { get; set; } = new RezkaSettings("kwwsv=22yrlgerrvw1qhw", streamproxy: true) { enable = false, rip = true };

        public OnlinesSettings Seasonvar { get; set; } = new OnlinesSettings("kwws=22dsl1vhdvrqydu1ux", enable: false, rip: true);

        public OnlinesSettings Lostfilmhd { get; set; } = new OnlinesSettings("kwws=22zzz1glvqh|oryh1ux", streamproxy: true, rip: true);

        public OnlinesSettings Eneyida { get; set; } = new OnlinesSettings("kwwsv=22hqh|lgd1wy", rip: true);
    }
}
