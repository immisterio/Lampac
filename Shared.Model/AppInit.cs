﻿using Lampac.Models.SISI;
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

        public SisiSettings BongaCams { get; set; } = new SisiSettings("BongaCams", "kwwsv=22hh1erqjdfdpv1frp")
        {
            headers = HeadersModel.Init(
                ("accept", "*/*"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("priority", "u=1, i"),
                ("cookie", "bonga20120608=99834a7a1dee3a3d50f77380ba22a881; ts_type2=1; fv=BGxjZGtjZGD3ZD==; uh=IIDlE0MYIaAkF1cxEwS2FyyPqSMipj==; reg_ver2=3; sg=861; warning18=%5B%22et_EE%22%5D; ol4ekwvk4v=1834809388; ls02=camscore; ls01=%7B%22th_type%22%3A%22live%22%2C%22display%22%3A%22medium%22%7D; BONGA_REF=https%3A%2F%2Fwww.google.com%2F"),
                ("referer", "{host}/"),
                ("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "same-origin"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36"),
                ("x-requested-with", "XMLHttpRequest")
            ).ToDictionary()
        };

        public SisiSettings Chaturbate { get; set; } = new SisiSettings("Chaturbate", "kwwsv=22fkdwxuedwh1frp");

        public SisiSettings Ebalovo { get; set; } = new SisiSettings("Ebalovo", "kwwsv=22zzz1hedoryr1sur");

        public SisiSettings Eporner { get; set; } = new SisiSettings("Eporner", "kwwsv=22zzz1hsruqhu1frp", streamproxy: true);

        public SisiSettings HQporner { get; set; } = new SisiSettings("HQporner", "kwwsv=22p1ktsruqhu1frp")
        {
            geostreamproxy = new string[] { "ALL" },
            headers = HeadersModel.Init("referer", "{host}").ToDictionary()
        };

        public SisiSettings Porntrex { get; set; } = new SisiSettings("Porntrex", "kwwsv=22zzz1sruqwuh{1frp");

        public SisiSettings Spankbang { get; set; } = new SisiSettings("Spankbang", "kwwsv=22ux1vsdqnedqj1frp")
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
            ).ToDictionary()
        };

        public SisiSettings Xhamster { get; set; } = new SisiSettings("Xhamster", "kwwsv=22ux1{kdpvwhu1frp") 
        {
            headers = HeadersModel.Init(
                ("accept", "text/plain, */*; q=0.0"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("priority", "u=1, i"),
                ("sec-ch-ua", "\"Not(A: Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "cross-site"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public SisiSettings Xnxx { get; set; } = new SisiSettings("Xnxx", "kwwsv=22zzz1{q{{1frp");

        public SisiSettings Tizam { get; set; } = new SisiSettings("Tizam", "kwwsv=22lq1wl}dp1lqir");

        public SisiSettings Xvideos { get; set; } = new SisiSettings("Xvideos", "kwwsv=22zzz1{ylghrv1frp");

        public SisiSettings XvideosRED { get; set; } = new SisiSettings("XvideosRED", "kwwsv=22zzz1{ylghrv1uhg", enable: false);

        public SisiSettings PornHub { get; set; } = new SisiSettings("PornHub", "kwwsv=22uw1sruqkxe1frp", streamproxy: true)
        {
            headers = HeadersModel.Init(
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-site", "none"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1"),
                ("cookie", "platform=pc; bs=ukbqk2g03joiqzu68gitadhx5bhkm48j; ss=250837987735652383; fg_0d2ec4cbd943df07ec161982a603817e=56239.100000; atatusScript=hide; _gid=GA1.2.309162272.1686472069; d_fs=1; d_uidb=2f5e522a-fa28-a0fe-0ab2-fd90f45d96c0; d_uid=2f5e522a-fa28-a0fe-0ab2-fd90f45d96c0; d_uidb=2f5e522a-fa28-a0fe-0ab2-fd90f45d96c0; accessAgeDisclaimerPH=1; cookiesBannerSeen=1; _gat=1; __s=64858645-42FE722901BBA6E6-125476E1; __l=64858645-42FE722901BBA6E6-125476E1; hasVisited=1; fg_f916a4d27adf4fc066cd2d778b4d388e=78731.100000; fg_fa3f0973fd973fca3dfabc86790b408b=12606.100000; _ga_B39RFFWGYY=GS1.1.1686472069.1.1.1686472268.0.0.0; _ga=GA1.1.1515398043.1686472069")
            ).ToDictionary()
        };

        public SisiSettings PornHubPremium { get; set; } = new SisiSettings("PornHubPremium", "kwwsv=22uw1sruqkxesuhplxp1frp", streamproxy: true, enable: false)
        {
            headers = HeadersModel.Init(
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-site", "none"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1")
            ).ToDictionary()
        };



        public RezkaSettings Rezka { get; set; } = new RezkaSettings("Rezka", "kwwsv=22kguh}nd1ph", true) { hls = true, scheme = "http" };

        public RezkaSettings RezkaPrem { get; set; } = new RezkaSettings("RezkaPrem", null) { enable = false, hls = true, scheme = "http" };

        public CollapsSettings Collaps { get; set; } = new CollapsSettings("Collaps", "kwwsv=22dsl1qlqvho1zv", streamproxy: true, two: false) 
        {
            headers = HeadersModel.Init(
                ("Origin", "{host}"), 
                ("Referer", "{host}/"),
                ("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1")
            ).ToDictionary(),

            headers_stream = HeadersModel.Init(
                ("Origin", "{host}"),
                ("Referer", "{host}/"),
                ("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1")
            ).ToDictionary()
        };

        public OnlinesSettings Ashdi { get; set; } = new OnlinesSettings("Ashdi", "kwwsv=22edvh1dvkgl1yls") { geo_hide = new string[] { "RU", "BY" } };

        public OnlinesSettings Kinoukr { get; set; } = new OnlinesSettings("Kinoukr", "kwwsv=22nlqrxnu1frp") 
        { 
            geo_hide = new string[] { "RU", "BY" },
            headers = HeadersModel.Init
            (
                ("cache-control", "no-cache"),
                ("cookie", "legit_user=1;"),
                ("dnt", "1"),
                ("origin", "{host}"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("referer", "{host}/"),
                ("sec-ch-ua", "\"Not A(Brand\";v=\"8\", \"Chromium\";v=\"132\", \"Google Chrome\";v=\"132\""),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-mode", "navigate"),
                ("sec-fetch-site", "same-origin"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public OnlinesSettings Kinotochka { get; set; } = new OnlinesSettings("Kinotochka", "kwwsv=22nlqryleh1fr", streamproxy: true);

        public OnlinesSettings CDNvideohub { get; set; } = new OnlinesSettings("CDNvideohub", "kwwsv=22sod|hu1fgqylghrkxe1frp", streamproxy: true, enable: false)
        {
            headers = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("cache-control", "no-cache"),
                ("referer", "encrypt:kwwsv=22kgnlqr1sxe2"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "iframe"),
                ("sec-fetch-mode", "navigate"),
                ("sec-fetch-site", "cross-site"),
                ("sec-fetch-storage-access", "active"),
                ("upgrade-insecure-requests", "1"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public OnlinesSettings Redheadsound { get; set; } = new OnlinesSettings("Redheadsound", "kwwsv=22uhgkhdgvrxqg1vwxglr") 
        {
            headers = HeadersModel.Init("referer", "{host}/").ToDictionary()
        };

        public OnlinesSettings iRemux { get; set; } = new OnlinesSettings("iRemux", "kwwsv=22phjdreodnr1frp") { corseu = true, geostreamproxy = new string[] { "UA" } };

        public PidTorSettings PidTor { get; set; } = new PidTorSettings() { enable = true, redapi = "http://redapi.cfhttp.top", min_sid = 15 };

        public FilmixSettings Filmix { get; set; } = new FilmixSettings("Filmix", "kwws=22ilopl{dss1f|rx") 
        {
            headers = HeadersModel.Init(
                ("Accept-Encoding", "gzip")
            ).ToDictionary()
        };

        public FilmixSettings FilmixTV { get; set; } = new FilmixSettings("FilmixTV", "kwwsv=22dsl1ilopl{1wy", enable: false) { pro = true };

        public FilmixSettings FilmixPartner { get; set; } = new FilmixSettings("FilmixPartner", "kwws=22819418914;2sduwqhubdsl", enable: false);

        public ZetflixSettings Zetflix { get; set; } = new ZetflixSettings("Zetflix", "kwwsv=22}hw0iol{1rqolqh") { geostreamproxy = new string[] { "ALL" }, hls = true };

        /// <summary>
        /// aHR0cHM6Ly9raW5vcGxheTIuc2l0ZS8=
        /// a2lub2dvLm1lZGlh
        /// aHR0cHM6Ly9maWxtLTIwMjQub3JnLw==
        /// </summary>
        public OnlinesSettings VideoDB { get; set; } = new OnlinesSettings("VideoDB", "kwwsv=2263ei6:<31reuxw1vkrz") 
        {
            geostreamproxy = new string[] { "ALL" },
            headers = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("origin", "encrypt:kwwsv=22nlqrsod|51vlwh"),
                ("pragma", "no-cache"),
                ("priority", "u=1, i"),
                ("referer", "encrypt:kwwsv=22nlqrsod|51vlwh2"),
                ("sec-ch-ua", "\"Not A(Brand\";v=\"8\", \"Chromium\";v=\"132\", \"Google Chrome\";v=\"132\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "cross-site"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        /// <summary>
        /// aHR0cHM6Ly9jb2xkZmlsbS5pbmsv
        /// </summary>
        public OnlinesSettings CDNmovies { get; set; } = new OnlinesSettings("CDNmovies", "kwwsv=22frogfgq1{|}") 
        {
            headers = HeadersModel.Init(
                ("DNT", "1"),
                ("Upgrade-Insecure-Requests", "1")
            ).ToDictionary()
        };

        public OnlinesSettings VDBmovies { get; set; } = new OnlinesSettings("VDBmovies", "kwwsv=22xjo|0wxunh|1fgqprylhv0vwuhdp1rqolqh") 
        {
            geostreamproxy = new string[] { "ALL" },
            headers = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("referer", "encrypt:kwwsv=22nlqrrqolqh0kg1frp2"),
                ("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "iframe"),
                ("sec-fetch-mode", "navigate"),
                ("sec-fetch-site", "cross-site"),
                ("sec-fetch-storage-access", "active"),
                ("upgrade-insecure-requests", "1"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public OnlinesSettings FanCDN { get; set; } = new OnlinesSettings("FanCDN", "kwwsv=22v71idqvhuldo1wy") 
        { 
            streamproxy = true,
            headers = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("referer", "{host}/"),
                ("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "iframe"),
                ("sec-fetch-mode", "navigate"),
                ("sec-fetch-site", "cross-site"),
                ("sec-fetch-storage-access", "active"),
                ("upgrade-insecure-requests", "1"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36")
            ).ToDictionary(),
            headers_stream = HeadersModel.Init(
                ("accept", "*/*"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("referer", "encrypt:kwwsv=22idqfgq1qhw2"),
                ("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "same-site"),
                ("sec-fetch-storage-access", "active"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36")
            ).ToDictionary(),
        };

        public LumexSettings Lumex { get; set; } = new LumexSettings("Lumex", "kwwsv=22sruwdo1oxph{1krvw", "F:]{GKxq7f9PGpQQ|lyGxOgYTSXnMK:l", "lumex.space", "fReBbuvVF6Rv") 
        {
            enable = false, 
            args_api = "&domain=kinogo.bet&url=kinogo.bet",
            hls = true, scheme = "http", geostreamproxy = new string[] { "ALL" },
            headers = HeadersModel.Init(
                ("accept", "*/*"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("Origin", "https://p.lumex.space"),
                ("Referer", "https://p.lumex.space/"),
                ("pragma", "no-cache"),
                ("priority", "u=1, i"),
                ("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "same-site"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public VokinoSettings VoKino { get; set; } = new VokinoSettings("VoKino", "kwws=22dsl1yrnlqr1wy", streamproxy: true);

        public IframeVideoSettings IframeVideo { get; set; } = new IframeVideoSettings("IframeVideo", "kwwsv=22liudph1ylghr", "kwwsv=22ylghriudph1vsdfh", enable: false);

        /// <summary>
        /// aHR0cHM6Ly92aWQxNzMwODAxMzcwLmZvdHBybzEzNWFsdG8uY29tL2FwaS9pZGtwP2twX2lkPTEzOTI1NTAmZD1raW5vZ28uaW5j
        /// </summary>
        public OnlinesSettings HDVB { get; set; } = new OnlinesSettings("HDVB", "kwwsv=22dslye1frp", token: "8h5ih7f:3edig<d:747f7i4:3hh4e4<5");

        /// <summary>
        /// aHR0cHM6Ly92aWJpeC5vcmcvYXBpL2V4dGVybmFsL2RvY3VtZW50YXRpb24=
        /// </summary>
        public OnlinesSettings Vibix { get; set; } = new OnlinesSettings("Vibix", "kwwsv=22ylel{1ruj", enable: false, streamproxy: true);

        public OnlinesSettings Videoseed { get; set; } = new OnlinesSettings("Videoseed", "kwwsv=22ylghrvhhg1wy", enable: false, streamproxy: true);

        /// <summary>
        /// kwwsv=22dsl1vuyns1frp
        /// kwwsv=22nsdss1olqn2dsl
        /// </summary>
        public KinoPubSettings KinoPub { get; set; } = new KinoPubSettings("KinoPub", "kwwsv=22dsl1vuyns1frp") 
        { 
            filetype = "hls", 
            headers = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-mode", "navigate"),
                ("sec-fetch-site", "none"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public AllohaSettings Alloha { get; set; } = new AllohaSettings("Alloha", "kwwsv=22dsl1dsexjdoo1ruj", "kwwsv=22wruvr0dv1doodunqrz1rqolqh", "", "", true, true);

        public AllohaSettings Mirage { get; set; } = new AllohaSettings("Mirage", "kwwsv=22dsl1dsexjdoo1ruj", "kwwsv=22roor0dv1dojrqrhz1rqolqh=<7<6", "875912cc3b0d48c90397c419a957e8", "", true, true) { enable = true, streamproxy = true };


        public KodikSettings Kodik { get; set; } = new KodikSettings("Kodik", "kwwsv=22nrglndsl1frp", "kwws=22nrgln1lqir", "hh438g49<d<7g87dhe7hgh<6f6f4935:", "", true);

        public OnlinesSettings AnilibriaOnline { get; set; } = new OnlinesSettings("AnilibriaOnline", "kwwsv=22dsl1dqloleuld1wy");

        /// <summary>
        /// aHR0cHM6Ly9hbmlsaWIubWU=
        /// </summary>
        public OnlinesSettings AnimeLib { get; set; } = new OnlinesSettings("AnimeLib", "kwwsv=22dsl51pdqjdole1ph", streamproxy: true) 
        {
            headers = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("origin", "encrypt:kwwsv=22dqlole1ph"),
                ("referer", "encrypt:kwwsv=22dqlole1ph2"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("sec-ch-ua", "\"Not A(Brand\";v=\"8\", \"Chromium\";v=\"132\", \"Google Chrome\";v=\"132\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-mode", "navigate"),
                ("sec-fetch-site", "none"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public OnlinesSettings AniMedia { get; set; } = new OnlinesSettings("AniMedia", "kwwsv=22rqolqh1dqlphgld1wy", streamproxy: true, enable: false);

        public OnlinesSettings Animevost { get; set; } = new OnlinesSettings("Animevost", "kwwsv=22dqlphyrvw1ruj", streamproxy: true);

        public OnlinesSettings MoonAnime { get; set; } = new OnlinesSettings("MoonAnime", "kwwsv=22dsl1prrqdqlph1duw", token: ";98iHI0H5h4Ef05fd7640h9D4830:;3GIG0:6:F9E") { geo_hide = new string[] { "RU", "BY" } };

        public OnlinesSettings Animebesst { get; set; } = new OnlinesSettings("Animebesst", "kwwsv=22dqlph41ehvw");

        public OnlinesSettings AnimeGo { get; set; } = new OnlinesSettings("AnimeGo", "kwwsv=22dqlphjr1ruj", streamproxy: true, enable: false) 
        {
            headers_stream = HeadersModel.Init(
                ("origin", "https://aniboom.one"),
                ("referer", "https://aniboom.one/")
            ).ToDictionary()
        };






        public OnlinesSettings Kinobase { get; set; } = new OnlinesSettings("Kinobase", "kwwsv=22nlqredvh1ruj") { rip = true, geostreamproxy = new string[] { "ALL" } };

        public RezkaSettings Voidboost { get; set; } = new RezkaSettings("Voidboost", "kwwsv=22yrlgerrvw1qhw", streamproxy: true) { enable = false, rip = true };

        public OnlinesSettings Seasonvar { get; set; } = new OnlinesSettings("Seasonvar", "kwws=22dsl1vhdvrqydu1ux", enable: false, rip: true);

        public OnlinesSettings Lostfilmhd { get; set; } = new OnlinesSettings("Lostfilmhd", "kwws=22zzz1glvqh|oryh1ux", streamproxy: true, rip: true);

        public OnlinesSettings Eneyida { get; set; } = new OnlinesSettings("Eneyida", "kwwsv=22hqh|lgd1wy", rip: true);

        public OnlinesSettings VCDN { get; set; } = new OnlinesSettings("VCDN", "kwws=2255;;71dqqdfgq1ff2qSE]ZGT8grh5", "kwwsv=22sruwdo1oxph{1krvw", token: "F:]{GKxq7f9PGpQQ|lyGxOgYTSXnMK:l", rip: true) { scheme = "http", geostreamproxy = new string[] { "ALL" } };
    }
}
