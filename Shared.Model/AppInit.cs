using Lampac.Models.SISI;
using Lampac.Models.LITE;
using Lampac.Models.AppConf;

namespace Shared.Model
{
    public class AppInit
    {
        public static string corseuhost { get; set; } = "https://cors.apn.monster";

        public string? apn { get; set; } = "https://apn.watch";

        public string? corsehost { get; set; }

        public SisiConf sisi { get; set; } = new SisiConf() { heightPicture = 200 };

        public SisiSettings BongaCams { get; set; } = new SisiSettings("https://rus.bongacams.com");

        public SisiSettings Chaturbate { get; set; } = new SisiSettings("https://chaturbate.com");

        public SisiSettings Ebalovo { get; set; } = new SisiSettings("https://www.ebalovo.pro", streamproxy: true);

        public SisiSettings Eporner { get; set; } = new SisiSettings("https://www.eporner.com", streamproxy: true);

        public SisiSettings HQporner { get; set; } = new SisiSettings("https://m.hqporner.com");

        public SisiSettings Porntrex { get; set; } = new SisiSettings("https://www.porntrex.com");

        public SisiSettings Spankbang { get; set; } = new SisiSettings("https://ru.spankbang.com");

        public SisiSettings Xhamster { get; set; } = new SisiSettings("https://ru.xhamster.com");

        public SisiSettings Xnxx { get; set; } = new SisiSettings("https://www.xnxx.com");

        public SisiSettings Tizam { get; set; } = new SisiSettings("https://ww.tizam.pw", streamproxy: true);

        public SisiSettings Xvideos { get; set; } = new SisiSettings("https://www.xvideos.com");

        public SisiSettings PornHub { get; set; } = new SisiSettings("https://rt.pornhub.com", streamproxy: true);

        public SisiSettings PornHubPremium { get; set; } = new SisiSettings("https://rt.pornhubpremium.com", enable: false, streamproxy: true);



        public OnlinesSettings Kinobase { get; set; } = new OnlinesSettings("https://kinobase.org", streamproxy: true);

        public RezkaSettings Rezka { get; set; } = new RezkaSettings("https://rezka.ag") { uacdn = "https://prx.ukrtelcdn.net" };

        public RezkaSettings Voidboost { get; set; } = new RezkaSettings("https://voidboost.tv", streamproxy: true);

        public OnlinesSettings Collaps { get; set; } = new OnlinesSettings("https://api.delivembd.ws");

        public OnlinesSettings Ashdi { get; set; } = new OnlinesSettings("https://base.ashdi.vip");

        public OnlinesSettings Eneyida { get; set; } = new OnlinesSettings("https://eneyida.tv");

        public OnlinesSettings Kinotochka { get; set; } = new OnlinesSettings("https://kinovibe.co", streamproxy: true);

        public OnlinesSettings Redheadsound { get; set; } = new OnlinesSettings("https://redheadsound.studio", streamproxy: true);

        public OnlinesSettings iRemux { get; set; } = new OnlinesSettings("https://oblakofailov.ru", streamproxy: true) { corseu = true };

        public OnlinesSettings Lostfilmhd { get; set; } = new OnlinesSettings("http://www.disneylove.ru", streamproxy: true, rip: true);

        public FilmixSettings Filmix { get; set; } = new FilmixSettings("http://filmixapp.cyou");

        public FilmixSettings FilmixPartner { get; set; } = new FilmixSettings("http://5.61.56.18/partner_api", enable: false);

        public OnlinesSettings Zetflix { get; set; } = new OnlinesSettings("https://zetfix.online", enable: false);

        public OnlinesSettings VideoDB { get; set; } = new OnlinesSettings("https://lordsfilm3.net");

        public OnlinesSettings CDNmovies { get; set; } = new OnlinesSettings("https://coldcdn.xyz");

        public OnlinesSettings VDBmovies { get; set; } = new OnlinesSettings("https://1f29036bcf55d.sarnage.cc"/*, token: "02d56099082ad5ad586d7fe4e2493dd9"*/);


        public OnlinesSettings VCDN { get; set; } = new OnlinesSettings("https://89442664434375553.svetacdn.in/0HlZgU1l1mw5", "https://videocdn.tv", token: "3i40G5TSECmLF77oAqnEgbx61ZWaOYaE", streamproxy: true);

        public OnlinesSettings VoKino { get; set; } = new OnlinesSettings("http://api.vokino.tv", enable: false, streamproxy: true);

        public IframeVideoSettings IframeVideo { get; set; } = new IframeVideoSettings("https://iframe.video", "https://videoframe.space", enable: false);

        public OnlinesSettings HDVB { get; set; } = new OnlinesSettings("https://apivb.info", token: "5e2fe4c70bafd9a7414c4f170ee1b192");

        public OnlinesSettings Seasonvar { get; set; } = new OnlinesSettings("http://api.seasonvar.ru", enable: false);

        public KinoPubSettings KinoPub { get; set; } = new KinoPubSettings("https://api.srvkp.com") { uhd = true, hevc = true, hdr = true, filetype = "hls4" };

        public AllohaSettings Alloha { get; set; } = new AllohaSettings("https://api.alloha.tv", "https://torso-as.newplayjj.com:9443", "", "", true);

        public KodikSettings Kodik { get; set; } = new KodikSettings("https://kodikapi.com", "http://kodik.biz", "71d163b40d50397a86ca54c366f33b72", "", true);


        public OnlinesSettings AnilibriaOnline { get; set; } = new OnlinesSettings("https://api.anilibria.tv");

        public OnlinesSettings AniMedia { get; set; } = new OnlinesSettings("https://online.animedia.tv", streamproxy: true);

        public OnlinesSettings AnimeGo { get; set; } = new OnlinesSettings("https://animego.org", streamproxy: true);

        public OnlinesSettings Animevost { get; set; } = new OnlinesSettings("https://animevost.org", streamproxy: true);

        public OnlinesSettings Animebesst { get; set; } = new OnlinesSettings("https://anime1.best", streamproxy: true);
    }
}
