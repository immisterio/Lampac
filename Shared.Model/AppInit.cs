using Lampac.Models.SISI;
using Lampac.Models.LITE;
using Lampac.Models.AppConf;

namespace Shared.Model
{
    public class AppInit
    {
        public static string corseuhost { get; set; } = "https://cors.bwa.workers.dev";

        public static string rsizehost { get; set; } = "https://rsize.bwa.workers.dev";


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

        public SisiSettings Xvideos { get; set; } = new SisiSettings("https://www.xvideos.com");

        public SisiSettings PornHub { get; set; } = new SisiSettings("https://rt.pornhub.com", streamproxy: true);

        public SisiSettings PornHubPremium { get; set; } = new SisiSettings("https://rt.pornhubpremium.com", enable: false, streamproxy: true);



        public OnlinesSettings Kinobase { get; set; } = new OnlinesSettings("https://kinobase.org", streamproxy: true);

        public OnlinesSettings Rezka { get; set; } = new OnlinesSettings("https://rezka.ag");

        public OnlinesSettings Voidboost { get; set; } = new OnlinesSettings("https://voidboost.net", streamproxy: true);

        public OnlinesSettings Collaps { get; set; } = new OnlinesSettings("https://api.delivembd.ws");

        public OnlinesSettings Ashdi { get; set; } = new OnlinesSettings("https://base.ashdi.vip");

        public OnlinesSettings Eneyida { get; set; } = new OnlinesSettings("https://eneyida.tv");

        /// <summary>
        /// убрали собственный cdn
        /// </summary>
        public OnlinesSettings Kinokrad { get; set; } = new OnlinesSettings("https://kinokrad.cc", streamproxy: true, enable: false);

        public OnlinesSettings Kinotochka { get; set; } = new OnlinesSettings("https://kinotochka.co", streamproxy: true);

        public OnlinesSettings Redheadsound { get; set; } = new OnlinesSettings("https://redheadsound.studio", streamproxy: true);

        public OnlinesSettings Kinoprofi { get; set; } = new OnlinesSettings("https://kinoprofi.io", apihost: "https://api.kinoprofi.io", streamproxy: true, enable: false);

        public OnlinesSettings Lostfilmhd { get; set; } = new OnlinesSettings("http://www.disneylove.ru", streamproxy: true);

        public FilmixSettings Filmix { get; set; } = new FilmixSettings("http://filmixapp.cyou");

        public FilmixSettings FilmixPartner { get; set; } = new FilmixSettings("http://5.61.56.18/partner_api", enable: false);

        public OnlinesSettings Zetflix { get; set; } = new OnlinesSettings("https://zetfix.online");

        public OnlinesSettings VideoDB { get; set; } = new OnlinesSettings("https://kinoplay.site");

        public OnlinesSettings CDNmovies { get; set; } = new OnlinesSettings("https://cdnmovies.nl");

        public OnlinesSettings VDBmovies { get; set; } = new OnlinesSettings("https://1f29036bcf55d.sarnage.cc"/*, token: "02d56099082ad5ad586d7fe4e2493dd9"*/);


        public OnlinesSettings VCDN { get; set; } = new OnlinesSettings("https://89442664434375553.svetacdn.in/0HlZgU1l1mw5");

        public OnlinesSettings VoKino { get; set; } = new OnlinesSettings("http://api.vokino.tv", enable: false, streamproxy: true);

        public OnlinesSettings VideoAPI { get; set; } = new OnlinesSettings("https://videoapi.tv", token: "qR0taraBKvEZULgjoIRj69AJ7O6Pgl9O", enable: false);

        public IframeVideoSettings IframeVideo { get; set; } = new IframeVideoSettings("https://iframe.video", "https://videoframe.space");

        public OnlinesSettings HDVB { get; set; } = new OnlinesSettings("https://apivb.info", token: "5e2fe4c70bafd9a7414c4f170ee1b192");

        public OnlinesSettings Seasonvar { get; set; } = new OnlinesSettings("http://api.seasonvar.ru", enable: false);

        public KinoPubSettings KinoPub { get; set; } = new KinoPubSettings("https://api.srvkp.com") { uhd = true, enable = true };

        public BazonSettings Bazon { get; set; } = new BazonSettings("https://bazon.cc", "", true);

        public AllohaSettings Alloha { get; set; } = new AllohaSettings("https://api.alloha.tv", "https://torso.as.alloeclub.com", "", "", true);

        public KodikSettings Kodik { get; set; } = new KodikSettings("https://kodikapi.com", "http://kodik.biz", "b7cc4293ed475c4ad1fd599d114f4435", "", true);


        public OnlinesSettings AnilibriaOnline { get; set; } = new OnlinesSettings("https://api.anilibria.tv");


        /// <summary>
        /// cdn сервер сдох
        /// </summary>
        public OnlinesSettings AniMedia { get; set; } = new OnlinesSettings("https://online.animedia.tv", enable: false, streamproxy: true);

        public OnlinesSettings AnimeGo { get; set; } = new OnlinesSettings("https://animego.org", streamproxy: true);

        public OnlinesSettings Animevost { get; set; } = new OnlinesSettings("https://animevost.org", streamproxy: true);

        public OnlinesSettings Animebesst { get; set; } = new OnlinesSettings("https://anime1.best", streamproxy: true);
    }
}
